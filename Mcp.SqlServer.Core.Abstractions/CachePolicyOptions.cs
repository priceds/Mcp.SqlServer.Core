namespace Mcp.SqlServer.Core.Abstractions;

public sealed class CachePolicyOptions
{
    public TimeSpan MetadataTtl { get; set; } = TimeSpan.FromMinutes(30);

    public TimeSpan DeterministicQueryTtl { get; set; } = TimeSpan.FromMinutes(2);

    public TimeSpan QueryPlanTtl { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan ReportSnapshotTtl { get; set; } = TimeSpan.FromMinutes(5);

    public bool EnableBackgroundRefresh { get; set; } = true;
}
