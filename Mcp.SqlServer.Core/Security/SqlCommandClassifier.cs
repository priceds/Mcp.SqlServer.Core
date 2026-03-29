using System.Text.RegularExpressions;
using Mcp.SqlServer.Core.Abstractions;

namespace Mcp.SqlServer.Core.Security;

internal sealed partial class SqlCommandClassifier
{
    [GeneratedRegex(@"^\s*(?:/\*.*?\*/\s*)*(?:--.*?$[\r\n]*)*(?<verb>[a-zA-Z]+)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex LeadingVerbRegex();

    private static readonly string[] NonDeterministicTokens =
    [
        "getdate(",
        "sysdatetime(",
        "newid(",
        "rand(",
        "current_timestamp",
        "row_number(",
        "newsequentialid(",
        "@@spid",
        "#"
    ];

    public string GetLeadingVerb(string sql)
    {
        var match = LeadingVerbRegex().Match(sql);
        return match.Success ? match.Groups["verb"].Value.ToLowerInvariant() : string.Empty;
    }

    public SqlOperationKind Classify(string sql)
    {
        var verb = GetLeadingVerb(sql);
        return verb switch
        {
            "select" or "with" => SqlOperationKind.Read,
            "insert" or "update" or "delete" or "merge" => SqlOperationKind.Write,
            "create" or "alter" or "drop" or "dbcc" or "truncate" or "rebuild" => SqlOperationKind.Admin,
            "exec" or "execute" => SqlOperationKind.Write,
            _ => SqlOperationKind.Unknown
        };
    }

    public bool IsDeterministicRead(string sql)
    {
        if (Classify(sql) is not SqlOperationKind.Read)
        {
            return false;
        }

        var normalized = sql.ToLowerInvariant();
        return NonDeterministicTokens.All(token => !normalized.Contains(token, StringComparison.Ordinal));
    }
}
