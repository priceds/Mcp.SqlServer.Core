# Mcp.SqlServer.Core

High-performance SQL Server access for MCP clients, built on .NET 10 with one shared core, two transports, strong safety controls, intelligent caching, and enterprise-ready diagnostics.

## Why this server exists

Most SQL MCP servers stop at schema discovery and raw query execution. `Mcp.SqlServer.Core` goes further:

- Safe read and write data access with capability gating
- Shared implementation for `stdio` and streamable HTTP MCP transports
- Multi-layer intelligent caching for metadata, deterministic reads, plans, and reports
- Execution-plan retrieval, operational reporting, and index analysis
- Admin tooling that is present but disabled by default
- Structured audit logging, OpenTelemetry, and cancellation-aware async execution

This makes the server suitable for local IDE copilots, internal agents, remote MCP hosting, and serious SQL operations workflows without splitting the product into multiple disconnected services.

## Feature Snapshot

| Area | Capability |
| --- | --- |
| Discovery | `list_databases`, `list_schemas`, `list_tables`, `describe_table`, `describe_relationships`, `search_schema` |
| Data access | `read_records`, `execute_sql`, `create_record`, `update_record`, `delete_record`, `execute_stored_procedure` |
| Diagnostics | `explain_query`, `get_query_plan`, `analyze_indexes` |
| Reporting | `database_health_report`, `query_performance_report`, `table_statistics_report` |
| Admin | `create_index`, `rebuild_index`, `update_statistics`, `run_maintenance_task` |
| Safety | capability profiles, database allowlists, denied token checks, command timeouts, row limits, audit trails |
| Performance | memory caching, deterministic query result caching, plan/report snapshot caching, async ADO.NET, JSON source generation |
| Deployment | `stdio` host, HTTP host, one shared core |

## Architecture

```text
                 ┌──────────────────────────────┐
                 │ MCP Client                   │
                 │ Claude / Cursor / Copilot    │
                 └──────────────┬───────────────┘
                                │
                ┌───────────────┴────────────────┐
                │ Transport Layer                 │
                │ stdio host or streamable HTTP   │
                └───────────────┬────────────────┘
                                │
                ┌───────────────┴────────────────┐
                │ Mcp.SqlServer.Core             │
                │ tool handlers + safety + cache │
                │ diagnostics + reports + audit  │
                └───────────────┬────────────────┘
                                │
                ┌───────────────┴────────────────┐
                │ SQL Server                     │
                │ data + metadata + DMVs + plans │
                └────────────────────────────────┘
```

## Solution Layout

- `Mcp.SqlServer.Core.Abstractions`
  Shared options, capability profiles, and tool contracts.
- `Mcp.SqlServer.Core`
  SQL execution engine, safety validation, caching, diagnostics, reporting, MCP tools, and DI registration.
- `Mcp.SqlServer.Core.StdioHost`
  Console host for local MCP clients using stdio transport.
- `Mcp.SqlServer.Core.HttpHost`
  ASP.NET Core host exposing streamable HTTP MCP endpoints.
- `Mcp.SqlServer.Core.Tests`
  Unit coverage for classification, safety validation, and cache key behavior.

## Tool Catalog

### Discovery

- `list_databases`
- `list_schemas`
- `list_tables`
- `describe_table`
- `describe_relationships`
- `search_schema`

### Read and write

- `read_records`
- `execute_sql`
- `create_record`
- `update_record`
- `delete_record`
- `execute_stored_procedure`

### Diagnostics and reporting

- `explain_query`
- `get_query_plan`
- `analyze_indexes`
- `database_health_report`
- `query_performance_report`
- `table_statistics_report`

### Admin, disabled by default

- `create_index`
- `rebuild_index`
- `update_statistics`
- `run_maintenance_task`

## Safety Model

`Mcp.SqlServer.Core` is designed to avoid the classic "raw SQL gateway with a README warning" trap.

- Capability profiles:
  `ReadOnly`, `ReadWrite`, `Admin`
- Admin tools ship disabled by default
- Denied SQL tokens block dangerous server-level operations
- Database allowlists restrict scope at configuration time
- Stored procedure execution is separately controlled
- Command timeouts and row limits are enforced centrally
- Write and admin actions emit audit events with correlation IDs

## Intelligent Caching

Caching is built into the server core rather than bolted on at the client layer.

- Metadata cache:
  schema, table, and relationship discovery
- Deterministic query cache:
  keyed by normalized SQL, database, parameters, and capability context
- Query plan cache:
  estimated and actual plan payload reuse
- Report snapshot cache:
  health, performance, and statistics reports

Read-result caching is intentionally bypassed for non-deterministic or session-sensitive SQL.

## Performance Techniques

- Async Dapper-based query execution on top of `Microsoft.Data.SqlClient`
- Connection pooling through standard SQL client behavior
- JSON source generation for tool request and response contracts
- Shared service core for both transports
- OpenTelemetry traces and metrics
- Background metadata warmup
- Cached report and execution-plan generation
- Centralized timeout selection for standard versus expensive operations

## Configuration

Both hosts bind the same `SqlServerMcp` configuration section.

```json
{
  "SqlServerMcp": {
    "ConnectionString": "Server=localhost,1433;Database=master;User ID=sa;Password=Your_strong_password123;TrustServerCertificate=True;Encrypt=False;",
    "CapabilityProfile": "ReadWrite",
    "EnableAdminTools": false,
    "EnableExecutionPlans": true,
    "EnableReporting": true,
    "EnableMetadataWarmup": true,
    "WarmupDatabases": [ "master" ],
    "CachePolicy": {
      "MetadataTtl": "00:30:00",
      "DeterministicQueryTtl": "00:02:00",
      "QueryPlanTtl": "00:10:00",
      "ReportSnapshotTtl": "00:05:00"
    },
    "Safety": {
      "AllowedDatabases": [ "master" ],
      "MaxRows": 250,
      "MaxPayloadBytes": 1000000,
      "DefaultCommandTimeout": "00:00:30",
      "ExpensiveCommandTimeout": "00:01:30",
      "AllowStoredProcedures": true
    }
  }
}
```

