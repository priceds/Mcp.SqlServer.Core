using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mcp.SqlServer.Core.Caching;

internal static class CacheKeyBuilder
{
    public static string ForMetadata(string database, string scope)
    {
        return $"metadata:{database}:{scope}";
    }

    public static string ForQuery(string database, string normalizedSql, IReadOnlyDictionary<string, JsonElement>? parameters, string securityContext)
    {
        return $"query:{database}:{Hash(normalizedSql)}:{Hash(parameters)}:{securityContext}";
    }

    public static string ForPlan(string database, string normalizedSql)
    {
        return $"plan:{database}:{Hash(normalizedSql)}";
    }

    public static string ForReport(string database, string reportName, string scope)
    {
        return $"report:{database}:{reportName}:{Hash(scope)}";
    }

    private static string Hash(object? value)
    {
        var serialized = value switch
        {
            null => "<null>",
            string text => text,
            IReadOnlyDictionary<string, JsonElement> parameters => SerializeParameters(parameters),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.ToString() ?? "<null>"
        };

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(serialized));
        return Convert.ToHexString(bytes);
    }

    private static string SerializeParameters(IReadOnlyDictionary<string, JsonElement> parameters)
    {
        if (parameters.Count is 0)
        {
            return "<empty>";
        }

        var builder = new StringBuilder();
        foreach (var pair in parameters.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            builder
                .Append(pair.Key)
                .Append('=')
                .Append(pair.Value.GetRawText())
                .Append(';');
        }

        return builder.ToString();
    }
}
