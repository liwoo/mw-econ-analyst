# Malawi Financial Intelligence — MCP Server

MCP server + REST API exposing Malawi's financial market data as structured tools for AI assistants. Covers equities, fixed income, FX, commodities, tobacco, real estate, and macroeconomics. Data sourced from Bridgepath Capital PDF reports and external live feeds.

Built with .NET 9, [Dapper](https://github.com/DapperLib/Dapper) for data access, [TimescaleDB](https://www.timescale.com/) for time-series storage, [FastEndpoints](https://fast-endpoints.com/) for the REST API, [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) for orchestration, [Docling](https://github.com/docling-project/docling-serve) for PDF parsing, and the [official MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

---

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Docker and Docker Compose
- Python 3.10+ (for the PDF scraper)

---

## Installation

```bash
git clone https://github.com/your-org/malawi-financial-mcp.git
cd malawi-financial-mcp
docker compose up -d

# Run all migrations
psql postgresql://postgres:malawi_agent@localhost:5432/malawi_financial \
  -f Migrations/001_InitSchema.sql \
  -f Migrations/002_AddMonthlyTables.sql \
  -f Migrations/003_AddCommodityExpansion.sql \
  -f Migrations/004_AddTobaccoTables.sql \
  -f Migrations/005_AddRealEstateAndMinerals.sql \
  -f Migrations/006_AddUniqueConstraints.sql

dotnet restore && dotnet build
```

---

## Running

```bash
# All services via Aspire (recommended for local dev)
dotnet run --project MalawiFinancialMcp.AppHost

# Or run individually:

# MCP server — stdio transport (Claude Desktop)
dotnet run --project MalawiFinancialMcp -- --transport stdio

# MCP server — HTTP transport (Claude.ai, ChatGPT, Copilot)
dotnet run --project MalawiFinancialMcp -- --transport http --port 5000

# REST API server (dashboard, non-AI clients)
dotnet run --project MalawiFinancialApi -- --port 5001
```

The Aspire dashboard provides health checks, logs, and traces for all services at `http://localhost:15888`.

---

## Ingesting Data

```bash
# Scrape Bridgepath Capital PDFs
cd scraper && pip install requests beautifulsoup4 && python scraper.py

# Weekly — backfill all historical
dotnet run --project MalawiFinancialMcp -- ingest --type weekly --directory ./data --backfill

# Single file
dotnet run --project MalawiFinancialMcp -- ingest --type weekly --file "./data/january 2026/weekly/BridgepathCapital...pdf"
```

The ingestion pipeline:
1. Discovers PDFs in `data/` (handles all filename conventions from 2020–2026)
2. Sends each PDF to **Docling-serve** (local Docker container) for structured parsing
3. Runs extractors: `AppendixExtractor` (40 indicators × 13 months), `NarrativeExtractor`, `AuctionExtractor`
4. Persists to TimescaleDB via Dapper with idempotent upserts

Docling results are cached as `.docling.json` alongside each PDF to avoid re-parsing on subsequent runs.

---

## Tests

```bash
dotnet test
```

---

## Configuration

All settings live in `MalawiFinancialMcp/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "TimescaleDB": "Host=localhost;Port=5432;Database=malawi_financial;Username=postgres;Password=malawi_agent"
  },
  "Docling": {
    "BaseUrl": "http://localhost:8080"
  },
  "Ingestion": {
    "WeeklyWatchDirectory":  "./data/raw/weekly",
    "MonthlyWatchDirectory": "./data/raw/monthly",
    "BudgetWatchDirectory":  "./data/raw/budget",
    "AutoIngestEnabled": true
  }
}
```

See `appsettings.json` for the full set of options including `LiveData`, `SignalDetector`, and `Correlation`.

---

## Connecting AI Clients

### Claude Desktop

```json
// ~/Library/Application Support/Claude/claude_desktop_config.json
{
  "mcpServers": {
    "malawi-financial": {
      "command": "dotnet",
      "args": [
        "run",
        "--project", "/path/to/MalawiFinancialMcp",
        "--", "--transport", "stdio"
      ]
    }
  }
}
```

### Claude.ai

**Settings → Integrations → Add MCP Server** → enter `https://your-server/mcp`

### ChatGPT

Requires Pro/Team/Enterprise/Education plan with Developer Mode enabled.

1. **Settings → Connectors → Advanced → Enable Developer Mode**
2. **Settings → Connectors → Create** → set Connector URL to `https://your-server/mcp`
3. In chat: **+** → **More** → **Developer Mode** → enable connector

### GitHub Copilot (VS Code)

```json
// .vscode/mcp.json
{
  "servers": {
    "malawi-financial": {
      "type": "http",
      "url": "https://your-server/mcp"
    }
  }
}
```

### Cursor

```json
// ~/.cursor/mcp.json
{
  "mcpServers": {
    "malawi-financial": {
      "type": "http",
      "url": "https://your-server/mcp"
    }
  }
}
```

For local dev, expose port 5000 via [ngrok](https://ngrok.com/) or [Cloudflare Tunnel](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/):

```bash
ngrok http 5000
```

---

## Project Structure

```
MalawiFinancialMcp/              ← MCP server (31 tools, 12 tool classes)
├── Tools/                       ← [McpServerToolType] classes
├── Data/
│   ├── Models/                  ← 17 entity POCOs (Dapper-mapped)
│   ├── Repositories/            ← 10 interface + implementation pairs
│   └── DbConnectionFactory.cs   ← Npgsql connection factory
├── Services/                    ← Signal detection, BM25 search, correlation
└── Ingestion/                   ← DoclingClient, extractors, PDF date parser

MalawiFinancialApi/              ← REST API (20 FastEndpoints, Swagger UI)
└── Endpoints/                   ← Feature-organized endpoint classes

MalawiFinancialMcp.AppHost/      ← Aspire orchestrator (Postgres, MCP, API)
MalawiFinancialMcp.ServiceDefaults/ ← OpenTelemetry, health checks, resilience
MalawiFinancialMcp.Tests/        ← Unit tests (tools, ingestion, services)

Migrations/                      ← 6 TimescaleDB SQL migrations (17 tables)
scraper/                         ← Bridgepath Capital PDF scraper (Python)
data/                            ← Scraped PDFs (gitignored)
```

---

## Documentation

| Document | Contents |
|---|---|
| [MCP.md](./MCP.md) | All 31 MCP tool definitions, DB table schemas, coverage matrix |
| [REST.md](./REST.md) | All 20 REST endpoints, request/response shapes, tier access |

---

## License

MIT — see [LICENSE](./LICENSE) for details.
