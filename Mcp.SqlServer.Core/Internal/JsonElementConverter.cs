using System.Globalization;
using System.Text.Json;

namespace Mcp.SqlServer.Core.Internal;

internal static class JsonElementConverter
{
    public static object? ToValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String when element.TryGetDateTimeOffset(out var dto) => dto,
            JsonValueKind.String when element.TryGetDateTime(out var dt) => dt,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var i) => i,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number when element.TryGetDecimal(out var d) => d,
            JsonValueKind.Number => double.Parse(element.GetRawText(), CultureInfo.InvariantCulture),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => DBNull.Value,
            JsonValueKind.Undefined => DBNull.Value,
            _ => element.GetRawText()
        };
    }
}
