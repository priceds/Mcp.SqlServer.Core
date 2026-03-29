using System.Text.Json;
using Mcp.SqlServer.Core.Caching;

namespace Mcp.SqlServer.Core.Tests;

public sealed class CacheKeyBuilderTests
{
    [Fact]
    public void ForQuery_ChangesWhenParametersChange()
    {
        var left = new Dictionary<string, JsonElement> { ["id"] = JsonDocument.Parse("1").RootElement.Clone() };
        var right = new Dictionary<string, JsonElement> { ["id"] = JsonDocument.Parse("2").RootElement.Clone() };

        var leftKey = CacheKeyBuilder.ForQuery("appdb", "select * from dbo.users where id = @id", left, "ReadWrite");
        var rightKey = CacheKeyBuilder.ForQuery("appdb", "select * from dbo.users where id = @id", right, "ReadWrite");

        leftKey.Should().NotBe(rightKey);
    }

    [Fact]
    public void ForMetadata_IsStableForSameInput()
    {
        var first = CacheKeyBuilder.ForMetadata("appdb", "schemas");
        var second = CacheKeyBuilder.ForMetadata("appdb", "schemas");

        first.Should().Be(second);
    }
}
