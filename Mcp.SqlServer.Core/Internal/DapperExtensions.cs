using System.Data;
using System.Text.Json;
using Dapper;

namespace Mcp.SqlServer.Core.Internal;

internal static class DapperExtensions
{
    public static DynamicParameters ToDynamicParameters(Dictionary<string, JsonElement>? parameters)
    {
        var dynamicParameters = new DynamicParameters();
        if (parameters is null)
        {
            return dynamicParameters;
        }

        foreach (var (name, value) in parameters)
        {
            var parameterName = name.StartsWith('@') ? name : $"@{name}";
            dynamicParameters.Add(parameterName, JsonElementConverter.ToValue(value));
        }

        return dynamicParameters;
    }

    public static DynamicParameters ToDynamicParameters(params (string Name, object? Value)[] parameters)
    {
        var dynamicParameters = new DynamicParameters();
        foreach (var (name, value) in parameters)
        {
            dynamicParameters.Add(name, value);
        }

        return dynamicParameters;
    }

    public static IReadOnlyList<IReadOnlyDictionary<string, object?>> ToDictionaryRows(IEnumerable<dynamic> rows, int? maxRows = null)
    {
        var materialized = new List<IReadOnlyDictionary<string, object?>>();
        var taken = 0;

        foreach (var row in rows)
        {
            if (maxRows.HasValue && taken >= maxRows.Value)
            {
                break;
            }

            if (row is IDictionary<string, object?> typed)
            {
                materialized.Add(new Dictionary<string, object?>(typed, StringComparer.OrdinalIgnoreCase));
                taken++;
                continue;
            }

            if (row is IDictionary<string, object> boxed)
            {
                materialized.Add(boxed.ToDictionary(static pair => pair.Key, static pair => (object?)pair.Value, StringComparer.OrdinalIgnoreCase));
                taken++;
                continue;
            }
        }

        return materialized;
    }

    public static CommandDefinition Command(
        string sql,
        object? parameters,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        CommandType commandType = CommandType.Text)
    {
        return new CommandDefinition(
            sql,
            parameters,
            commandType: commandType,
            commandTimeout: (int)timeout.TotalSeconds,
            cancellationToken: cancellationToken);
    }
}
