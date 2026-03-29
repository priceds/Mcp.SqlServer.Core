using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Dapper;
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
        var command = DapperExtensions.Command(
            sql,
            DapperExtensions.ToDynamicParameters(parameters),
            _validator.SelectTimeout(operation, options),
            cancellationToken);
        var queryRows = await connectionLease.Connection.QueryAsync(command).ConfigureAwait(false);
        var rows = DapperExtensions.ToDictionaryRows(queryRows, options.Safety.MaxRows);
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
        var command = DapperExtensions.Command(
            sql,
            DapperExtensions.ToDynamicParameters(parameters),
            _validator.SelectTimeout(operation, options),
            cancellationToken);

        var rows = await connectionLease.Connection.ExecuteAsync(command).ConfigureAwait(false);
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

        var command = DapperExtensions.Command(
            wrappedSql,
            DapperExtensions.ToDynamicParameters(parameters),
            _validator.SelectTimeout(SqlOperationKind.Read, options),
            cancellationToken);
        var planXml = await ExtractPlanXmlAsync(connectionLease.Connection, command).ConfigureAwait(false);
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
        var command = DapperExtensions.Command(
            IdentifierGuard.QuoteMultipart(schema, procedure),
            DapperExtensions.ToDynamicParameters(parameters),
            options.Safety.DefaultCommandTimeout,
            cancellationToken,
            commandType: System.Data.CommandType.StoredProcedure);
        var rows = await connectionLease.Connection.ExecuteAsync(command).ConfigureAwait(false);
        return new SqlWriteResponse(connectionLease.Database, rows, $"Stored procedure {schema}.{procedure} executed.");
    }

    private async Task<SqlConnectionLease> OpenConnectionAsync(string? database, CancellationToken cancellationToken)
    {
        return await _connectionFactory.OpenAsync(database, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ExtractPlanXmlAsync(SqlConnection connection, CommandDefinition command)
    {
        var stopwatch = Stopwatch.StartNew();
        var builder = new StringBuilder();
        using var grid = await connection.QueryMultipleAsync(command).ConfigureAwait(false);

        while (!grid.IsConsumed)
        {
            var rows = await grid.ReadAsync().ConfigureAwait(false);
            foreach (var row in rows)
            {
                if (row is IDictionary<string, object?> typed)
                {
                    foreach (var value in typed.Values)
                    {
                        if (value is string text)
                        {
                            builder.Append(text);
                        }
                    }
                }
            }
        }

        stopwatch.Stop();
        _logger.LogInformation("Execution plan collected in {ElapsedMs} ms", stopwatch.ElapsedMilliseconds);
        return builder.ToString();
    }
}
