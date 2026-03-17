# 🇲🇼 Malawi Financial Intelligence — MCP Server

> **An MCP (Model Context Protocol) server exposing Malawi's financial
> market data as structured, queryable tools for AI assistants. Built for
> advisory firms like [Bridgepath Capital](https://www.bridgepathcapitalmw.com)
> allowing analysts to use ChatGPT, Claude, and Copilot to answer institutional-grade
> financial questions across weekly market updates, monthly economic reports,
> and annual budget briefs.**

Built with the **official [.NET MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)**
(`ModelContextProtocol` v1.1.0), maintained in collaboration with Microsoft.

---

## Table of Contents

- [Data Sources](#data-sources)
- [Architecture](#architecture)
- [Ingestion Pipeline](#ingestion-pipeline)
- [The 20 Tools](#the-20-tools)
  - [Coverage Matrix](#coverage-matrix)
  - [Statistical Guardrails](#statistical-guardrails)
  - [Honest Gaps](#honest-gaps)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Configuration](#configuration)
- [Running the Server](#running-the-server)
- [Connecting to AI Clients](#connecting-to-ai-clients)
- [Data Schema](#data-schema)
- [Contributing](#contributing)
- [Roadmap](#roadmap)

---

## Data Sources

Three complementary document streams from Bridgepath Capital, each serving
a different analytical purpose:

| Document | Cadence | Primary Value |
|---|---|---|
| **Weekly Market Update** | 52x/year | High-frequency time-series: stock prices, TB auctions, yield curve, FX rates, inflation, market events |
| **Monthly Economic Report** | 12x/year | Fundamentals: stock valuations (P/E, P/BV, dividend yield, market cap), commodity prices (maize, oil), institutional forecasts, trade data, banking sector metrics |
| **Annual Budget Brief** | 1x/year | Fiscal policy, sector allocations, expenditure breakdowns, revenue reforms, macroeconomic targets |

### What Each Source Enables

**Weekly alone** answers: *"What happened in the market this week?"*,
*"Where are T-bill yields?"*, *"Which stocks moved?"*

**Monthly adds**: *"Is AIRTEL expensive relative to its history?"*,
*"Does oil price predict non-food inflation?"*, *"What do the EIU and
World Bank project for Malawi's macro environment?"*

**Budget adds**: *"How does the current policy rate compare to the
budget's 18% target?"*, *"Has development expenditure as a share of
total budget actually improved?"*

### Indicators Tracked

**Weekly Appendix (time-series, monthly snapshots)**

| Category | Indicators |
|---|---|
| Exchange Rates | MK/USD, MK/GBP, MK/EUR, MK/ZAR |
| FX Reserves | Total Reserves (USD mn) |
| Inflation | Headline CPI, Food CPI, Non-food CPI |
| Interest Rates | MPR, Interbank Rate, Lombard Rate, Commercial Bank Reference Rate |
| Govt Securities Yields | 91-day TB, 182-day TB, 364-day TB, 2-yr TN, 3-yr TN, 5-yr TN, 7-yr TN, 10-yr TN |
| Equity Index Returns | MASI YTD, DSI YTD, FSI YTD |

**Weekly Stock Prices (genuinely weekly observations)**

AIRTEL, BHL, FDHB, FMBCH, ICON, ILLOVO, MPICO, NBM, NBS, NICO, NITL,
OMU, PCL, STANDARD, SUNBIRD, TNM

**Monthly Appendix 2 (stock valuations, monthly)**

Per stock: P/E ratio, P/BV ratio, dividend yield (%), market
capitalisation (MK billions)

**Monthly Commodity Prices (monthly)**

IFPRI maize price (MK/kg) — national average + Northern, Central,
Southern regional breakdown; OPEC reference basket price (USD/barrel)

**Monthly Institutional Forecasts (annual projections)**

EIU, World Bank, Oxford Economics, Government of Malawi projections for:
GDP growth, inflation (average), interest rates, government balance
(% GDP), exports/imports (USD bn), current account balance, MK/USD
exchange rate

---

## Architecture

```
╔══════════════════════════════════════════════════════════════════════╗
║                         DATA SOURCES                                 ║
║                                                                      ║
║  Weekly PDFs (52x/year)   Monthly PDFs (12x/year)   Annual Budget   ║
║         │                        │                       │           ║
╚═════════╪════════════════════════╪═══════════════════════╪═══════════╝
          │                        │                       │
╔═════════╪════════════════════════╪═══════════════════════╪═══════════╗
║                      INGESTION PIPELINE                              ║
║                                                                      ║
║  WeeklyPdfIngester         MonthlyPdfIngester      BudgetIngester    ║
║  ───────────────────       ─────────────────────   ──────────────    ║
║  AppendixExtractor         ValuationExtractor       BudgetExtractor  ║
║  NarrativeExtractor        CommodityExtractor                        ║
║  AuctionExtractor          ForecastExtractor                         ║
║  EntityTagger              TradeFlowExtractor                        ║
║  BM25 index update         MonthlyNarrativeExtractor                 ║
║                                                                      ║
║  ──────── APScheduler FileWatcher (auto-ingest on drop) ──────────   ║
╚══════════════════════════════════════════════════════════════════════╝
          │
          ▼
╔══════════════════════════════════════════════════════════════════════╗
║                        DATA STORES                                   ║
║                                                                      ║
║  TimescaleDB (PostgreSQL)              BM25 Index (in-memory)        ║
║  ──────────────────────────            ──────────────────────────    ║
║  financial_indicators (hypertable)     market_events narrative       ║
║  auction_events                        monthly_narrative             ║
║  stock_valuations                                                    ║
║  commodity_prices                                                    ║
║  institutional_forecasts                                             ║
║  trade_flows                                                         ║
║  budget_allocations                                                  ║
╚══════════════════════════════════════════════════════════════════════╝
          │
╔══════════════════════════════════════════════════════════════════════╗
║                      .NET MCP SERVER                                 ║
║                                                                      ║
║  MarketDataTools    SignalTools      EquityTools                     ║
║  ───────────────    ────────────     ──────────────────              ║
║  GetLatestSnapshot  DetectSignals    GetStockHistory                 ║
║  QueryIndicators    ComparePeriods   GetIndexDivergence              ║
║  ComputeRealRate    ComputeSpread    GetLiquidityProfile             ║
║  GetYieldCurve                       GetValuationMetrics  ← NEW     ║
║                                                                      ║
║  NarrativeTools     CommodityTools   CorrelationTools               ║
║  ──────────────     ─────────────    ──────────────────             ║
║  GetMarketEvents    GetCommodity     ComputeCorrelation  ← NEW      ║
║  SearchByEntity     Prices  ← NEW   ComputeRelative      ← NEW     ║
║  GetCorporate                         Strength                       ║
║    Actions                                                           ║
║  GetAuctionHistory                                                   ║
║                                                                      ║
║  SearchTools (ChatGPT Deep Research)                                 ║
║  ─────────────────────────────────                                   ║
║  Search    Fetch                                                     ║
╚══════════════════════════════════════════════════════════════════════╝
          │  MCP protocol (HTTP)
    ┌─────┴──────┬──────────────┬──────────┐
    ▼            ▼              ▼          ▼
 ChatGPT    Claude.ai    GitHub Copilot  Cursor
```

---

## Ingestion Pipeline

Three ingesters handle the three document types. All share the same
`DoclingClient` for PDF parsing, the same TimescaleDB connection, and
the same BM25 index — they differ only in which extractors they run and
which tables they write to.

### File Naming & Drop Directories

```
data/raw/
├── weekly/     bridgepath_market_update_YYYY_MM_DD.pdf
├── monthly/    bridgepath_monthly_economic_YYYY_MM.pdf
└── budget/     malawi_budget_brief_YYYY_YY.pdf
```

Dates are parsed from filenames and applied as metadata to every record.

### Manual Ingestion

```bash
# Weekly — backfill all historical
dotnet run -- ingest --type weekly --directory ./data/raw/weekly --backfill

# Monthly — backfill all historical
dotnet run -- ingest --type monthly --directory ./data/raw/monthly --backfill

# Budget — single file
dotnet run -- ingest --type budget \
  --file ./data/raw/budget/malawi_budget_brief_2026_27.pdf
```

### Automatic Ingestion

When `AutoIngestEnabled: true`, a file watcher monitors all three drop
directories. Any PDF dropped is automatically classified by type and
ingested within 60 seconds.

---

### Weekly PDF Ingestion (`WeeklyPdfIngester`)

Each weekly PDF (6 pages) runs five extractors sequentially:

| Stage | Extractor | Source Zone | Output Table |
|---|---|---|---|
| 1 | `NarrativeExtractor` | Pages 1–2 (news items) | `market_events` |
| 2 | `EntityTagger` | Narrative text | JSONB tags on `market_events` |
| 3 | `AppendixExtractor` | Page 5 (Appendix 1) | `financial_indicators` (~40 rows) |
| 4 | `AuctionExtractor` | Page 4 (govt securities) | `auction_events` |
| 5 | BM25 index update | All narrative | In-memory BM25 |

The `AppendixExtractor` is the most critical stage — it parses the
40-row × 13-column historical indicators table with full row/column
header alignment preserved. Numerical precision is enforced at
<0.01% error tolerance in `AppendixExtractorTests.cs`.

---

### Monthly PDF Ingestion (`MonthlyPdfIngester`)

Each monthly PDF (19 pages) runs five extractors:

| Stage | Extractor | Source Zone | Output Table |
|---|---|---|---|
| 1 | `ValuationExtractor` | Appendix 2 (stock valuations) | `stock_valuations` |
| 2 | `CommodityExtractor` | Section 3 (commodities) | `commodity_prices` |
| 3 | `ForecastExtractor` | Appendix 4 (EIU projections) | `institutional_forecasts` |
| 4 | `TradeFlowExtractor` | Narrative (trade data items) | `trade_flows` |
| 5 | `MonthlyNarrativeExtractor` | Sections 1–3 (analysis) | `market_events` (monthly tag) |

#### `ValuationExtractor` — highest priority stage

Parses Appendix 2 which contains P/E ratio, P/BV ratio, dividend yield,
and market capitalisation for all 16 MSE equities. This is the
fundamental data that enables proper investment analysis — previously
missing from the weekly pipeline entirely.

Example output for one monthly PDF:
```json
[
  { "month": "2026-02", "ticker": "AIRTEL",   "pe_ratio": 29.30, "pbv_ratio": 47.25, "div_yield_pct": 1.8,  "market_cap_mk_bn": 1252 },
  { "month": "2026-02", "ticker": "FMBCH",    "pe_ratio": 57.04, "pbv_ratio": 14.18, "div_yield_pct": 0.1,  "market_cap_mk_bn": 6745 },
  { "month": "2026-02", "ticker": "STANDARD", "pe_ratio": 57.58, "pbv_ratio": 29.22, "div_yield_pct": 3.8,  "market_cap_mk_bn": 4973 },
  ...
]
```

#### `CommodityExtractor`

Parses the IFPRI maize price chart (MK/kg national + 3 regions) and
OPEC reference basket price (USD/barrel). Both series appear as
annotated charts in the monthly report — DePlot is used to extract the
data points from chart images before writing to `commodity_prices`.

#### `ForecastExtractor`

Parses the EIU Five-Year Forecast table (Appendix 4). Stores one row
per institution per indicator per year in `institutional_forecasts`.
Currently handles EIU; World Bank and Oxford Economics projections
appear in narrative form and are extracted by
`MonthlyNarrativeExtractor` as structured text events.

#### `TradeFlowExtractor`

Parses trade balance figures from narrative text — exports (total and
by commodity), imports (total and by category), export-to-import ratio.
Closes the trade data gap identified in earlier versions of this system.

---

### Budget PDF Ingestion (`BudgetIngester`)

Annual budget briefs are infographic-heavy — ColQwen2 visual embeddings
are used alongside Docling text extraction to capture chart-based data
that standard OCR misses.

| Stage | Extractor | Output Table |
|---|---|---|
| 1 | `DoclingClient` | Text chunks |
| 2 | `ColQwen2Embedder` | Visual page vectors → Qdrant |
| 3 | `BudgetExtractor` | `budget_allocations` |

> ⚠️ The budget pipeline is the only one that requires Qdrant. Weekly
> and monthly pipelines write entirely to TimescaleDB and the BM25 index.
> Qdrant is optional for deployments that only need weekly + monthly data.

---

## Documentation

| Document | Contents |
|---|---|
| [MCP.md](./MCP.md) | All 22 MCP tool definitions, coverage matrix, statistical guardrails, data schema |
| [REST.md](./REST.md) | REST API endpoints, request/response shapes, dashboard integration, auth |

---

## Project Structure

```
MalawiFinancialMcp/
│
├── MalawiFinancialMcp.csproj
├── Program.cs
│
├── Tools/
│   ├── MarketDataTools.cs         # GetLatestSnapshot, QueryIndicators,
│   │                              # ComputeRealRate, GetYieldCurveSnapshot
│   ├── SignalTools.cs             # DetectMarketSignals, ComparePeriods,
│   │                              # ComputeSpread
│   ├── EquityTools.cs             # GetStockHistory, GetIndexDivergence,
│   │                              # GetLiquidityProfile, GetValuationMetrics
│   ├── CommodityTools.cs          # GetCommodityPrices
│   ├── CorrelationTools.cs        # ComputeCorrelation,
│   │                              # ComputeRelativeStrength
│   ├── NarrativeTools.cs          # GetMarketEvents, SearchByEntity,
│   │                              # GetCorporateActions, GetAuctionHistory
│   ├── BankingTools.cs            # GetBankingSectorMetrics
│   ├── TradeTools.cs              # GetTradeBalance
│   └── SearchTools.cs             # Search, Fetch (ChatGPT Deep Research)
│
├── Data/
│   ├── MalawiDbContext.cs
│   ├── Repositories/
│   │   ├── IIndicatorRepository.cs
│   │   ├── IndicatorRepository.cs
│   │   ├── IMarketEventRepository.cs
│   │   ├── MarketEventRepository.cs
│   │   ├── IAuctionRepository.cs
│   │   ├── AuctionRepository.cs
│   │   ├── IValuationRepository.cs     # New — stock_valuations
│   │   ├── ValuationRepository.cs
│   │   ├── ICommodityRepository.cs     # New — commodity_prices
│   │   ├── CommodityRepository.cs
│   │   ├── IForecastRepository.cs      # New — institutional_forecasts
│   │   ├── ForecastRepository.cs
│   │   ├── IBankingRepository.cs       # New — banking_metrics
│   │   ├── BankingRepository.cs
│   │   ├── ITradeRepository.cs         # New — trade_flows
│   │   └── TradeRepository.cs
│   └── Models/
│       ├── FinancialIndicator.cs
│       ├── MarketEvent.cs
│       ├── AuctionEvent.cs
│       ├── StockValuation.cs           # New
│       ├── CommodityPrice.cs           # New
│       ├── InstitutionalForecast.cs    # New
│       ├── BankingMetric.cs             # New
│       └── TradeFlow.cs                # New
│
├── Services/
│   ├── ISignalDetector.cs
│   ├── SignalDetector.cs
│   ├── INarrativeSearchService.cs
│   ├── BM25NarrativeSearchService.cs
│   ├── ICorrelationService.cs          # New
│   └── CorrelationService.cs           # New — Pearson/Spearman + guardrails
│
├── Ingestion/
│   ├── WeeklyPdfIngester.cs
│   ├── MonthlyPdfIngester.cs           # New
│   ├── BudgetIngester.cs               # New
│   ├── DoclingClient.cs
│   ├── AppendixExtractor.cs
│   ├── NarrativeExtractor.cs
│   ├── EntityTagger.cs
│   ├── AuctionExtractor.cs
│   ├── ValuationExtractor.cs           # New — Appendix 2 parser
│   ├── CommodityExtractor.cs           # New — maize + oil charts via DePlot
│   ├── ForecastExtractor.cs            # New — EIU table parser
│   ├── TradeFlowExtractor.cs           # New — trade data from narrative
│   ├── MonthlyNarrativeExtractor.cs    # New — monthly analysis sections
│   ├── ColQwen2Embedder.cs             # New — for budget visual embeddings
│   └── FileWatcher.cs
│
├── Migrations/
│   ├── 001_InitSchema.sql
│   └── 002_AddMonthlyTables.sql        # New
│
├── Tests/
│   MalawiFinancialMcp.Tests/
│   ├── Tools/
│   │   ├── MarketDataToolsTests.cs
│   │   ├── SignalToolsTests.cs
│   │   ├── EquityToolsTests.cs
│   │   ├── CommodityToolsTests.cs      # New
│   │   ├── CorrelationToolsTests.cs    # New — guardrail validation critical
│   │   ├── NarrativeToolsTests.cs
│   │   └── SearchToolsTests.cs
│   ├── Ingestion/
│   │   ├── AppendixExtractorTests.cs
│   │   ├── ValuationExtractorTests.cs  # New — P/E precision tests
│   │   ├── CommodityExtractorTests.cs  # New
│   │   └── EntityTaggerTests.cs
│   └── Services/
│       └── CorrelationServiceTests.cs  # New — statistical logic tests
│
├── MalawiFinancialApi/              ← REST API for dashboard
│   ├── MalawiFinancialApi.csproj
│   ├── Program.cs
│   ├── Controllers/
│   │   ├── SnapshotController.cs
│   │   ├── EquitiesController.cs
│   │   ├── YieldsController.cs
│   │   ├── FxController.cs
│   │   ├── RatesController.cs
│   │   ├── MacroController.cs
│   │   ├── CommoditiesController.cs
│   │   ├── BankingController.cs
│   │   ├── TradeController.cs
│   │   └── IndicesController.cs
│   └── DTOs/
│       └── ...
├── docker-compose.yml
├── appsettings.json
└── README.md
```

---

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Docker and Docker Compose
- A running Docling instance (included in `docker-compose.yml`)
- Qdrant (only required if ingesting budget PDFs — optional)

---

## Installation

```bash
git clone https://github.com/your-org/malawi-financial-mcp.git
cd malawi-financial-mcp
docker compose up -d
psql postgresql://postgres:malawi_agent@localhost:5432/malawi_financial \
  -f Migrations/001_InitSchema.sql \
  -f Migrations/002_AddMonthlyTables.sql
dotnet restore && dotnet build
```

---

## Configuration

```json
{
  "ConnectionStrings": {
    "TimescaleDB": "Host=localhost;Port=5432;Database=malawi_financial;Username=postgres;Password=malawi_agent"
  },
  "Docling": { "BaseUrl": "http://localhost:8080" },
  "Ingestion": {
    "WeeklyWatchDirectory":  "./data/raw/weekly",
    "MonthlyWatchDirectory": "./data/raw/monthly",
    "BudgetWatchDirectory":  "./data/raw/budget",
    "AutoIngestEnabled": true
  },
  "SignalDetector": {
    "MasiMoveThresholdPct": 5.0,
    "YieldShiftThresholdBps": 100,
    "FxMoveThresholdPct": 1.0,
    "OversubscriptionThreshold": 5.0
  },
  "Correlation": {
    "MinObservations": 15,
    "NearZeroVarianceThreshold": 0.001,
    "SignificanceLevel": 0.05
  }
}
```

---

## Running the Server

```bash
# stdio (local dev — Claude Desktop)
dotnet run -- --transport stdio

# HTTP (production — Claude.ai, ChatGPT, Copilot)
dotnet run -- --transport http --port 5000
```

For ChatGPT and Claude.ai the server must be reachable over HTTPS.
Use [ngrok](https://ngrok.com/) or [Cloudflare Tunnel](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/)
for local development:

```bash
ngrok http 5000
# → https://abc123.ngrok.app → localhost:5000
```

---

## Connecting to AI Clients

### ChatGPT (Recommended)

Supports two modes — **Chat** (all 20 tools) and **Deep Research**
(`Search` + `Fetch` only, for comprehensive multi-step analysis).

**Requirements**: ChatGPT Pro, Team, Enterprise, or Education plan;
Developer Mode enabled; server accessible over HTTPS.

1. Go to **Settings → Connectors → Advanced → Enable Developer Mode**
2. Go to **Settings → Connectors → Create**
3. Fill in:
   - **Name**: Malawi Financial Intelligence
   - **Description**: Weekly and monthly Malawi financial market data —
     equity prices, valuations, yields, FX, inflation, commodity prices,
     and market events
   - **Connector URL**: `https://your-server/mcp`
4. Click **Create** — ChatGPT lists all 20 available tools
5. To use in chat: **+** → **More** → **Developer Mode** → enable
   connector

For Deep Research: start a prompt with *"Use deep research to..."*

After updating tool definitions, refresh via
**Settings → Connectors → [your connector] → Refresh**.

---

### Claude Desktop

```json
// ~/Library/Application Support/Claude/claude_desktop_config.json
{
  "mcpServers": {
    "malawi-financial": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/MalawiFinancialMcp",
               "--", "--transport", "stdio"]
    }
  }
}
```

Restart Claude Desktop — tools appear automatically.

---

### Claude.ai

**Settings → Integrations → Add MCP Server** →
enter `https://your-server/mcp`

---

### GitHub Copilot in VS Code

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

Click **Start** in the file → open Copilot Chat → **Agent Mode** →
verify server appears in tools list.

---

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

---


- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Docker and Docker Compose
- A running Docling instance (included in `docker-compose.yml`)
- Qdrant (only required if ingesting budget PDFs — optional)

---

## Roadmap

- [ ] **v0.1** — 16 tools (weekly only) + TimescaleDB + manual CLI
- [ ] **v0.2** — File watcher auto-ingestion + entity tagger
- [ ] **v0.3** — Monthly ingestion pipeline + 22 tools
  (GetValuationMetrics, GetCommodityPrices, ComputeCorrelation,
  ComputeRelativeStrength, GetBankingSectorMetrics, GetTradeBalance)
- [ ] **v0.4** — REST API layer (9 endpoints) powering the React dashboard
- [ ] **v0.5** — React dashboard live data integration (replace static arrays)
- [ ] **v0.6** — Historical backfill: all weekly + monthly PDFs
- [ ] **v0.7** — Budget ingestion pipeline + ColQwen2 visual embeddings
- [ ] **v0.8** — HTTPS deployment + ChatGPT OAuth
- [ ] **v0.9** — Sector taxonomy + peer group analysis tools
- [ ] **v1.0** — Production hardening: auth, rate limiting, replication

---

## Acknowledgements

- [Bridgepath Capital](https://www.bridgepathcapitalmw.com) for
  weekly and monthly research publications
- [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk)
  — official C# SDK, maintained in collaboration with Microsoft
- [Docling](https://github.com/DS4SD/docling) by IBM Research
- [TimescaleDB](https://www.timescale.com/)

---

## License

MIT — see [LICENSE](./LICENSE) for details.

> **Disclaimer**: For analytical and research purposes only. All outputs
> should be independently verified before use in investment or advisory
> decisions. Data sourced from Malawi Government, Reserve Bank of Malawi,
> Malawi Stock Exchange, IFPRI, OPEC, EIU, World Bank, and Bridgepath
> Capital publications.
