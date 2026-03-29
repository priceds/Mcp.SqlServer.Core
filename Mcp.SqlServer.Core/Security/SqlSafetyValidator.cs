using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Mcp.SqlServer.Core.Abstractions;
using ModelContextProtocol;

namespace Mcp.SqlServer.Core.Security;

internal sealed class SqlSafetyValidator
{
    private readonly SqlCommandClassifier _classifier;
    private readonly IOptionsMonitor<SqlServerMcpOptions> _options;

    public SqlSafetyValidator(SqlCommandClassifier classifier, IOptionsMonitor<SqlServerMcpOptions> options)
    {
        _classifier = classifier;
        _options = options;
    }

    public SqlOperationKind ValidateSql(string sql, string? database, bool allowAdmin = false)
    {
        var options = _options.CurrentValue;
        EnsureDatabaseAllowed(database, options);

        var lowered = sql.ToLowerInvariant();
        foreach (var token in options.Safety.DeniedTokens)
        {
            if (lowered.Contains(token, StringComparison.Ordinal))
            {
                throw new McpException($"SQL token '{token}' is blocked by server safety policy.");
            }
        }

        var operation = _classifier.Classify(sql);
        EnsureCapability(operation, allowAdmin, options);
        return operation;
    }

    public void EnsureCapability(SqlOperationKind operation, bool allowAdmin, SqlServerMcpOptions options)
    {
        if (operation is SqlOperationKind.Admin && (!options.EnableAdminTools || !allowAdmin || options.CapabilityProfile is not CapabilityProfile.Admin))
        {
            throw new McpException("Admin SQL operations are disabled.");
        }

        if (operation is SqlOperationKind.Write && options.CapabilityProfile is CapabilityProfile.ReadOnly)
        {
            throw new McpException("Write operations are disabled for the current capability profile.");
        }
    }

    public void EnsureDatabaseAllowed(string? database, SqlServerMcpOptions options)
    {
        if (string.IsNullOrWhiteSpace(database) || options.Safety.AllowedDatabases.Length is 0)
        {
            return;
        }

        if (!options.Safety.AllowedDatabases.Contains(database, StringComparer.OrdinalIgnoreCase))
        {
            throw new McpException($"Database '{database}' is not allowed by server configuration.");
        }
    }

    public void ValidateStoredProcedureAccess(SqlServerMcpOptions options)
    {
        if (!options.Safety.AllowStoredProcedures)
        {
            throw new McpException("Stored procedure execution is disabled.");
        }

        if (options.CapabilityProfile is CapabilityProfile.ReadOnly)
        {
            throw new McpException("Stored procedure execution is not allowed in read-only mode.");
        }
    }

    public string NormalizeSql(string sql)
    {
        var builder = new StringBuilder(sql.Length);
        var previousWhitespace = false;

        foreach (var ch in sql)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (previousWhitespace)
                {
                    continue;
                }

                builder.Append(' ');
                previousWhitespace = true;
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
            previousWhitespace = false;
        }

        return builder.ToString().Trim();
    }

    public SqlConnectionStringBuilder BuildConnectionString(string baseConnectionString, string? databaseOverride)
    {
        var builder = new SqlConnectionStringBuilder(baseConnectionString);
        if (!string.IsNullOrWhiteSpace(databaseOverride))
        {
            builder.InitialCatalog = databaseOverride;
        }

        return builder;
    }

    public int ClampRowLimit(int? requested, SqlServerMcpOptions options)
    {
        return requested is > 0 ? Math.Min(requested.Value, options.Safety.MaxRows) : options.Safety.MaxRows;
    }

    public TimeSpan SelectTimeout(SqlOperationKind operation, SqlServerMcpOptions options)
    {
        return operation is SqlOperationKind.Admin ? options.Safety.ExpensiveCommandTimeout : options.Safety.DefaultCommandTimeout;
    }
}
