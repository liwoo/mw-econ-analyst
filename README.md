# Malawi Financial Intelligence — MCP Server

MCP server + REST API exposing Malawi's financial market data as structured tools for AI assistants. Covers equities, fixed income, FX, commodities, tobacco, real estate, and macroeconomics. Data sourced from Bridgepath Capital PDF reports and external live feeds.

Built with .NET 9 and the [official MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk). See [SPEC.md](./SPEC.md) for architecture, data sources, ingestion pipelines, and the full tool inventory.

---

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Docker and Docker Compose
- A running Docling instance (included in `docker-compose.yml`)
- Qdrant (only required for budget PDF ingestion — optional)

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
  -f Migrations/005_AddRealEstateAndMinerals.sql

dotnet restore && dotnet build
```

---

## Running

```bash
# MCP server — stdio transport (local dev, Claude Desktop)
dotnet run --project MalawiFinancialMcp -- --transport stdio

# MCP server — HTTP transport (production, Claude.ai, ChatGPT, Copilot)
dotnet run --project MalawiFinancialMcp -- --transport http --port 5000

# REST API server (dashboard and non-AI clients)
dotnet run --project MalawiFinancialApi -- --port 5001
```

---

## Ingesting Data

```bash
# Scrape Bridgepath Capital PDFs
cd scraper && pip install requests beautifulsoup4 && python scraper.py

# Weekly — backfill all historical
dotnet run -- ingest --type weekly --directory ./data/raw/weekly --backfill

# Monthly — backfill all historical
dotnet run -- ingest --type monthly --directory ./data/raw/monthly --backfill

# Budget — single file
dotnet run -- ingest --type budget \
  --file ./data/raw/budget/malawi_budget_brief_2026_27.pdf
```

When `AutoIngestEnabled: true` in config, PDFs dropped into `data/raw/weekly/`, `data/raw/monthly/`, or `data/raw/budget/` are automatically ingested.

---

## Tests

```bash
dotnet test
```

---

## Configuration

All settings live in `appsettings.json`:

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

See `appsettings.json` for the full set of options including `LiveData`, `SignalDetector`, `Correlation`, and `ApiKeys`.

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
MalawiFinancialMcp/          ← MCP server (31 tools, 12 tool classes)
├── Tools/                   ← McpServerToolType classes
├── Data/Repositories/       ← Repository pattern (interface + impl per table)
├── Data/Models/             ← Entity models
├── Services/                ← Signal detection, BM25 search, correlation
├── Ingestion/               ← PDF extractors + live data pipeline
└── Migrations/              ← TimescaleDB schema (5 migration files)

MalawiFinancialApi/          ← REST API (20 endpoints, tier-based access)
├── Controllers/
└── DTOs/

MalawiFinancialMcp.Tests/    ← Unit tests (tools, ingestion, services)

scraper/                     ← Bridgepath Capital PDF scraper
```

---

## Documentation

| Document | Contents |
|---|---|
| [SPEC.md](./SPEC.md) | Architecture, data sources, ingestion pipelines, tool inventory, roadmap |
| [MCP.md](./MCP.md) | All 31 MCP tool definitions, DB table schemas, coverage matrix |
| [REST.md](./REST.md) | All 20 REST endpoints, request/response shapes, tier access |
| [FEATURE_MAP.md](./FEATURE_MAP.md) | Feature segmentation across access tiers |

---

## License

MIT — see [LICENSE](./LICENSE) for details.
