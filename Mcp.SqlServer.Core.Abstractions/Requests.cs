using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcp.SqlServer.Core.Abstractions;

public sealed record ListSchemasRequest(
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record ListTablesRequest(
    [property: Description("Optional database name override.")] string? Database = null,
    [property: Description("Optional schema name filter.")] string? Schema = null);

public sealed record DescribeTableRequest(
    [property: Description("Table schema name.")] string Schema,
    [property: Description("Table name.")] string Table,
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record DescribeRelationshipsRequest(
    [property: Description("Optional schema name filter.")] string? Schema = null,
    [property: Description("Optional table name filter.")] string? Table = null,
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record SearchSchemaRequest(
    [property: Description("Search term for schema, table, or column names.")] string SearchTerm,
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record ReadRecordsRequest(
    [property: Description("Table schema name.")] string Schema,
    [property: Description("Table name.")] string Table,
    [property: Description("Optional list of columns to return.")] string[]? Columns = null,
    [property: Description("Optional dictionary of equality filters.")] Dictionary<string, JsonElement>? Filters = null,
    [property: Description("Optional ORDER BY expression using validated identifiers only.")] string? OrderBy = null,
    [property: Description("Maximum rows to return.")] int? Limit = null,
    [property: Description("Zero-based row offset.")] int? Offset = null,
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record ExecuteSqlRequest(
    [property: Description("SQL text to execute.")] string Sql,
    [property: Description("Optional parameter values keyed by name.")] Dictionary<string, JsonElement>? Parameters = null,
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record CreateRecordRequest(
    [property: Description("Table schema name.")] string Schema,
    [property: Description("Table name.")] string Table,
    [property: Description("Column/value payload to insert.")] Dictionary<string, JsonElement> Values,
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record UpdateRecordRequest(
    [property: Description("Table schema name.")] string Schema,
    [property: Description("Table name.")] string Table,
    [property: Description("Column/value payload to update.")] Dictionary<string, JsonElement> Values,
    [property: Description("Equality filter payload identifying rows to update.")] Dictionary<string, JsonElement> Filters,
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record DeleteRecordRequest(
    [property: Description("Table schema name.")] string Schema,
    [property: Description("Table name.")] string Table,
    [property: Description("Equality filter payload identifying rows to delete.")] Dictionary<string, JsonElement> Filters,
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record ExecuteStoredProcedureRequest(
    [property: Description("Procedure schema name.")] string Schema,
    [property: Description("Procedure name.")] string Procedure,
    [property: Description("Optional input parameter values keyed by name.")] Dictionary<string, JsonElement>? Parameters = null,
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record ExplainQueryRequest(
    [property: Description("T-SQL text to explain.")] string Sql,
    [property: Description("Optional parameter values keyed by name.")] Dictionary<string, JsonElement>? Parameters = null,
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record GetQueryPlanRequest(
    [property: Description("T-SQL text to capture an execution plan for.")] string Sql,
    [property: Description("Optional parameter values keyed by name.")] Dictionary<string, JsonElement>? Parameters = null,
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record AnalyzeIndexesRequest(
    [property: Description("Optional schema name filter.")] string? Schema = null,
    [property: Description("Optional table name filter.")] string? Table = null,
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record DatabaseHealthReportRequest(
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record QueryPerformanceReportRequest(
    [property: Description("Maximum number of query rows to return.")] int? Top = null,
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record TableStatisticsReportRequest(
    [property: Description("Optional schema name filter.")] string? Schema = null,
    [property: Description("Optional table name filter.")] string? Table = null,
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record CreateIndexRequest(
    [property: Description("Table schema name.")] string Schema,
    [property: Description("Table name.")] string Table,
    [property: Description("Index name to create.")] string IndexName,
    [property: Description("Columns to include in the index key.")] string[] Columns,
    [property: Description("Whether to create the index as unique.")] bool Unique = false,
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record RebuildIndexRequest(
    [property: Description("Table schema name.")] string Schema,
    [property: Description("Table name.")] string Table,
    [property: Description("Index name to rebuild.")] string IndexName,
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record UpdateStatisticsRequest(
    [property: Description("Table schema name.")] string Schema,
    [property: Description("Table name.")] string Table,
    [property: Description("Optional database name override.")] string? Database = null);

public sealed record RunMaintenanceTaskRequest(
    [property: Description("Task name: index_reorganize, update_statistics, or checkdb.")] string TaskName,
    [property: Description("Optional database name override.")] string? Database = null,
    [property: Description("Optional schema name filter for targeted maintenance.")] string? Schema = null,
    [property: Description("Optional table name filter for targeted maintenance.")] string? Table = null);
