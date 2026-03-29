using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Mcp.SqlServer.Core.Abstractions;
using Mcp.SqlServer.Core.Security;
using ModelContextProtocol;

namespace Mcp.SqlServer.Core.Services;

internal sealed class SqlConnectionFactory
{
    private readonly IOptionsMonitor<SqlServerMcpOptions> _options;
    private readonly SqlSafetyValidator _validator;

    public SqlConnectionFactory(IOptionsMonitor<SqlServerMcpOptions> options, SqlSafetyValidator validator)
    {
        _options = options;
        _validator = validator;
    }

    public async Task<SqlConnectionLease> OpenAsync(string? database, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new McpException("SqlServerMcp:ConnectionString is required.");
        }

        var builder = _validator.BuildConnectionString(options.ConnectionString, database);
        var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return new SqlConnectionLease(connection, connection.Database);
    }
}

internal sealed class SqlConnectionLease : IAsyncDisposable
{
    public SqlConnectionLease(SqlConnection connection, string database)
    {
        Connection = connection;
        Database = database;
    }

    public SqlConnection Connection { get; }

    public string Database { get; }

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
