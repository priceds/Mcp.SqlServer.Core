using Dapper;
using Microsoft.Extensions.Options;
using Mcp.SqlServer.Core.Abstractions;
using Mcp.SqlServer.Core.Caching;
using Mcp.SqlServer.Core.Internal;

namespace Mcp.SqlServer.Core.Services;

internal sealed class SqlReportingService
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly ISqlServerCache _cache;
    private readonly IOptionsMonitor<SqlServerMcpOptions> _options;

    public SqlReportingService(SqlConnectionFactory connectionFactory, ISqlServerCache cache, IOptionsMonitor<SqlServerMcpOptions> options)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
        _options = options;
    }

    public Task<ReportResponse> GetDatabaseHealthReportAsync(string? database, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                DB_NAME() AS DatabaseName,
                SUM(size) * 8 / 1024 AS TotalSizeMb,
                SUM(CASE WHEN type_desc = 'ROWS' THEN size ELSE 0 END) * 8 / 1024 AS DataSizeMb,
                SUM(CASE WHEN type_desc = 'LOG' THEN size ELSE 0 END) * 8 / 1024 AS LogSizeMb
            FROM sys.database_files;
            """;

        return RunReportAsync("database_health_report", sql, database, cancellationToken);
    }

    public Task<ReportResponse> GetQueryPerformanceReportAsync(int? top, string? database, CancellationToken cancellationToken)
    {
        var safeTop = Math.Clamp(top ?? 20, 1, 100);
        var sql = $"""
            SELECT TOP ({safeTop})
                qs.execution_count,
                qs.total_elapsed_time / 1000 AS total_elapsed_ms,
                qs.total_worker_time / 1000 AS total_cpu_ms,
                qs.total_logical_reads,
                SUBSTRING(st.text, (qs.statement_start_offset/2) + 1,
                    ((CASE qs.statement_end_offset WHEN -1 THEN DATALENGTH(st.text) ELSE qs.statement_end_offset END - qs.statement_start_offset)/2) + 1) AS statement_text
            FROM sys.dm_exec_query_stats qs
            CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
            ORDER BY qs.total_elapsed_time DESC;
            """;

        return RunReportAsync("query_performance_report", sql, database, cancellationToken);
    }

    public Task<ReportResponse> GetTableStatisticsReportAsync(string? schema, string? table, string? database, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                s.name AS schema_name,
                t.name AS table_name,
                SUM(p.rows) AS row_count
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.partitions p ON t.object_id = p.object_id
            WHERE p.index_id IN (0,1)
              AND (@schema IS NULL OR s.name = @schema)
              AND (@table IS NULL OR t.name = @table)
            GROUP BY s.name, t.name
            ORDER BY row_count DESC, s.name, t.name;
            """;

        return RunReportAsync("table_statistics_report", sql, database, cancellationToken, ("@schema", schema), ("@table", table));
    }

    public Task<ReportResponse> AnalyzeIndexesAsync(string? schema, string? table, string? database, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                s.name AS schema_name,
                t.name AS table_name,
                i.name AS index_name,
                i.type_desc,
                i.is_unique,
                ISNULL(us.user_seeks, 0) AS user_seeks,
                ISNULL(us.user_scans, 0) AS user_scans,
                ISNULL(us.user_lookups, 0) AS user_lookups,
                ISNULL(us.user_updates, 0) AS user_updates
            FROM sys.indexes i
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            LEFT JOIN sys.dm_db_index_usage_stats us
                ON us.object_id = i.object_id
               AND us.index_id = i.index_id
               AND us.database_id = DB_ID()
            WHERE i.index_id > 0
              AND (@schema IS NULL OR s.name = @schema)
              AND (@table IS NULL OR t.name = @table)
            ORDER BY s.name, t.name, i.name;
            """;

        return RunReportAsync("analyze_indexes", sql, database, cancellationToken, ("@schema", schema), ("@table", table));
    }

    private async Task<ReportResponse> RunReportAsync(
        string reportName,
        string sql,
        string? database,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        var actualDatabase = database ?? "default";
        var scope = string.Join("|", parameters.Select(parameter => $"{parameter.Name}={parameter.Value}"));
        var cacheKey = CacheKeyBuilder.ForReport(actualDatabase, reportName, scope);
        var cached = await _cache.TryGetAsync<ReportResponse>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached.Found && cached.Value is not null)
        {
            return cached.Value;
        }

        await using var lease = await _connectionFactory.OpenAsync(database, cancellationToken).ConfigureAwait(false);
        var command = DapperExtensions.Command(
            sql,
            DapperExtensions.ToDynamicParameters(parameters),
            _options.CurrentValue.Safety.ExpensiveCommandTimeout,
            cancellationToken);
        var queryRows = await lease.Connection.QueryAsync(command).ConfigureAwait(false);
        var rows = DapperExtensions.ToDictionaryRows(queryRows);

        var response = new ReportResponse(reportName, lease.Database, DateTimeOffset.UtcNow, rows);
        await _cache.SetAsync(cacheKey, response, _options.CurrentValue.CachePolicy.ReportSnapshotTtl, cancellationToken).ConfigureAwait(false);
        return response;
    }
}
