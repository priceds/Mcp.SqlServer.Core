using Microsoft.Extensions.Options;
using Mcp.SqlServer.Core.Abstractions;
using Mcp.SqlServer.Core.Security;
using ModelContextProtocol;

namespace Mcp.SqlServer.Core.Tests;

public sealed class SqlSafetyValidatorTests
{
    private readonly SqlSafetyValidator _validator;

    public SqlSafetyValidatorTests()
    {
        var options = Options.Create(new SqlServerMcpOptions
        {
            ConnectionString = "Server=localhost;Database=master;Trusted_Connection=true;",
            CapabilityProfile = CapabilityProfile.ReadWrite,
            EnableAdminTools = false,
            Safety = new ExecutionSafetyOptions
            {
                AllowedDatabases = ["master", "appdb"]
            }
        });

        _validator = new SqlSafetyValidator(new SqlCommandClassifier(), new StaticOptionsMonitor<SqlServerMcpOptions>(options.Value));
    }

    [Fact]
    public void ValidateSql_ThrowsForDeniedToken()
    {
        var action = () => _validator.ValidateSql("SELECT * FROM sys.objects; EXEC xp_cmdshell 'dir';", "master");
        action.Should().Throw<McpException>().WithMessage("*blocked by server safety policy*");
    }

    [Fact]
    public void ValidateSql_ThrowsForDisallowedDatabase()
    {
        var action = () => _validator.ValidateSql("SELECT 1", "otherdb");
        action.Should().Throw<McpException>().WithMessage("*not allowed*");
    }

    [Fact]
    public void ValidateSql_AllowsReadQueryForConfiguredDatabase()
    {
        var result = _validator.ValidateSql("SELECT 1", "master");
        result.Should().Be(SqlOperationKind.Read);
    }
}
