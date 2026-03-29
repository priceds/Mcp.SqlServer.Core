namespace Mcp.SqlServer.Core.Abstractions;

public interface ISqlServerMcpAuditSink
{
    Task WriteAsync(
        string correlationId,
        string toolName,
        SqlOperationKind operationKind,
        string database,
        string summary,
        CancellationToken cancellationToken);
}
