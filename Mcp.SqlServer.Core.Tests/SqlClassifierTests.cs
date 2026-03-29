using Mcp.SqlServer.Core.Abstractions;
using Mcp.SqlServer.Core.Security;

namespace Mcp.SqlServer.Core.Tests;

public sealed class SqlClassifierTests
{
    private readonly SqlCommandClassifier _classifier = new();

    [Theory]
    [InlineData("SELECT * FROM dbo.Users", SqlOperationKind.Read)]
    [InlineData("WITH cte AS (SELECT 1 AS value) SELECT * FROM cte", SqlOperationKind.Read)]
    [InlineData("INSERT INTO dbo.Users(Id) VALUES (1)", SqlOperationKind.Write)]
    [InlineData("UPDATE dbo.Users SET Name = 'x'", SqlOperationKind.Write)]
    [InlineData("ALTER INDEX IX_Users ON dbo.Users REBUILD", SqlOperationKind.Admin)]
    public void Classify_ReturnsExpectedOperation(string sql, SqlOperationKind expected)
    {
        _classifier.Classify(sql).Should().Be(expected);
    }

    [Theory]
    [InlineData("SELECT * FROM dbo.Users", true)]
    [InlineData("SELECT GETDATE()", false)]
    [InlineData("SELECT * FROM #Temp", false)]
    public void IsDeterministicRead_MatchesExpectations(string sql, bool expected)
    {
        _classifier.IsDeterministicRead(sql).Should().Be(expected);
    }
}
