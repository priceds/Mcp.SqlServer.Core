namespace Mcp.SqlServer.Core.Abstractions;

public enum SqlOperationKind
{
    Read = 0,
    Write = 1,
    Admin = 2,
    Unknown = 3
}
