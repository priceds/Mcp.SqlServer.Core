using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Mcp.SqlServer.Core.Abstractions;
using Mcp.SqlServer.Core.Internal;

namespace Mcp.SqlServer.Core.Services;

internal sealed class SqlServerMcpToolService
{
    private readonly SqlSchemaService _schemaService;
    private readonly SqlQueryExecutor _queryExecutor;
    private readonly SqlReportingService _reportingService;
    private readonly SqlAdminService _adminService;
    private readonly ISqlServerMcpAuditSink _auditSink;
    private readonly IOptionsMonitor<SqlServerMcpOptions> _options;

    public SqlServerMcpToolService(
        SqlSchemaService schemaService,
        SqlQueryExecutor queryExecutor,
        SqlReportingService reportingService,
        SqlAdminService adminService,
        ISqlServerMcpAuditSink auditSink,
        IOptionsMonitor<SqlServerMcpOptions> options)
    {
        _schemaService = schemaService;
        _queryExecutor = queryExecutor;
        _reportingService = reportingService;
        _adminService = adminService;
        _auditSink = auditSink;
        _options = options;
    }

    public async Task<ToolEnvelope> ListDatabasesAsync(CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var (payload, cacheHit) = await _schemaService.ListDatabasesAsync(cancellationToken).ConfigureAwait(false);
        return Wrap("list_databases", correlationId, payload, cacheHit);
    }

    public async Task<ToolEnvelope> ListSchemasAsync(ListSchemasRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var (payload, cacheHit) = await _schemaService.ListSchemasAsync(request.Database, cancellationToken).ConfigureAwait(false);
        return Wrap("list_schemas", correlationId, payload, cacheHit);
    }

    public async Task<ToolEnvelope> ListTablesAsync(ListTablesRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var (payload, cacheHit) = await _schemaService.ListTablesAsync(request.Database, request.Schema, cancellationToken).ConfigureAwait(false);
        return Wrap("list_tables", correlationId, payload, cacheHit);
    }

    public async Task<ToolEnvelope> DescribeTableAsync(DescribeTableRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var (payload, cacheHit) = await _schemaService.DescribeTableAsync(request.Schema, request.Table, request.Database, cancellationToken).ConfigureAwait(false);
        return Wrap("describe_table", correlationId, payload, cacheHit);
    }

    public async Task<ToolEnvelope> DescribeRelationshipsAsync(DescribeRelationshipsRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var (payload, cacheHit) = await _schemaService.DescribeRelationshipsAsync(request.Schema, request.Table, request.Database, cancellationToken).ConfigureAwait(false);
        return Wrap("describe_relationships", correlationId, payload, cacheHit);
    }

    public async Task<ToolEnvelope> SearchSchemaAsync(SearchSchemaRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var (payload, cacheHit) = await _schemaService.SearchSchemaAsync(request.SearchTerm, request.Database, cancellationToken).ConfigureAwait(false);
        return Wrap("search_schema", correlationId, payload, cacheHit);
    }

    public async Task<ToolEnvelope> ReadRecordsAsync(ReadRecordsRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var sql = BuildReadSql(request);
        var parameters = BuildParameters(request.Filters);
        var (payload, cacheHit) = await _queryExecutor.ExecuteQueryAsync(sql, parameters, request.Database, cancellationToken).ConfigureAwait(false);
        return Wrap("read_records", correlationId, payload, cacheHit);
    }

    public async Task<ToolEnvelope> ExecuteSqlAsync(ExecuteSqlRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var (response, cacheHit) = await _queryExecutor.ExecuteQueryAsync(request.Sql, request.Parameters, request.Database, cancellationToken).ConfigureAwait(false);
        return Wrap("execute_sql", correlationId, response, cacheHit);
    }

    public async Task<ToolEnvelope> CreateRecordAsync(CreateRecordRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var (sql, parameters) = BuildInsertSql(request);
        var payload = await _queryExecutor.ExecuteNonQueryAsync(sql, parameters, request.Database, allowAdmin: false, cancellationToken).ConfigureAwait(false);
        await AuditAsync(correlationId, "create_record", SqlOperationKind.Write, request.Database, payload.Summary, cancellationToken).ConfigureAwait(false);
        return Wrap("create_record", correlationId, payload, cacheHit: false);
    }

    public async Task<ToolEnvelope> UpdateRecordAsync(UpdateRecordRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var (sql, parameters) = BuildUpdateSql(request);
        var payload = await _queryExecutor.ExecuteNonQueryAsync(sql, parameters, request.Database, allowAdmin: false, cancellationToken).ConfigureAwait(false);
        await AuditAsync(correlationId, "update_record", SqlOperationKind.Write, request.Database, payload.Summary, cancellationToken).ConfigureAwait(false);
        return Wrap("update_record", correlationId, payload, cacheHit: false);
    }

