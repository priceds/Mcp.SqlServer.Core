using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mcp.SqlServer.Core.Abstractions;
using Mcp.SqlServer.Core.Services;

namespace Mcp.SqlServer.Core.Hosting;

internal sealed class MetadataWarmupService : BackgroundService
{
    private readonly SqlSchemaService _schemaService;
    private readonly IOptionsMonitor<SqlServerMcpOptions> _options;
    private readonly ILogger<MetadataWarmupService> _logger;

    public MetadataWarmupService(SqlSchemaService schemaService, IOptionsMonitor<SqlServerMcpOptions> options, ILogger<MetadataWarmupService> logger)
    {
        _schemaService = schemaService;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.CurrentValue.EnableMetadataWarmup)
        {
            return;
        }

        try
        {
            var (_, dbCacheHit) = await _schemaService.ListDatabasesAsync(stoppingToken).ConfigureAwait(false);
            foreach (var database in _options.CurrentValue.WarmupDatabases)
            {
                await _schemaService.ListSchemasAsync(database, stoppingToken).ConfigureAwait(false);
                await _schemaService.ListTablesAsync(database, null, stoppingToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Metadata warmup completed. Database cache hit: {CacheHit}", dbCacheHit);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Metadata warmup failed during startup.");
        }
    }
}