## NativeAOT Releases

The default shipped artifact is the `stdio` host, published as a NativeAOT, self-contained, single-file executable for:

- `linux-x64`
- `win-x64`
- `osx-arm64`

Each release archive includes:

- the platform-native `Mcp.SqlServer.Core.StdioHost` binary
- `appsettings.json.example`

Release automation lives in [release.yml](/Users/sarvesh/Mcp.SqlServer.Core/.github/workflows/release.yml). When you push a tag like `v0.1.0`, GitHub Actions:

- restores the solution
- runs the test suite
- publishes NativeAOT binaries for the three target platforms
- packages release archives
- uploads them to the GitHub Release

Regular CI lives in [ci.yml](/Users/sarvesh/Mcp.SqlServer.Core/.github/workflows/ci.yml). It runs on pushes to `main`, pull requests targeting `main`, and manual dispatch. CI validates restore, build, tests, and a Linux `linux-x64` NativeAOT smoke publish.

## Running the Server

### 1. Restore

```bash
dotnet restore Mcp.SqlServer.Core.slnx
```

### 2. Build

```bash
dotnet build Mcp.SqlServer.Core.slnx
```

### 3. Run the stdio host

```bash
dotnet run --project Mcp.SqlServer.Core.StdioHost
```

### 3a. Publish a local NativeAOT binary

Apple Silicon:

```bash
brew install openssl@3 brotli zlib

export CPATH=/opt/homebrew/include:/opt/homebrew/opt/openssl@3/include:/opt/homebrew/opt/zlib/include
export LIBRARY_PATH=/opt/homebrew/lib:/opt/homebrew/opt/openssl@3/lib:/opt/homebrew/opt/zlib/lib
export DYLD_FALLBACK_LIBRARY_PATH=/opt/homebrew/lib:/opt/homebrew/opt/openssl@3/lib:/opt/homebrew/opt/zlib/lib

dotnet publish Mcp.SqlServer.Core.StdioHost/Mcp.SqlServer.Core.StdioHost.csproj \
  -c Release \
  -r osx-arm64 \
  -p:PublishAot=true \
  -p:PublishSingleFile=true \
  -p:SelfContained=true
```

Linux x64:

```bash
dotnet publish Mcp.SqlServer.Core.StdioHost/Mcp.SqlServer.Core.StdioHost.csproj \
  -c Release \
  -r linux-x64 \
  -p:PublishAot=true \
  -p:PublishSingleFile=true \
  -p:SelfContained=true
```

Windows x64:

```bash
dotnet publish Mcp.SqlServer.Core.StdioHost/Mcp.SqlServer.Core.StdioHost.csproj ^
  -c Release ^
  -r win-x64 ^
  -p:PublishAot=true ^
  -p:PublishSingleFile=true ^
  -p:SelfContained=true
```

### 4. Run the HTTP host

```bash
dotnet run --project Mcp.SqlServer.Core.HttpHost
```

HTTP MCP endpoint:

```text
/mcp
```

## Docker and Remote Hosting

The HTTP host is built for container or central MCP deployment:

- mount environment-specific configuration
- inject `SqlServerMcp__ConnectionString` from secrets
- keep admin tools disabled unless a dedicated operational deployment requires them
- front the service with your normal reverse proxy and auth layer

## MCP Client Setup

For release binaries, configure your MCP client to launch the extracted stdio executable directly.

Claude Desktop example on macOS or Linux:

```json
{
  "mcpServers": {
    "sqlserver": {
      "command": "/absolute/path/to/Mcp.SqlServer.Core.StdioHost",
      "args": []
    }
  }
}
```

Windows example:

```json
{
  "mcpServers": {
    "sqlserver": {
      "command": "C:\\absolute\\path\\to\\Mcp.SqlServer.Core.StdioHost.exe",
      "args": []
    }
  }
}
```

Before launching the binary:

1. Copy `appsettings.json.example` to `appsettings.json`.
2. Set the SQL Server connection string.
3. Review `AllowedDatabases`, row limits, and admin-tool settings.
4. Keep `EnableAdminTools` disabled unless you explicitly need it.

## Example Positioning

Use this server when you want:

- one SQL Server MCP endpoint instead of separate schema, CRUD, and diagnostics servers
- a .NET-native implementation for Windows, Linux, containers, and enterprise hosting
- richer operational context than a generic `execute_sql` server can provide
- performance features that help repeated MCP tool calls stay fast under agent workloads

## Validation

Current verification in this repo:

```bash
dotnet build Mcp.SqlServer.Core.slnx --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false /clp:ErrorsOnly
dotnet test Mcp.SqlServer.Core.Tests/Mcp.SqlServer.Core.Tests.csproj -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false
```

NativeAOT smoke test:

```bash
dotnet publish Mcp.SqlServer.Core.StdioHost/Mcp.SqlServer.Core.StdioHost.csproj -c Release -r osx-arm64 -p:PublishAot=true -p:PublishSingleFile=true -p:SelfContained=true
```

## Current Status

This repository now contains a full greenfield MCP server foundation for SQL Server:

- shared contracts and options
- a capability-gated tool surface
- stdio and HTTP hosts
- safety validation and caching
- diagnostics and reporting tools
- baseline automated tests

If you want to extend it next, the natural follow-ups are integration tests against a real SQL Server container, auth for remote HTTP deployments, and richer DMV/report surfaces.
