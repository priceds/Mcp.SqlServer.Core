namespace Mcp.SqlServer.Core.Abstractions;

public sealed class ExecutionSafetyOptions
{
    public string[] AllowedDatabases { get; set; } = [];

    public string[] DeniedTokens { get; set; } =
    [
        "xp_cmdshell",
        "sp_configure",
        "drop login",
        "create login",
        "alter server role",
        "shutdown",
        "openrowset",
        "opendatasource"
    ];

    public string[] AllowedReadVerbs { get; set; } = ["select", "with", "exec"];

    public string[] AllowedWriteVerbs { get; set; } = ["insert", "update", "delete", "merge", "exec"];

    public string[] AllowedAdminVerbs { get; set; } = ["create", "alter", "drop", "rebuild", "update", "dbcc", "exec"];

    public int MaxRows { get; set; } = 250;

    public int MaxPayloadBytes { get; set; } = 1_000_000;

    public TimeSpan DefaultCommandTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan ExpensiveCommandTimeout { get; set; } = TimeSpan.FromSeconds(90);

    public bool AllowStoredProcedures { get; set; } = true;
}
