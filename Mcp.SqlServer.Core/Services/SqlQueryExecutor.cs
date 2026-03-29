using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mcp.SqlServer.Core.Abstractions;
using Mcp.SqlServer.Core.Caching;
using Mcp.SqlServer.Core.Internal;
using Mcp.SqlServer.Core.Security;

namespace Mcp.SqlServer.Core.Services;

internal sealed class SqlQueryExecutor
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly SqlSafetyValidator _validator;
    private readonly SqlCommandClassifier _classifier;
    private readonly ISqlServerCache _cache;
    private readonly IOptionsMonitor<SqlServerMcpOptions> _options;
    private readonly ILogger<SqlQueryExecutor> _logger;

    public SqlQueryExecutor(
        SqlConnectionFactory connectionFactory,
        SqlSafetyValidator validator,
        SqlCommandClassifier classifier,
        ISqlServerCache cache,
        IOptionsMonitor<SqlServerMcpOptions> options,
        ILogger<SqlQueryExecutor> logger)
    {
        _connectionFactory = connectionFactory;
        _validator = validator;
        _classifier = classifier;
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    public async Task<(SqlQueryResponse Response, bool CacheHit)> ExecuteQueryAsync(
        string sql,
        Dictionary<string, JsonElement>? parameters,
        string? database,
        CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var normalizedSql = _validator.NormalizeSql(sql);
        var operation = _validator.ValidateSql(sql, database);
        var actualDatabase = database ?? new SqlConnectionStringBuilder(options.ConnectionString).InitialCatalog;

        var canCache = _classifier.IsDeterministicRead(sql);
        var cacheKey = CacheKeyBuilder.ForQuery(actualDatabase, normalizedSql, parameters, options.CapabilityProfile.ToString());
        if (canCache)
        {
            var cached = await _cache.TryGetAsync<SqlQueryResponse>(cacheKey, cancellationToken).ConfigureAwait(false);
            if (cached.Found && cached.Value is not null)
            {
                return (cached.Value, true);
            }
        }

        await using var connectionLease = await OpenConnectionAsync(database, cancellationToken).ConfigureAwait(false);
        await using var command = CreateCommand(connectionLease.Connection, sql, parameters, operation, options);
        var rows = await ReadRowsAsync(command, options.Safety.MaxRows, cancellationToken).ConfigureAwait(false);
        var response = new SqlQueryResponse(connectionLease.Database, rows.Count, rows, sql);

        if (canCache)
        {
            await _cache.SetAsync(cacheKey, response, options.CachePolicy.DeterministicQueryTtl, cancellationToken).ConfigureAwait(false);
        }

        return (response, false);
    }

    public async Task<SqlWriteResponse> ExecuteNonQueryAsync(
        string sql,
        Dictionary<string, JsonElement>? parameters,
        string? database,
        bool allowAdmin,
        CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var operation = _validator.ValidateSql(sql, database, allowAdmin);
        await using var connectionLease = await OpenConnectionAsync(database, cancellationToken).ConfigureAwait(false);
        await using var command = CreateCommand(connectionLease.Connection, sql, parameters, operation, options);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return new SqlWriteResponse(connectionLease.Database, rows, $"{operation} statement completed.");
    }

    public async Task<QueryPlanResponse> GetPlanAsync(
        string sql,
        Dictionary<string, JsonElement>? parameters,
        string? database,
        string planMode,
        CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (!options.EnableExecutionPlans)
        {
            throw new ModelContextProtocol.McpException("Execution plan tools are disabled.");
        }

        _validator.ValidateSql(sql, database);
        var normalizedSql = _validator.NormalizeSql(sql);
        var actualDatabase = database ?? new SqlConnectionStringBuilder(options.ConnectionString).InitialCatalog;
        var cacheKey = CacheKeyBuilder.ForPlan(actualDatabase, $"{planMode}:{normalizedSql}");
        var cached = await _cache.TryGetAsync<QueryPlanResponse>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached.Found && cached.Value is not null)
        {
            return cached.Value;
        }

        await using var connectionLease = await OpenConnectionAsync(database, cancellationToken).ConfigureAwait(false);
        var wrappedSql = planMode.Equals("estimated", StringComparison.OrdinalIgnoreCase)
            ? $"SET SHOWPLAN_XML ON; {sql}; SET SHOWPLAN_XML OFF;"
            : $"SET STATISTICS XML ON; {sql}; SET STATISTICS XML OFF;";

        await using var command = CreateCommand(connectionLease.Connection, wrappedSql, parameters, SqlOperationKind.Read, options);
        var planXml = await ExtractPlanXmlAsync(command, cancellationToken).ConfigureAwait(false);
        var response = new QueryPlanResponse(connectionLease.Database, planMode, planXml, sql);
        await _cache.SetAsync(cacheKey, response, options.CachePolicy.QueryPlanTtl, cancellationToken).ConfigureAwait(false);
        return response;
    }

    public async Task<SqlWriteResponse> ExecuteStoredProcedureAsync(
        string schema,
        string procedure,
        Dictionary<string, JsonElement>? parameters,
        string? database,
        CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        _validator.ValidateStoredProcedureAccess(options);

        await using var connectionLease = await OpenConnectionAsync(database, cancellationToken).ConfigureAwait(false);
        await using var command = connectionLease.Connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = IdentifierGuard.QuoteMultipart(schema, procedure);
        command.CommandTimeout = (int)options.Safety.DefaultCommandTimeout.TotalSeconds;

        AddParameters(command, parameters);
        var rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return new SqlWriteResponse(connectionLease.Database, rows, $"Stored procedure {schema}.{procedure} executed.");
    }

    private async Task<SqlConnectionLease> OpenConnectionAsync(string? database, CancellationToken cancellationToken)
    {
        return await _connectionFactory.OpenAsync(database, cancellationToken).ConfigureAwait(false);
    }

    private SqlCommand CreateCommand(
        SqlConnection connection,
        string sql,
        Dictionary<string, JsonElement>? parameters,
        SqlOperationKind operation,
        SqlServerMcpOptions options)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = (int)_validator.SelectTimeout(operation, options).TotalSeconds;
        AddParameters(command, parameters);
        return command;
    }

    private static void AddParameters(SqlCommand command, Dictionary<string, JsonElement>? parameters)
    {
        if (parameters is null)
        {
            return;
        }

        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name.StartsWith('@') ? name : $"@{name}";
            parameter.Value = JsonElementConverter.ToValue(value) ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadRowsAsync(SqlCommand command, int maxRows, CancellationToken cancellationToken)
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new Dictionary<string, object?>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                row[reader.GetName(index)] = await reader.IsDBNullAsync(index, cancellationToken).ConfigureAwait(false)
                    ? null
                    : reader.GetValue(index);
            }

            rows.Add(row);
            if (rows.Count >= maxRows)
            {
                break;
            }
        }

        return rows;
    }

    private async Task<string> ExtractPlanXmlAsync(SqlCommand command, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var builder = new StringBuilder();

        do
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    if (reader.GetFieldType(index) == typeof(string))
                    {
                        builder.Append(reader.GetString(index));
                    }
                }
            }
        }
        while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));

        stopwatch.Stop();
        _logger.LogInformation("Execution plan collected in {ElapsedMs} ms", stopwatch.ElapsedMilliseconds);
        return builder.ToString();
    }
}
