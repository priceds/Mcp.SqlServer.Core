namespace Mcp.SqlServer.Core.Abstractions;

public interface ISqlServerCache
{
    ValueTask<(bool Found, T? Value)> TryGetAsync<T>(string key, CancellationToken cancellationToken);

    ValueTask SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken);
}
