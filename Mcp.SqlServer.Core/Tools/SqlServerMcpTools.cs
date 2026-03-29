using System.ComponentModel;
using Mcp.SqlServer.Core.Abstractions;
using Mcp.SqlServer.Core.Services;
using ModelContextProtocol.Server;

namespace Mcp.SqlServer.Core.Tools;

[McpServerToolType]
internal sealed class SqlServerMcpTools
{
    private readonly SqlServerMcpToolService _service;

    public SqlServerMcpTools(SqlServerMcpToolService service)
    {
        _service = service;
    }

    [McpServerTool(Name = "list_databases", Title = "List Databases", ReadOnly = true, Idempotent = true)]
    [Description("Lists online databases that the MCP server can access.")]
    public Task<ToolEnvelope> ListDatabases(CancellationToken cancellationToken) => _service.ListDatabasesAsync(cancellationToken);

    [McpServerTool(Name = "list_schemas", Title = "List Schemas", ReadOnly = true, Idempotent = true)]
    [Description("Lists schemas for the selected or default database.")]
    public Task<ToolEnvelope> ListSchemas(ListSchemasRequest request, CancellationToken cancellationToken) => _service.ListSchemasAsync(request, cancellationToken);

    [McpServerTool(Name = "list_tables", Title = "List Tables", ReadOnly = true, Idempotent = true)]
    [Description("Lists tables and views with an optional schema filter.")]
    public Task<ToolEnvelope> ListTables(ListTablesRequest request, CancellationToken cancellationToken) => _service.ListTablesAsync(request, cancellationToken);

    [McpServerTool(Name = "describe_table", Title = "Describe Table", ReadOnly = true, Idempotent = true)]
    [Description("Returns column, key, and relationship metadata for a specific table.")]
    public Task<ToolEnvelope> DescribeTable(DescribeTableRequest request, CancellationToken cancellationToken) => _service.DescribeTableAsync(request, cancellationToken);

    [McpServerTool(Name = "describe_relationships", Title = "Describe Relationships", ReadOnly = true, Idempotent = true)]
    [Description("Lists foreign-key relationships for a database or a specific table.")]
    public Task<ToolEnvelope> DescribeRelationships(DescribeRelationshipsRequest request, CancellationToken cancellationToken) => _service.DescribeRelationshipsAsync(request, cancellationToken);

    [McpServerTool(Name = "search_schema", Title = "Search Schema", ReadOnly = true, Idempotent = true)]
    [Description("Searches tables, schemas, and columns by a free-text term.")]
    public Task<ToolEnvelope> SearchSchema(SearchSchemaRequest request, CancellationToken cancellationToken) => _service.SearchSchemaAsync(request, cancellationToken);

    [McpServerTool(Name = "read_records", Title = "Read Records", ReadOnly = true, Idempotent = true)]
    [Description("Reads rows from a table using validated identifiers, filters, ordering, and paging.")]
    public Task<ToolEnvelope> ReadRecords(ReadRecordsRequest request, CancellationToken cancellationToken) => _service.ReadRecordsAsync(request, cancellationToken);

    [McpServerTool(Name = "execute_sql", Title = "Execute SQL", ReadOnly = true)]
    [Description("Executes SQL subject to capability and safety validation. Read queries are recommended.")]
    public Task<ToolEnvelope> ExecuteSql(ExecuteSqlRequest request, CancellationToken cancellationToken) => _service.ExecuteSqlAsync(request, cancellationToken);

    [McpServerTool(Name = "create_record", Title = "Create Record", Destructive = true)]
    [Description("Inserts a row into a target table using a column/value payload.")]
    public Task<ToolEnvelope> CreateRecord(CreateRecordRequest request, CancellationToken cancellationToken) => _service.CreateRecordAsync(request, cancellationToken);

    [McpServerTool(Name = "update_record", Title = "Update Record", Destructive = true)]
    [Description("Updates rows in a target table using value and filter dictionaries.")]
    public Task<ToolEnvelope> UpdateRecord(UpdateRecordRequest request, CancellationToken cancellationToken) => _service.UpdateRecordAsync(request, cancellationToken);

    [McpServerTool(Name = "delete_record", Title = "Delete Record", Destructive = true)]
    [Description("Deletes rows in a target table using filter dictionaries.")]
    public Task<ToolEnvelope> DeleteRecord(DeleteRecordRequest request, CancellationToken cancellationToken) => _service.DeleteRecordAsync(request, cancellationToken);

