namespace Mcp.SqlServer.Core.Abstractions;

public sealed record ToolEnvelope(
    string Tool,
    CapabilityProfile CapabilityProfile,
    bool CacheHit,
    string CorrelationId,
    object Payload);

public sealed record SqlQueryResponse(
    string Database,
    int RowCount,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    string? Sql = null);

public sealed record SqlWriteResponse(
    string Database,
    int RowsAffected,
    string Summary);

public sealed record SchemaSearchMatch(
    string Database,
    string Schema,
    string ObjectName,
    string ObjectType,
    string? ColumnName);

public sealed record TableColumnInfo(
    string Name,
    string DataType,
    bool IsNullable,
    int? MaxLength,
    int OrdinalPosition,
    bool IsPrimaryKey);

public sealed record RelationshipInfo(
    string ForeignKeyName,
    string ParentSchema,
    string ParentTable,
    string ParentColumn,
    string ReferencedSchema,
    string ReferencedTable,
    string ReferencedColumn);

public sealed record TableDescription(
    string Database,
    string Schema,
    string Table,
    IReadOnlyList<TableColumnInfo> Columns,
    IReadOnlyList<RelationshipInfo> Relationships);

public sealed record QueryPlanResponse(
    string Database,
    string PlanType,
    string PlanXml,
    string Sql);

public sealed record ReportResponse(
    string ReportName,
    string Database,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);