    public async Task<ToolEnvelope> DeleteRecordAsync(DeleteRecordRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var (sql, parameters) = BuildDeleteSql(request);
        var payload = await _queryExecutor.ExecuteNonQueryAsync(sql, parameters, request.Database, allowAdmin: false, cancellationToken).ConfigureAwait(false);
        await AuditAsync(correlationId, "delete_record", SqlOperationKind.Write, request.Database, payload.Summary, cancellationToken).ConfigureAwait(false);
        return Wrap("delete_record", correlationId, payload, cacheHit: false);
    }

    public async Task<ToolEnvelope> ExecuteStoredProcedureAsync(ExecuteStoredProcedureRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var payload = await _queryExecutor.ExecuteStoredProcedureAsync(request.Schema, request.Procedure, request.Parameters, request.Database, cancellationToken).ConfigureAwait(false);
        await AuditAsync(correlationId, "execute_stored_procedure", SqlOperationKind.Write, request.Database, payload.Summary, cancellationToken).ConfigureAwait(false);
        return Wrap("execute_stored_procedure", correlationId, payload, cacheHit: false);
    }

    public async Task<ToolEnvelope> ExplainQueryAsync(ExplainQueryRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var payload = await _queryExecutor.GetPlanAsync(request.Sql, request.Parameters, request.Database, "estimated", cancellationToken).ConfigureAwait(false);
        return Wrap("explain_query", correlationId, payload, cacheHit: false);
    }

    public async Task<ToolEnvelope> GetQueryPlanAsync(GetQueryPlanRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var payload = await _queryExecutor.GetPlanAsync(request.Sql, request.Parameters, request.Database, "actual", cancellationToken).ConfigureAwait(false);
        return Wrap("get_query_plan", correlationId, payload, cacheHit: false);
    }

    public async Task<ToolEnvelope> AnalyzeIndexesAsync(AnalyzeIndexesRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var payload = await _reportingService.AnalyzeIndexesAsync(request.Schema, request.Table, request.Database, cancellationToken).ConfigureAwait(false);
        return Wrap("analyze_indexes", correlationId, payload, cacheHit: false);
    }

    public async Task<ToolEnvelope> DatabaseHealthReportAsync(DatabaseHealthReportRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var payload = await _reportingService.GetDatabaseHealthReportAsync(request.Database, cancellationToken).ConfigureAwait(false);
        return Wrap("database_health_report", correlationId, payload, cacheHit: false);
    }

    public async Task<ToolEnvelope> QueryPerformanceReportAsync(QueryPerformanceReportRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var payload = await _reportingService.GetQueryPerformanceReportAsync(request.Top, request.Database, cancellationToken).ConfigureAwait(false);
        return Wrap("query_performance_report", correlationId, payload, cacheHit: false);
    }

    public async Task<ToolEnvelope> TableStatisticsReportAsync(TableStatisticsReportRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var payload = await _reportingService.GetTableStatisticsReportAsync(request.Schema, request.Table, request.Database, cancellationToken).ConfigureAwait(false);
        return Wrap("table_statistics_report", correlationId, payload, cacheHit: false);
    }

    public async Task<ToolEnvelope> CreateIndexAsync(CreateIndexRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var payload = await _adminService.CreateIndexAsync(request, cancellationToken).ConfigureAwait(false);
        await AuditAsync(correlationId, "create_index", SqlOperationKind.Admin, request.Database, payload.Summary, cancellationToken).ConfigureAwait(false);
        return Wrap("create_index", correlationId, payload, cacheHit: false);
    }

    public async Task<ToolEnvelope> RebuildIndexAsync(RebuildIndexRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var payload = await _adminService.RebuildIndexAsync(request, cancellationToken).ConfigureAwait(false);
        await AuditAsync(correlationId, "rebuild_index", SqlOperationKind.Admin, request.Database, payload.Summary, cancellationToken).ConfigureAwait(false);
        return Wrap("rebuild_index", correlationId, payload, cacheHit: false);
    }

    public async Task<ToolEnvelope> UpdateStatisticsAsync(UpdateStatisticsRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var payload = await _adminService.UpdateStatisticsAsync(request, cancellationToken).ConfigureAwait(false);
        await AuditAsync(correlationId, "update_statistics", SqlOperationKind.Admin, request.Database, payload.Summary, cancellationToken).ConfigureAwait(false);
        return Wrap("update_statistics", correlationId, payload, cacheHit: false);
    }

    public async Task<ToolEnvelope> RunMaintenanceTaskAsync(RunMaintenanceTaskRequest request, CancellationToken cancellationToken)
    {
        var correlationId = NewCorrelationId();
        var payload = await _adminService.RunMaintenanceTaskAsync(request, cancellationToken).ConfigureAwait(false);
        await AuditAsync(correlationId, "run_maintenance_task", SqlOperationKind.Admin, request.Database, payload.Summary, cancellationToken).ConfigureAwait(false);
        return Wrap("run_maintenance_task", correlationId, payload, cacheHit: false);
    }