    [McpServerTool(Name = "execute_stored_procedure", Title = "Execute Stored Procedure", Destructive = true)]
    [Description("Executes a stored procedure when write mode and procedure execution are enabled.")]
    public Task<ToolEnvelope> ExecuteStoredProcedure(ExecuteStoredProcedureRequest request, CancellationToken cancellationToken) => _service.ExecuteStoredProcedureAsync(request, cancellationToken);

    [McpServerTool(Name = "explain_query", Title = "Explain Query", ReadOnly = true, Idempotent = true)]
    [Description("Returns an estimated XML execution plan without running the query.")]
    public Task<ToolEnvelope> ExplainQuery(ExplainQueryRequest request, CancellationToken cancellationToken) => _service.ExplainQueryAsync(request, cancellationToken);

    [McpServerTool(Name = "get_query_plan", Title = "Get Query Plan", ReadOnly = true)]
    [Description("Returns an execution plan payload for a query, with plan caching applied.")]
    public Task<ToolEnvelope> GetQueryPlan(GetQueryPlanRequest request, CancellationToken cancellationToken) => _service.GetQueryPlanAsync(request, cancellationToken);

    [McpServerTool(Name = "analyze_indexes", Title = "Analyze Indexes", ReadOnly = true, Idempotent = true)]
    [Description("Returns index usage and shape information for performance review.")]
    public Task<ToolEnvelope> AnalyzeIndexes(AnalyzeIndexesRequest request, CancellationToken cancellationToken) => _service.AnalyzeIndexesAsync(request, cancellationToken);

    [McpServerTool(Name = "database_health_report", Title = "Database Health Report", ReadOnly = true, Idempotent = true)]
    [Description("Returns storage and file sizing signals for the current database.")]
    public Task<ToolEnvelope> DatabaseHealthReport(DatabaseHealthReportRequest request, CancellationToken cancellationToken) => _service.DatabaseHealthReportAsync(request, cancellationToken);

    [McpServerTool(Name = "query_performance_report", Title = "Query Performance Report", ReadOnly = true, Idempotent = true)]
    [Description("Returns high-cost query statistics from DMVs for performance tuning.")]
    public Task<ToolEnvelope> QueryPerformanceReport(QueryPerformanceReportRequest request, CancellationToken cancellationToken) => _service.QueryPerformanceReportAsync(request, cancellationToken);

    [McpServerTool(Name = "table_statistics_report", Title = "Table Statistics Report", ReadOnly = true, Idempotent = true)]
    [Description("Returns table row-count and statistics-oriented reporting signals.")]
    public Task<ToolEnvelope> TableStatisticsReport(TableStatisticsReportRequest request, CancellationToken cancellationToken) => _service.TableStatisticsReportAsync(request, cancellationToken);

    [McpServerTool(Name = "create_index", Title = "Create Index", Destructive = true)]
    [Description("Creates an index when admin tools are explicitly enabled.")]
    public Task<ToolEnvelope> CreateIndex(CreateIndexRequest request, CancellationToken cancellationToken) => _service.CreateIndexAsync(request, cancellationToken);

    [McpServerTool(Name = "rebuild_index", Title = "Rebuild Index", Destructive = true)]
    [Description("Rebuilds an existing index when admin tools are explicitly enabled.")]
    public Task<ToolEnvelope> RebuildIndex(RebuildIndexRequest request, CancellationToken cancellationToken) => _service.RebuildIndexAsync(request, cancellationToken);

    [McpServerTool(Name = "update_statistics", Title = "Update Statistics", Destructive = true)]
    [Description("Updates table statistics when admin tools are explicitly enabled.")]
    public Task<ToolEnvelope> UpdateStatistics(UpdateStatisticsRequest request, CancellationToken cancellationToken) => _service.UpdateStatisticsAsync(request, cancellationToken);

    [McpServerTool(Name = "run_maintenance_task", Title = "Run Maintenance Task", Destructive = true)]
    [Description("Runs a targeted maintenance task such as reorganize, update_statistics, or checkdb.")]
    public Task<ToolEnvelope> RunMaintenanceTask(RunMaintenanceTaskRequest request, CancellationToken cancellationToken) => _service.RunMaintenanceTaskAsync(request, cancellationToken);
}
