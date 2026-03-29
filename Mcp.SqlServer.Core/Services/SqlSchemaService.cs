using Microsoft.Extensions.Options;
using Mcp.SqlServer.Core.Abstractions;
using Mcp.SqlServer.Core.Caching;

namespace Mcp.SqlServer.Core.Services;

internal sealed class SqlSchemaService
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly ISqlServerCache _cache;
    private readonly IOptionsMonitor<SqlServerMcpOptions> _options;

    public SqlSchemaService(SqlConnectionFactory connectionFactory, ISqlServerCache cache, IOptionsMonitor<SqlServerMcpOptions> options)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
        _options = options;
    }

    public async Task<(IReadOnlyList<string> Databases, bool CacheHit)> ListDatabasesAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "metadata:instance:databases";
        var cached = await _cache.TryGetAsync<IReadOnlyList<string>>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached.Found && cached.Value is not null)
        {
            return (cached.Value, true);
        }

        const string sql = """
            SELECT name
            FROM sys.databases
            WHERE state_desc = 'ONLINE'
            ORDER BY name;
            """;

        await using var lease = await _connectionFactory.OpenAsync(null, cancellationToken).ConfigureAwait(false);
        await using var command = lease.Connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = (int)_options.CurrentValue.Safety.DefaultCommandTimeout.TotalSeconds;
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(reader.GetString(0));
        }

        await _cache.SetAsync(cacheKey, rows, _options.CurrentValue.CachePolicy.MetadataTtl, cancellationToken).ConfigureAwait(false);
        return (rows, false);
    }

    public async Task<(IReadOnlyList<string> Schemas, bool CacheHit)> ListSchemasAsync(string? database, CancellationToken cancellationToken)
    {
        var db = await ResolveDatabaseAsync(database, cancellationToken).ConfigureAwait(false);
        var cacheKey = CacheKeyBuilder.ForMetadata(db, "schemas");
        var cached = await _cache.TryGetAsync<IReadOnlyList<string>>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached.Found && cached.Value is not null)
        {
            return (cached.Value, true);
        }

        const string sql = """
            SELECT name
            FROM sys.schemas
            ORDER BY name;
            """;

        var rows = new List<string>();
        await using var lease = await _connectionFactory.OpenAsync(database, cancellationToken).ConfigureAwait(false);
        await using var command = lease.Connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = (int)_options.CurrentValue.Safety.DefaultCommandTimeout.TotalSeconds;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(reader.GetString(0));
        }

        await _cache.SetAsync(cacheKey, rows, _options.CurrentValue.CachePolicy.MetadataTtl, cancellationToken).ConfigureAwait(false);
        return (rows, false);
    }

    public async Task<(IReadOnlyList<IReadOnlyDictionary<string, object?>> Tables, bool CacheHit)> ListTablesAsync(string? database, string? schema, CancellationToken cancellationToken)
    {
        var db = await ResolveDatabaseAsync(database, cancellationToken).ConfigureAwait(false);
        var cacheKey = CacheKeyBuilder.ForMetadata(db, $"tables:{schema ?? "*"}");
        var cached = await _cache.TryGetAsync<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached.Found && cached.Value is not null)
        {
            return (cached.Value, true);
        }

        const string sql = """
            SELECT
                TABLE_SCHEMA AS [Schema],
                TABLE_NAME AS [Table],
                TABLE_TYPE AS [Type]
            FROM INFORMATION_SCHEMA.TABLES
            WHERE (@schema IS NULL OR TABLE_SCHEMA = @schema)
            ORDER BY TABLE_SCHEMA, TABLE_NAME;
            """;

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using var lease = await _connectionFactory.OpenAsync(database, cancellationToken).ConfigureAwait(false);
        await using var command = lease.Connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@schema", (object?)schema ?? DBNull.Value);
        command.CommandTimeout = (int)_options.CurrentValue.Safety.DefaultCommandTimeout.TotalSeconds;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new Dictionary<string, object?>
            {
                ["schema"] = reader.GetString(0),
                ["table"] = reader.GetString(1),
                ["type"] = reader.GetString(2)
            });
        }

        await _cache.SetAsync(cacheKey, rows, _options.CurrentValue.CachePolicy.MetadataTtl, cancellationToken).ConfigureAwait(false);
        return (rows, false);
    }

    public async Task<(TableDescription Description, bool CacheHit)> DescribeTableAsync(string schema, string table, string? database, CancellationToken cancellationToken)
    {
        var db = await ResolveDatabaseAsync(database, cancellationToken).ConfigureAwait(false);
        var cacheKey = CacheKeyBuilder.ForMetadata(db, $"describe:{schema}.{table}");
        var cached = await _cache.TryGetAsync<TableDescription>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached.Found && cached.Value is not null)
        {
            return (cached.Value, true);
        }

        const string columnsSql = """
            SELECT
                c.COLUMN_NAME,
                c.DATA_TYPE,
                CASE WHEN c.IS_NULLABLE = 'YES' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsNullable,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.ORDINAL_POSITION,
                CASE WHEN k.COLUMN_NAME IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS IsPrimaryKey
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE k
                ON c.TABLE_SCHEMA = k.TABLE_SCHEMA
               AND c.TABLE_NAME = k.TABLE_NAME
               AND c.COLUMN_NAME = k.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = @schema
              AND c.TABLE_NAME = @table
            ORDER BY c.ORDINAL_POSITION;
            """;

        const string relationshipsSql = """
            SELECT
                fk.name AS ForeignKeyName,
                ps.name AS ParentSchema,
                pt.name AS ParentTable,
                pc.name AS ParentColumn,
                rs.name AS ReferencedSchema,
                rt.name AS ReferencedTable,
                rc.name AS ReferencedColumn
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            INNER JOIN sys.tables pt ON fkc.parent_object_id = pt.object_id
            INNER JOIN sys.schemas ps ON pt.schema_id = ps.schema_id
            INNER JOIN sys.columns pc ON pc.object_id = pt.object_id AND pc.column_id = fkc.parent_column_id
            INNER JOIN sys.tables rt ON fkc.referenced_object_id = rt.object_id
            INNER JOIN sys.schemas rs ON rt.schema_id = rs.schema_id
            INNER JOIN sys.columns rc ON rc.object_id = rt.object_id AND rc.column_id = fkc.referenced_column_id
            WHERE ps.name = @schema
              AND pt.name = @table
            ORDER BY fk.name;
            """;

        var columns = new List<TableColumnInfo>();
        var relationships = new List<RelationshipInfo>();
        await using var lease = await _connectionFactory.OpenAsync(database, cancellationToken).ConfigureAwait(false);

        await using (var columnCommand = lease.Connection.CreateCommand())
        {
            columnCommand.CommandText = columnsSql;
            columnCommand.Parameters.AddWithValue("@schema", schema);
            columnCommand.Parameters.AddWithValue("@table", table);
            await using var reader = await columnCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                columns.Add(new TableColumnInfo(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetBoolean(2),
                    await reader.IsDBNullAsync(3, cancellationToken).ConfigureAwait(false) ? null : reader.GetInt32(3),
                    reader.GetInt32(4),
                    reader.GetBoolean(5)));
            }
        }

        await using (var relationshipCommand = lease.Connection.CreateCommand())
        {
            relationshipCommand.CommandText = relationshipsSql;
            relationshipCommand.Parameters.AddWithValue("@schema", schema);
            relationshipCommand.Parameters.AddWithValue("@table", table);
            await using var reader = await relationshipCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                relationships.Add(new RelationshipInfo(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6)));
            }
        }

        var description = new TableDescription(db, schema, table, columns, relationships);
        await _cache.SetAsync(cacheKey, description, _options.CurrentValue.CachePolicy.MetadataTtl, cancellationToken).ConfigureAwait(false);
        return (description, false);
    }

    public async Task<(IReadOnlyList<RelationshipInfo> Relationships, bool CacheHit)> DescribeRelationshipsAsync(string? schema, string? table, string? database, CancellationToken cancellationToken)
    {
        var db = await ResolveDatabaseAsync(database, cancellationToken).ConfigureAwait(false);
        var cacheKey = CacheKeyBuilder.ForMetadata(db, $"relationships:{schema}:{table}");
        var cached = await _cache.TryGetAsync<IReadOnlyList<RelationshipInfo>>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached.Found && cached.Value is not null)
        {
            return (cached.Value, true);
        }

        const string sql = """
            SELECT
                fk.name AS ForeignKeyName,
                ps.name AS ParentSchema,
                pt.name AS ParentTable,
                pc.name AS ParentColumn,
                rs.name AS ReferencedSchema,
                rt.name AS ReferencedTable,
                rc.name AS ReferencedColumn
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            INNER JOIN sys.tables pt ON fkc.parent_object_id = pt.object_id
            INNER JOIN sys.schemas ps ON pt.schema_id = ps.schema_id
            INNER JOIN sys.columns pc ON pc.object_id = pt.object_id AND pc.column_id = fkc.parent_column_id
            INNER JOIN sys.tables rt ON fkc.referenced_object_id = rt.object_id
            INNER JOIN sys.schemas rs ON rt.schema_id = rs.schema_id
            INNER JOIN sys.columns rc ON rc.object_id = rt.object_id AND rc.column_id = fkc.referenced_column_id
            WHERE (@schema IS NULL OR ps.name = @schema)
              AND (@table IS NULL OR pt.name = @table)
            ORDER BY ps.name, pt.name, fk.name;
            """;

        var relationships = new List<RelationshipInfo>();
        await using var lease = await _connectionFactory.OpenAsync(database, cancellationToken).ConfigureAwait(false);
        await using var command = lease.Connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@schema", (object?)schema ?? DBNull.Value);
        command.Parameters.AddWithValue("@table", (object?)table ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            relationships.Add(new RelationshipInfo(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6)));
        }

        await _cache.SetAsync(cacheKey, relationships, _options.CurrentValue.CachePolicy.MetadataTtl, cancellationToken).ConfigureAwait(false);
        return (relationships, false);
    }

    public async Task<(IReadOnlyList<SchemaSearchMatch> Matches, bool CacheHit)> SearchSchemaAsync(string searchTerm, string? database, CancellationToken cancellationToken)
    {
        var db = await ResolveDatabaseAsync(database, cancellationToken).ConfigureAwait(false);
        var cacheKey = CacheKeyBuilder.ForMetadata(db, $"search:{searchTerm}");
        var cached = await _cache.TryGetAsync<IReadOnlyList<SchemaSearchMatch>>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached.Found && cached.Value is not null)
        {
            return (cached.Value, true);
        }

        const string sql = """
            SELECT
                DB_NAME() AS DatabaseName,
                c.TABLE_SCHEMA,
                c.TABLE_NAME,
                'TABLE_COLUMN' AS ObjectType,
                c.COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS c
            WHERE c.TABLE_SCHEMA LIKE @term
               OR c.TABLE_NAME LIKE @term
               OR c.COLUMN_NAME LIKE @term
            ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.COLUMN_NAME;
            """;

        var matches = new List<SchemaSearchMatch>();
        await using var lease = await _connectionFactory.OpenAsync(database, cancellationToken).ConfigureAwait(false);
        await using var command = lease.Connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@term", $"%{searchTerm}%");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            matches.Add(new SchemaSearchMatch(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4)));
        }

        await _cache.SetAsync(cacheKey, matches, _options.CurrentValue.CachePolicy.MetadataTtl, cancellationToken).ConfigureAwait(false);
        return (matches, false);
    }

    private async Task<string> ResolveDatabaseAsync(string? database, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(database))
        {
            return database;
        }

        await using var lease = await _connectionFactory.OpenAsync(null, cancellationToken).ConfigureAwait(false);
        return lease.Database;
    }
}
