namespace Mcp.SqlServer.Core.Abstractions;

public sealed class SqlServerMcpOptions
{
    public const string SectionName = "SqlServerMcp";

    public string ConnectionString { get; set; } = string.Empty;

    public CapabilityProfile CapabilityProfile { get; set; } = CapabilityProfile.ReadWrite;

    public bool EnableAdminTools { get; set; }

    public bool EnableHttpTransport { get; set; } = true;

    public bool EnableStdioTransport { get; set; } = true;

    public bool EnableDetailedSqlLogging { get; set; }

    public bool EnableOpenTelemetryConsoleExporter { get; set; } = true;

    public bool EnableExecutionPlans { get; set; } = true;

    public bool EnableReporting { get; set; } = true;

    public bool EnableMetadataWarmup { get; set; } = true;

    public string[] WarmupDatabases { get; set; } = [];

    public CachePolicyOptions CachePolicy { get; set; } = new();

    public ExecutionSafetyOptions Safety { get; set; } = new();
}
