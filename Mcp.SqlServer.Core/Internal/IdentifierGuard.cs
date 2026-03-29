using System.Text.RegularExpressions;
using ModelContextProtocol;

namespace Mcp.SqlServer.Core.Internal;

internal static partial class IdentifierGuard
{
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled)]
    private static partial Regex SafeIdentifierRegex();

    public static string Quote(string value)
    {
        if (!SafeIdentifierRegex().IsMatch(value))
        {
            throw new McpException($"Unsafe SQL identifier '{value}'.");
        }

        return $"[{value}]";
    }

    public static string QuoteMultipart(params string?[] parts)
    {
        return string.Join(".", parts.Where(static part => !string.IsNullOrWhiteSpace(part)).Select(Quote));
    }
}
