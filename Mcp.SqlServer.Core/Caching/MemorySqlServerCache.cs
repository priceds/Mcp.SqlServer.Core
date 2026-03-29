using Microsoft.Extensions.Caching.Memory;
using Mcp.SqlServer.Core.Abstractions;

namespace Mcp.SqlServer.Core.Caching;

internal sealed class MemorySqlServerCache : ISqlServerCache
{
    private readonly IMemoryCache _memoryCache;

    public MemorySqlServerCache(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public ValueTask<(bool Found, T? Value)> TryGetAsync<T>(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _memoryCache.TryGetValue(key, out T? value)
            ? ValueTask.FromResult<(bool, T?)>((true, value))
            : ValueTask.FromResult<(bool, T?)>((false, default));
    }

    public ValueTask SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _memoryCache.Set(key, value, ttl);
        return ValueTask.CompletedTask;
    }
}
