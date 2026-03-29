using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Mcp.SqlServer.Core.Abstractions;
using Mcp.SqlServer.Core.Caching;
using Mcp.SqlServer.Core.Security;
using Mcp.SqlServer.Core.Services;
using Mcp.SqlServer.Core.Tools;

namespace Mcp.SqlServer.Core.Hosting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSqlServerMcpCore(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<SqlServerMcpOptions>()
            .Bind(configuration.GetSection(SqlServerMcpOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "A SQL Server connection string is required.")
            .ValidateOnStart();

        services.AddMemoryCache();
        services.AddSingleton<SqlCommandClassifier>();
        services.AddSingleton<SqlSafetyValidator>();
        services.AddSingleton<ISqlServerCache, MemorySqlServerCache>();
        services.AddSingleton<ISqlServerMcpAuditSink, LoggingAuditSink>();
        services.AddSingleton<SqlConnectionFactory>();
        services.AddSingleton<SqlQueryExecutor>();
        services.AddSingleton<SqlSchemaService>();
        services.AddSingleton<SqlReportingService>();
        services.AddSingleton<SqlAdminService>();
        services.AddSingleton<SqlServerMcpToolService>();
        services.AddHostedService<MetadataWarmupService>();

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("Mcp.SqlServer.Core"))
            .WithTracing(builder =>
            {
                builder
                    .AddSource("Mcp.SqlServer.Core")
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddConsoleExporter();
            })
            .WithMetrics(builder =>
            {
                builder
                    .AddMeter("Mcp.SqlServer.Core")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddConsoleExporter();
            });

        return services;
    }

    public static IMcpServerBuilder AddSqlServerMcpServer(this IServiceCollection services)
    {
        return services
            .AddMcpServer()
            .WithTools<SqlServerMcpTools>(McpServerJsonContext.Default.Options);
    }
}

[JsonSerializable(typeof(ToolEnvelope))]
[JsonSerializable(typeof(SqlQueryResponse))]
[JsonSerializable(typeof(SqlWriteResponse))]
[JsonSerializable(typeof(TableDescription))]
[JsonSerializable(typeof(QueryPlanResponse))]
[JsonSerializable(typeof(ReportResponse))]
[JsonSerializable(typeof(ListSchemasRequest))]
[JsonSerializable(typeof(ListTablesRequest))]
[JsonSerializable(typeof(DescribeTableRequest))]
[JsonSerializable(typeof(DescribeRelationshipsRequest))]
[JsonSerializable(typeof(SearchSchemaRequest))]
[JsonSerializable(typeof(ReadRecordsRequest))]
[JsonSerializable(typeof(ExecuteSqlRequest))]
[JsonSerializable(typeof(CreateRecordRequest))]
[JsonSerializable(typeof(UpdateRecordRequest))]
[JsonSerializable(typeof(DeleteRecordRequest))]
[JsonSerializable(typeof(ExecuteStoredProcedureRequest))]
[JsonSerializable(typeof(ExplainQueryRequest))]
[JsonSerializable(typeof(GetQueryPlanRequest))]
[JsonSerializable(typeof(AnalyzeIndexesRequest))]
[JsonSerializable(typeof(DatabaseHealthReportRequest))]
[JsonSerializable(typeof(QueryPerformanceReportRequest))]
[JsonSerializable(typeof(TableStatisticsReportRequest))]
[JsonSerializable(typeof(CreateIndexRequest))]
[JsonSerializable(typeof(RebuildIndexRequest))]
[JsonSerializable(typeof(UpdateStatisticsRequest))]
[JsonSerializable(typeof(RunMaintenanceTaskRequest))]
public partial class McpServerJsonContext : JsonSerializerContext
{
}
