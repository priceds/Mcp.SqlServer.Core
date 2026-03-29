using Mcp.SqlServer.Core.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

builder.Services.AddSqlServerMcpCore(builder.Configuration);
builder.Services
    .AddSqlServerMcpServer()
    .WithHttpTransport();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "Mcp.SqlServer.Core",
    protocol = "MCP",
    transport = "streamable-http",
    endpoint = "/mcp"
}));

app.MapMcp("/mcp");
await app.RunAsync();
