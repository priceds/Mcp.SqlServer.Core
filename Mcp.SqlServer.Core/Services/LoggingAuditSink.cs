using Microsoft.Extensions.Logging;
using Mcp.SqlServer.Core.Abstractions;

namespace Mcp.SqlServer.Core.Services;

internal sealed class LoggingAuditSink : ISqlServerMcpAuditSink
{
    private readonly ILogger<LoggingAuditSink> _logger;

    public LoggingAuditSink(ILogger<LoggingAuditSink> logger)
    {
        _logger = logger;
    }

    public Task WriteAsync(string correlationId, string toolName, SqlOperationKind operationKind, string database, string summary, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(
            "SQL MCP audit {CorrelationId} {ToolName} {OperationKind} {Database} {Summary}",
            correlationId,
            toolName,
            operationKind,
            database,
            summary);

        return Task.CompletedTask;
    }
}
