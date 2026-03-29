using Mcp.SqlServer.Core.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

builder.Services.AddSqlServerMcpCore(builder.Configuration);
builder.Services
    .AddSqlServerMcpServer()
    .WithStdioServerTransport();

using var host = builder.Build();
var server = host.Services.GetRequiredService<McpServer>();
await server.RunAsync();