    private ToolEnvelope Wrap(string tool, string correlationId, object payload, bool cacheHit)
    {
        return new ToolEnvelope(tool, _options.CurrentValue.CapabilityProfile, cacheHit, correlationId, payload);
    }

    private async Task AuditAsync(string correlationId, string tool, SqlOperationKind operationKind, string? database, string summary, CancellationToken cancellationToken)
    {
        await _auditSink.WriteAsync(correlationId, tool, operationKind, database ?? "<default>", summary, cancellationToken).ConfigureAwait(false);
    }

    private static string NewCorrelationId() => Guid.NewGuid().ToString("N");

    private static Dictionary<string, JsonElement>? BuildParameters(Dictionary<string, JsonElement>? values)
    {
        return values is null || values.Count == 0 ? null : new Dictionary<string, JsonElement>(values, StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildReadSql(ReadRecordsRequest request)
    {
        var columns = request.Columns is { Length: > 0 }
            ? string.Join(", ", request.Columns.Select(IdentifierGuard.Quote))
            : "*";

        var builder = new StringBuilder();
        builder.Append("SELECT ");
        builder.Append(columns);
        builder.Append(" FROM ");
        builder.Append(IdentifierGuard.QuoteMultipart(request.Schema, request.Table));

        if (request.Filters is { Count: > 0 })
        {
            builder.Append(" WHERE ");
            builder.Append(string.Join(" AND ", request.Filters.Keys.Select((key, index) => $"{IdentifierGuard.Quote(key)} = @p{index}")));
        }

        if (!string.IsNullOrWhiteSpace(request.OrderBy))
        {
            builder.Append(" ORDER BY ");
            builder.Append(request.OrderBy);
        }

        var limit = Math.Max(request.Limit ?? 100, 1);
        builder.Append($" OFFSET {Math.Max(request.Offset ?? 0, 0)} ROWS FETCH NEXT {limit} ROWS ONLY;");
        return builder.ToString();
    }

    private static (string Sql, Dictionary<string, JsonElement> Parameters) BuildInsertSql(CreateRecordRequest request)
    {
        if (request.Values.Count is 0)
        {
            throw new ModelContextProtocol.McpException("CreateRecord requires at least one value.");
        }

        var columns = request.Values.Keys.Select(IdentifierGuard.Quote).ToArray();
        var parameterNames = request.Values.Keys.Select((_, index) => $"@p{index}").ToArray();
        var sql = $"""
            INSERT INTO {IdentifierGuard.QuoteMultipart(request.Schema, request.Table)} ({string.Join(", ", columns)})
            VALUES ({string.Join(", ", parameterNames)});
            """;

        return (sql, Rebind(request.Values));
    }

    private static (string Sql, Dictionary<string, JsonElement> Parameters) BuildUpdateSql(UpdateRecordRequest request)
    {
        if (request.Values.Count is 0 || request.Filters.Count is 0)
        {
            throw new ModelContextProtocol.McpException("UpdateRecord requires values and filters.");
        }

        var sqlBuilder = new StringBuilder();
        var parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        sqlBuilder.Append($"UPDATE {IdentifierGuard.QuoteMultipart(request.Schema, request.Table)} SET ");
        sqlBuilder.Append(string.Join(", ", request.Values.Select(kvp =>
        {
            var name = $"p{index++}";
            parameters[name] = kvp.Value;
            return $"{IdentifierGuard.Quote(kvp.Key)} = @{name}";
        })));
        sqlBuilder.Append(" WHERE ");
        sqlBuilder.Append(string.Join(" AND ", request.Filters.Select(kvp =>
        {
            var name = $"p{index++}";
            parameters[name] = kvp.Value;
            return $"{IdentifierGuard.Quote(kvp.Key)} = @{name}";
        })));
        sqlBuilder.Append(';');
        return (sqlBuilder.ToString(), parameters);
    }

    private static (string Sql, Dictionary<string, JsonElement> Parameters) BuildDeleteSql(DeleteRecordRequest request)
    {
        if (request.Filters.Count is 0)
        {
            throw new ModelContextProtocol.McpException("DeleteRecord requires at least one filter.");
        }

        var parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var conditions = request.Filters.Select((kvp, index) =>
        {
            var name = $"p{index}";
            parameters[name] = kvp.Value;
            return $"{IdentifierGuard.Quote(kvp.Key)} = @{name}";
        });

        var sql = $"""
            DELETE FROM {IdentifierGuard.QuoteMultipart(request.Schema, request.Table)}
            WHERE {string.Join(" AND ", conditions)};
            """;

        return (sql, parameters);
    }

    private static Dictionary<string, JsonElement> Rebind(Dictionary<string, JsonElement> values)
    {
        var parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var value in values.Values)
        {
            parameters[$"p{index++}"] = value;
        }

        return parameters;
    }
}
