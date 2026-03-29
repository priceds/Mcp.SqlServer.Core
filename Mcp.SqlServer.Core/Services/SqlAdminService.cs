using System.Text.Json;
using Mcp.SqlServer.Core.Abstractions;
using Mcp.SqlServer.Core.Internal;

namespace Mcp.SqlServer.Core.Services;

internal sealed class SqlAdminService
{
    private readonly SqlQueryExecutor _executor;

    public SqlAdminService(SqlQueryExecutor executor)
    {
        _executor = executor;
    }

    public Task<SqlWriteResponse> CreateIndexAsync(CreateIndexRequest request, CancellationToken cancellationToken)
    {
        var columns = string.Join(", ", request.Columns.Select(IdentifierGuard.Quote));
        var unique = request.Unique ? "UNIQUE " : string.Empty;
        var sql = $"""
            CREATE {unique}INDEX {IdentifierGuard.Quote(request.IndexName)}
            ON {IdentifierGuard.QuoteMultipart(request.Schema, request.Table)} ({columns});
            """;

        return _executor.ExecuteNonQueryAsync(sql, null, request.Database, allowAdmin: true, cancellationToken);
    }

    public Task<SqlWriteResponse> RebuildIndexAsync(RebuildIndexRequest request, CancellationToken cancellationToken)
    {
        var sql = $"""
            ALTER INDEX {IdentifierGuard.Quote(request.IndexName)}
            ON {IdentifierGuard.QuoteMultipart(request.Schema, request.Table)}
            REBUILD;
            """;

        return _executor.ExecuteNonQueryAsync(sql, null, request.Database, allowAdmin: true, cancellationToken);
    }

    public Task<SqlWriteResponse> UpdateStatisticsAsync(UpdateStatisticsRequest request, CancellationToken cancellationToken)
    {
        var sql = $"UPDATE STATISTICS {IdentifierGuard.QuoteMultipart(request.Schema, request.Table)};";
        return _executor.ExecuteNonQueryAsync(sql, null, request.Database, allowAdmin: true, cancellationToken);
    }

    public Task<SqlWriteResponse> RunMaintenanceTaskAsync(RunMaintenanceTaskRequest request, CancellationToken cancellationToken)
    {
        var sql = request.TaskName.ToLowerInvariant() switch
        {
            "index_reorganize" when request.Schema is not null && request.Table is not null =>
                $"ALTER INDEX ALL ON {IdentifierGuard.QuoteMultipart(request.Schema, request.Table)} REORGANIZE;",
            "update_statistics" when request.Schema is not null && request.Table is not null =>
                $"UPDATE STATISTICS {IdentifierGuard.QuoteMultipart(request.Schema, request.Table)};",
            "checkdb" => "DBCC CHECKDB WITH NO_INFOMSGS;",
            _ => throw new ModelContextProtocol.McpException("Unsupported maintenance task or missing schema/table target.")
        };

        return _executor.ExecuteNonQueryAsync(sql, null, request.Database, allowAdmin: true, cancellationToken);
    }
}
