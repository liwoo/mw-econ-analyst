# 🇲🇼 Malawi Financial Intelligence — MCP Server

> **An MCP (Model Context Protocol) server exposing Malawi's financial
> market data as structured, queryable tools for AI assistants. Built for
> advisory firms like [Bridgepath Capital](https://www.bridgepathcapitalmw.com)
> whose analysts use ChatGPT, Claude, and Copilot to answer institutional-grade
> financial questions across equities, fixed income, FX, commodities, tobacco,
> real estate, and macroeconomics.**

Built with the **official [.NET MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)**
(`ModelContextProtocol` v1.1.0), maintained in collaboration with Microsoft.

**31 MCP tools · 20 REST endpoints · 17 DB tables · 12 tool classes**

---

## Table of Contents

- [Data Sources](#data-sources)
- [Architecture](#architecture)
- [Ingestion Pipeline](#ingestion-pipeline)
- [The 31 Tools](#the-31-tools)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Configuration](#configuration)
- [Running the Server](#running-the-server)
- [Connecting to AI Clients](#connecting-to-ai-clients)
- [Documentation](#documentation)
- [Roadmap](#roadmap)
- [Acknowledgements](#acknowledgements)

---

## Data Sources

### Core document streams (Bridgepath Capital)

| Document | Cadence | Primary analytical value |
|---|---|---|
| **Weekly Market Update** | 52×/year | Stock prices, TB auctions, yield curve, FX rates, inflation, volume, market events |
| **Monthly Economic Report** | 12×/year | Stock valuations (P/E, P/BV, dividend yield), commodity prices, institutional forecasts, trade data, banking metrics, World Bank analysis |
| **Annual Budget Brief** | 1×/year | Fiscal policy, sector allocations, expenditure breakdowns, revenue reforms, macro targets |

### External live data sources

| Source | Data | Frequency | Endpoint |
|---|---|---|---|
| IFPRI | Maize retail prices — national + 3 regions | Monthly | IFPRI website |
| NFRA | Maize purchase / sale floor prices | As revised | Press releases |
| OPEC | Reference basket oil price | Monthly | OPEC website |
| NSO | Trade statistics by commodity | Monthly | NSO website |
| MERA | Domestic fuel pump prices (APM) | Monthly | MERA gazette PDF |
| ESCOM | Electricity tariff bands | Periodic | ESCOM website |
| BWB / LWB | Water tariffs (Blantyre, Lilongwe) | Periodic | Water board websites |
| AHL | Tobacco weekly auction results | Weekly — Apr to Sep | AHL bulletin (relationship required) |
| TIMB | Zimbabwe tobacco reference price | Weekly — Mar to Nov | timb.gov.zw |
| USDA FAS | Global tobacco supply/demand balance | Annual | apps.fas.usda.gov/psdonline |
| Lotus Resources | Kayelekera uranium milestones | Event-driven | ASX announcements |
| Sovereign Metals | Kasiya graphite/rutile milestones | Event-driven | ASX announcements |
| Agent survey | Real estate rental prices | Quarterly | Knight Frank, Pam Golding, local agents |

### What the combined source set enables

**Weekly alone**: *"What happened in the market this week?"*, *"Where are TB yields?"*, *"Which stocks moved?"*

**Monthly adds**: *"Is AIRTEL expensive vs its history?"*, *"Does oil price predict non-food inflation?"*, *"What do EIU and World Bank project for macro?"*

**External live adds**: *"Is property in Blantyre competitive vs government securities right now?"*, *"What is the AHL-TIMB tobacco basis spread entering the 2026 season?"*, *"What is the MERA diesel pump price this month?"*

**Combined**: Cross-asset allocation analysis — the question no other Malawi platform answers.

### Indicators tracked

**Weekly Appendix 1 (time-series)**

| Category | Indicators |
|---|---|
| Exchange rates | MK/USD, MK/GBP, MK/EUR, MK/ZAR |
| FX reserves | Total reserves (USD mn) — note 3-month publication lag |
| Inflation | Headline CPI, Food CPI, Non-food CPI |
| Interest rates | MPR, Overnight Interbank, Lombard Rate, Commercial Bank Reference Rate |
| Govt securities | 91-day TB, 182-day TB, 364-day TB, 2-yr TN, 3-yr TN, 5-yr TN, 7-yr TN, 10-yr TN |
| Equity index returns | MASI YTD, DSI YTD, FSI YTD |

**Weekly stock prices** (16 equities):
AIRTEL, BHL, FDHB, FMBCH, ICON, ILLOVO, MPICO, NBM, NBS, NICO, NITL, OMU, PCL, STANDARD, SUNBIRD, TNM

**Monthly Appendix 2** (stock valuations, per equity):
P/E ratio, P/BV ratio, dividend yield (%), market capitalisation (MK bn)

**Commodities** (17 tracked — see MCP.md for full vocabulary):
Maize (national + 3 regions + NFRA floor), fertiliser imports, OPEC oil,
diesel imports, petrol imports, domestic fuel pump price (MERA),
uranium (Kayelekera), graphite (Kasiya), rutile (Kasiya),
tobacco exports, soybeans, groundnuts, macadamia

**Institutional forecasts** (per institution × indicator × year):
EIU, World Bank, Oxford Economics, GoM (SONA), GoM (Budget) —
GDP growth, CPI average, MK/USD, government balance, exports/imports

---

## Architecture

```
╔══════════════════════════════════════════════════════════════════════════╗
║                           DATA SOURCES                                   ║
║                                                                          ║
║  Weekly PDFs    Monthly PDFs    Annual Budget    External Live Feeds     ║
║  (52x/year)     (12x/year)      (1x/year)        AHL · TIMB · MERA      ║
║       │               │               │           ESCOM · Agents · USDA  ║
╚═══════╪═══════════════╪═══════════════╪═══════════════════════╪══════════╝
        │               │               │                       │
╔═══════╪═══════════════╪═══════════════╪═══════════════════════╪══════════╗
║                       INGESTION PIPELINE                                 ║
║                                                                          ║
║  WeeklyPdfIngester     MonthlyPdfIngester    BudgetIngester              ║
║  ─────────────────     ──────────────────    ─────────────               ║
║  AppendixExtractor     ValuationExtractor    BudgetExtractor             ║
║  NarrativeExtractor    CommodityExtractor    ColQwen2Embedder            ║
║  AuctionExtractor      ForecastExtractor                                 ║
║  EntityTagger          TradeFlowExtractor                                ║
║  BM25 index update     MonthlyNarrativeExtractor                         ║
║                                                                          ║
║  LiveDataPipeline (external feeds — separate scheduler)                  ║
║  ─────────────────────────────────────────────────────                   ║
║  TobaccoAuctionExtractor   TIMBScraper        FuelPriceExtractor         ║
║  RealEstatePriceCollector  MineralMilestoneMonitor                       ║
║                                                                          ║
║  ────────── APScheduler FileWatcher (auto-ingest on PDF drop) ─────────  ║
╚══════════════════════════════════════════════════════════════════════════╝
        │
        ▼
╔══════════════════════════════════════════════════════════════════════════╗
║                           DATA STORES                                    ║
║                                                                          ║
║  TimescaleDB (PostgreSQL — 17 tables)    BM25 Index (in-memory)          ║
║  ────────────────────────────────────    ───────────────────────         ║
║  financial_indicators (hypertable)       market_events narrative         ║
║  auction_events (per-tenor)              monthly_narrative               ║
║  stock_valuations                                                        ║
║  commodity_prices (17 commodities)                                       ║
║  institutional_forecasts                                                 ║
║  trade_flows                                                             ║
║  banking_metrics                                                         ║
║  export_structure                                                        ║
║  monthly_market_activity                                                 ║
║  weekly_stock_volume                                                     ║
║  data_freshness                                                          ║
║  tobacco_ahl_auctions                                                    ║
║  tobacco_timb_reference                                                  ║
║  tobacco_global_balance                                                  ║
║  real_estate_prices                                                      ║
║  mineral_milestones                                                      ║
║  market_events                                                           ║
╚══════════════════════════════════════════════════════════════════════════╝
        │
╔══════════════════════════════════════════════════════════════════════════╗
║                     .NET MCP SERVER  (31 tools)                          ║
║                                                                          ║
║  MarketDataTools (4)      SignalTools (3)        EquityTools (5)         ║
║  ────────────────         ─────────────          ────────────────        ║
║  GetLatestSnapshot        DetectSignals          GetStockHistory         ║
║  QueryIndicators          ComparePeriods         GetIndexDivergence      ║
║  ComputeRealRate          ComputeSpread          GetWeeklyStockVolume    ║
║  GetYieldCurveSnapshot                           GetLiquidityProfile     ║
║                                                  GetValuationMetrics     ║
║                                                                          ║
║  CommodityTools (3)       CorrelationTools (2)   NarrativeTools (4)      ║
║  ────────────────         ──────────────────     ──────────────────      ║
║  GetCommodityPrices       ComputeCorrelation     GetMarketEvents         ║
║  GetMineralMilestones     ComputeRelative        SearchByEntity          ║
║  GetCrossAssetYield         Strength             GetCorporateActions     ║
║    Matrix                                        GetAuctionHistory       ║
║                                                                          ║
║  TradeStructureTools (1)  BankingTools (2)       ForecastTools (1)       ║
║  ───────────────────      ─────────────          ─────────────           ║
║  GetExportStructure       GetBankingSector        GetInstitutional       ║
║                             Metrics               Forecasts              ║
║                           GetTradeBalance                                ║
║                                                                          ║
║  TobaccoTools (2)         RealEstateTools (2)    SearchTools (2)         ║
║  ────────────────         ───────────────────    ─────────────           ║
║  GetTobaccoAuction        GetRealEstatePrices    Search                  ║
║  GetTobaccoOutlook        GetCrossAssetYield     Fetch                   ║
║                             Matrix                                       ║
╚══════════════════════════════════════════════════════════════════════════╝
        │  MCP protocol (HTTP/stdio)
   ┌────┴────┬────────────┬──────────┐
   ▼         ▼            ▼          ▼
ChatGPT  Claude.ai  GitHub Copilot  Cursor
```

---

## Ingestion Pipeline

Four ingesters handle the four data stream types. PDF ingesters share
the same `DoclingClient`, TimescaleDB connection, and BM25 index — they
differ only in which extractors they run.

### File naming & drop directories

```
data/raw/
├── weekly/     bridgepath_market_update_YYYY_MM_DD.pdf
├── monthly/    bridgepath_monthly_economic_YYYY_MM.pdf
└── budget/     malawi_budget_brief_YYYY_YY.pdf
```

Dates are parsed from filenames and applied as metadata to every record.

### Manual ingestion

```bash
# Weekly — backfill all historical
dotnet run -- ingest --type weekly --directory ./data/raw/weekly --backfill

# Monthly — backfill all historical
dotnet run -- ingest --type monthly --directory ./data/raw/monthly --backfill

# Budget — single file
dotnet run -- ingest --type budget \
  --file ./data/raw/budget/malawi_budget_brief_2026_27.pdf
```

### Automatic ingestion

When `AutoIngestEnabled: true`, a file watcher monitors all three drop
directories. Any PDF dropped is automatically classified and ingested
within 60 seconds.

---

### Weekly PDF ingestion (`WeeklyPdfIngester`)

Each weekly PDF (6 pages) runs five extractors sequentially:

| Stage | Extractor | Source zone | Output table |
|---|---|---|---|
| 1 | `NarrativeExtractor` | Pages 1–2 (news items) | `market_events` |
| 2 | `EntityTagger` | Narrative text | JSONB tags on `market_events` |
| 3 | `AppendixExtractor` | Page 5 (Appendix 1 — ~40 indicators × 13 months) | `financial_indicators` |
| 4 | `AuctionExtractor` | Page 4 (govt securities — TB + per-tenor breakdown) | `auction_events` |
| 5 | BM25 index update | All narrative | In-memory BM25 |

The `AppendixExtractor` is the highest-priority stage — it parses the
40-row × 13-column historical indicators table with full row/column header
alignment preserved. Numerical precision enforced at <0.01% error
tolerance in `AppendixExtractorTests.cs`.

The `AuctionExtractor` now writes per-tenor rows (91-day, 182-day, 364-day)
with `pct_of_total_applications` populated — the key signal that captured
the market duration-shortening before the February 2026 yield compression.

---

### Monthly PDF ingestion (`MonthlyPdfIngester`)

Each monthly PDF (19 pages) runs five extractors:

| Stage | Extractor | Source zone | Output table |
|---|---|---|---|
| 1 | `ValuationExtractor` | Appendix 2 (stock valuations) | `stock_valuations` |
| 2 | `CommodityExtractor` | Section 3 (commodities — IFPRI chart, OPEC chart) | `commodity_prices` |
| 3 | `ForecastExtractor` | Appendix 4 (EIU projections table) | `institutional_forecasts` |
| 4 | `TradeFlowExtractor` | Narrative (trade balance items) | `trade_flows` |
| 5 | `MonthlyNarrativeExtractor` | Sections 1–3 (analysis text) | `market_events` (monthly tag) |

**`ValuationExtractor`** — parses Appendix 2: P/E ratio, P/BV ratio,
dividend yield, and market cap for all 16 MSE equities. Enables proper
investment analysis not possible from price data alone.

**`CommodityExtractor`** — parses the IFPRI maize chart (MK/kg national +
3 regions) and OPEC basket price. Uses DePlot to extract data points from
annotated chart images before writing to `commodity_prices`.

**`ForecastExtractor`** — parses EIU Five-Year Forecast table (Appendix 4).
One row per institution × indicator × year. World Bank and Oxford Economics
projections are extracted from narrative by `MonthlyNarrativeExtractor`.

**`TradeFlowExtractor`** — parses trade balance from narrative: exports
(total and by commodity), imports (total and by category),
export-to-import ratio.

---

### Budget PDF ingestion (`BudgetIngester`)

Annual budget briefs are infographic-heavy. ColQwen2 visual embeddings
are used alongside Docling text extraction for chart-based data.

| Stage | Extractor | Output |
|---|---|---|
| 1 | `DoclingClient` | Text chunks |
| 2 | `ColQwen2Embedder` | Visual page vectors → Qdrant |
| 3 | `BudgetExtractor` | `budget_allocations` |

> ⚠️ The budget pipeline is the only one that requires Qdrant. Weekly
> and monthly pipelines write entirely to TimescaleDB and the BM25 index.

---

### Live data pipeline (`LiveDataPipeline`)

External feeds that run on their own schedule, independent of PDF ingestion:

| Extractor | Source | Cadence | Output table | Status |
|---|---|---|---|---|
| `TobaccoAuctionExtractor` | AHL weekly bulletin | Weekly (Apr–Sep) | `tobacco_ahl_auctions` | ⏳ pending |
| `TIMBScraper` | timb.gov.zw PDF | Weekly (Mar–Nov) | `tobacco_timb_reference` | ⏳ pending |
| `USDAFasFetcher` | USDA FAS PSD API | Annual | `tobacco_global_balance` | ⏳ pending |
| `FuelPriceExtractor` | MERA monthly gazette | Monthly | `commodity_prices` | ⏳ pending |
| `RealEstatePriceCollector` | Agent survey / Property24 | Quarterly | `real_estate_prices` | ⏳ pending |
| `MineralMilestoneMonitor` | ASX announcements | Event-driven | `mineral_milestones` | ⏳ pending |

> ⚠️ **Time-critical**: `TobaccoAuctionExtractor` and `TIMBScraper` must
> be live before the 2026 AHL season opens (~7 April 2026). Missing the
> season open means waiting until April 2027 for a complete season dataset.

---

## The 31 Tools

Grouped into 12 `[McpServerToolType]` classes. Full documentation in
[MCP.md](./MCP.md).

| Class | Tools | Description |
|---|---|---|
| `MarketDataTools` | 4 | Snapshot, raw time-series, real rate, yield curve |
| `SignalTools` | 3 | Anomaly detection, period comparison, spread computation |
| `EquityTools` | 5 | Stock history, index divergence, weekly volume, liquidity, valuations |
| `CommodityTools` | 3 | All 17 commodities, mineral milestones, cross-asset yield matrix |
| `CorrelationTools` | 2 | Pearson/Spearman correlation with guardrails, relative strength / alpha |
| `NarrativeTools` | 4 | BM25 market events, entity search, corporate actions, auction history |
| `TradeStructureTools` | 1 | Export structure decline + Africa benchmarks |
| `BankingTools` | 2 | Sector health metrics, trade balance |
| `ForecastTools` | 1 | Per-institution macro projections |
| `TobaccoTools` | 2 | AHL + TIMB + USDA tobacco intelligence layer |
| `RealEstateTools` | 2 | Rental prices, implied yields, cross-asset yield matrix |
| `SearchTools` | 2 | Search + Fetch (ChatGPT Deep Research) |

---

## Project Structure

```
MalawiFinancialMcp/
│
├── MalawiFinancialMcp.csproj
├── Program.cs
│
├── Tools/
│   ├── MarketDataTools.cs          # GetLatestSnapshot, QueryIndicators,
│   │                               # ComputeRealRate, GetYieldCurveSnapshot
│   ├── SignalTools.cs              # DetectMarketSignals, ComparePeriods,
│   │                               # ComputeSpread
│   ├── EquityTools.cs             # GetStockHistory, GetIndexDivergence,
│   │                               # GetWeeklyStockVolume, GetLiquidityProfile,
│   │                               # GetValuationMetrics
│   ├── CommodityTools.cs          # GetCommodityPrices, GetMineralMilestones,
│   │                               # GetCrossAssetYieldMatrix
│   ├── CorrelationTools.cs        # ComputeCorrelation,
│   │                               # ComputeRelativeStrength
│   ├── NarrativeTools.cs          # GetMarketEvents, SearchByEntity,
│   │                               # GetCorporateActions, GetAuctionHistory
│   ├── TradeStructureTools.cs     # GetExportStructure
│   ├── BankingTools.cs            # GetBankingSectorMetrics, GetTradeBalance
│   ├── ForecastTools.cs           # GetInstitutionalForecasts
│   ├── TobaccoTools.cs            # GetTobaccoAuction, GetTobaccoOutlook
│   ├── RealEstateTools.cs         # GetRealEstatePrices, GetCrossAssetYieldMatrix
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
│   │   ├── IValuationRepository.cs
│   │   ├── ValuationRepository.cs
│   │   ├── ICommodityRepository.cs
│   │   ├── CommodityRepository.cs
│   │   ├── IForecastRepository.cs
│   │   ├── ForecastRepository.cs
│   │   ├── IBankingRepository.cs
│   │   ├── BankingRepository.cs
│   │   ├── ITradeRepository.cs
│   │   ├── TradeRepository.cs
│   │   ├── ITobaccoRepository.cs
│   │   ├── TobaccoRepository.cs
│   │   ├── IRealEstateRepository.cs
│   │   └── RealEstateRepository.cs
│   └── Models/
│       ├── FinancialIndicator.cs
│       ├── MarketEvent.cs
│       ├── AuctionEvent.cs
│       ├── StockValuation.cs
│       ├── CommodityPrice.cs
│       ├── InstitutionalForecast.cs
│       ├── BankingMetric.cs
│       ├── TradeFlow.cs
│       ├── TobaccoAhlAuction.cs
│       ├── TobaccoTimbReference.cs
│       ├── TobaccoGlobalBalance.cs
│       ├── RealEstatePrice.cs
│       └── MineralMilestone.cs
│
├── Services/
│   ├── ISignalDetector.cs
│   ├── SignalDetector.cs
│   ├── INarrativeSearchService.cs
│   ├── BM25NarrativeSearchService.cs
│   ├── ICorrelationService.cs
│   └── CorrelationService.cs       # Pearson/Spearman + 3 statistical guardrails
│
├── Ingestion/
│   ├── WeeklyPdfIngester.cs
│   ├── MonthlyPdfIngester.cs
│   ├── BudgetIngester.cs
│   ├── LiveDataPipeline.cs         # External feed orchestrator
│   ├── DoclingClient.cs
│   ├── AppendixExtractor.cs
│   ├── NarrativeExtractor.cs
│   ├── EntityTagger.cs
│   ├── AuctionExtractor.cs         # Now writes per-tenor rows
│   ├── ValuationExtractor.cs
│   ├── CommodityExtractor.cs       # DePlot chart extraction
│   ├── ForecastExtractor.cs
│   ├── TradeFlowExtractor.cs
│   ├── MonthlyNarrativeExtractor.cs
│   ├── ColQwen2Embedder.cs         # Budget visual embeddings → Qdrant
│   ├── TobaccoAuctionExtractor.cs  # AHL weekly bulletin parser
│   ├── TIMBScraper.cs              # timb.gov.zw PDF scraper
│   ├── USDAFasFetcher.cs           # USDA PSD API client
│   ├── FuelPriceExtractor.cs       # MERA gazette parser
│   ├── RealEstatePriceCollector.cs # Agent survey + Property24 scraper
│   ├── MineralMilestoneMonitor.cs  # ASX announcement monitor
│   └── FileWatcher.cs
│
├── Migrations/
│   ├── 001_InitSchema.sql
│   ├── 002_AddMonthlyTables.sql
│   ├── 003_AddCommodityExpansion.sql
│   ├── 004_AddTobaccoTables.sql
│   └── 005_AddRealEstateAndMinerals.sql
│
├── Tests/
│   MalawiFinancialMcp.Tests/
│   ├── Tools/
│   │   ├── MarketDataToolsTests.cs
│   │   ├── SignalToolsTests.cs
│   │   ├── EquityToolsTests.cs
│   │   ├── CommodityToolsTests.cs
│   │   ├── CorrelationToolsTests.cs  # Statistical guardrail validation critical
│   │   ├── NarrativeToolsTests.cs
│   │   ├── TobaccoToolsTests.cs
│   │   ├── RealEstateToolsTests.cs
│   │   └── SearchToolsTests.cs
│   ├── Ingestion/
│   │   ├── AppendixExtractorTests.cs
│   │   ├── ValuationExtractorTests.cs
│   │   ├── CommodityExtractorTests.cs
│   │   ├── AuctionExtractorTests.cs  # Per-tenor row validation
│   │   └── EntityTaggerTests.cs
│   └── Services/
│       └── CorrelationServiceTests.cs
│
└── MalawiFinancialApi/              ← REST API — 20 endpoints
    ├── MalawiFinancialApi.csproj
    ├── Program.cs
    ├── Controllers/
    │   ├── SnapshotController.cs
    │   ├── EquitiesController.cs
    │   ├── YieldsController.cs
    │   ├── RatesController.cs
    │   ├── FxController.cs
    │   ├── MacroController.cs
    │   ├── ForecastsController.cs
    │   ├── CommoditiesController.cs
    │   ├── TradeController.cs
    │   ├── BankingController.cs
    │   ├── IndicesController.cs
    │   ├── RealEstateController.cs
    │   └── CrossAssetController.cs
    └── DTOs/
        └── ...
```

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

## Configuration

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
  },
  "LiveData": {
    "TobaccoEnabled": false,
    "TIMBEnabled": false,
    "FuelPricesEnabled": false,
    "RealEstateEnabled": false,
    "AHLBulletinDirectory": "./data/live/tobacco/ahl",
    "MERAGazetteDirectory": "./data/live/fuel"
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
  },
  "ApiKeys": {
    "Free": ["key-free-001"],
    "Professional": ["key-pro-001"],
    "Institutional": ["key-inst-001"],
    "Enterprise": ["key-ent-001"]
  }
}
```

---

## Running the Server

```bash
# MCP server — stdio transport (local dev, Claude Desktop)
dotnet run --project MalawiFinancialMcp -- --transport stdio

# MCP server — HTTP transport (production, Claude.ai, ChatGPT, Copilot)
dotnet run --project MalawiFinancialMcp -- --transport http --port 5000

# REST API server (dashboard and non-AI clients)
dotnet run --project MalawiFinancialApi -- --port 5001
```

For ChatGPT and Claude.ai the MCP server must be reachable over HTTPS.
Use [ngrok](https://ngrok.com/) or [Cloudflare Tunnel](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/) for local development:

```bash
ngrok http 5000
# → https://abc123.ngrok.app → localhost:5000
```

---

## Connecting to AI Clients

### ChatGPT

Supports two modes — **Chat** (all 31 tools) and **Deep Research**
(`Search` + `Fetch` only, for comprehensive multi-step analysis).

**Requirements**: ChatGPT Pro, Team, Enterprise, or Education plan;
Developer Mode enabled; server accessible over HTTPS.

1. **Settings → Connectors → Advanced → Enable Developer Mode**
2. **Settings → Connectors → Create**
3. Fill in:
   - **Name**: Malawi Financial Intelligence
   - **Description**: Cross-asset financial intelligence for Malawi — equities, fixed income, FX, commodities, tobacco intelligence, real estate yields, and macro analysis
   - **Connector URL**: `https://your-server/mcp`
4. Click **Create** — ChatGPT lists all 31 available tools
5. To use: **+** → **More** → **Developer Mode** → enable connector

For Deep Research: begin a prompt with *"Use deep research to..."*

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
      "args": [
        "run",
        "--project", "/path/to/MalawiFinancialMcp",
        "--", "--transport", "stdio"
      ]
    }
  }
}
```

Restart Claude Desktop — all 31 tools appear automatically.

---

### Claude.ai

**Settings → Integrations → Add MCP Server** →
enter `https://your-server/mcp`

---

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

## Documentation

| Document | Contents |
|---|---|
| [MCP.md](./MCP.md) | All 31 MCP tool definitions, 12 tool classes, coverage matrix (55 questions), statistical guardrails, 17 DB table schemas |
| [REST.md](./REST.md) | All 20 REST endpoints, request/response shapes, tier access control, rate limits, dashboard integration guide |
| [FEATURE_MAP.md](./FEATURE_MAP.md) | Feature segmentation across Free / Professional / Institutional / Enterprise tiers |
| [CHANGELOG.md](./CHANGELOG.md) | v1 — initial audit: 12 data additions from weekly + monthly PDF audit |
| [CHANGELOG_V2.md](./CHANGELOG_V2.md) | v2 — commodity expansion: 2 → 17 tracked commodities, 5 categories |
| [CHANGELOG_V3.md](./CHANGELOG_V3.md) | v3 — live prices panel: 22 price points across food, fuel, utilities |
| [CHANGELOG_V4.md](./CHANGELOG_V4.md) | v4 — tobacco intelligence layer + real estate yields + cross-asset yield matrix |

---

## Roadmap

- [x] **v0.1** — 16 tools (weekly only), TimescaleDB, manual CLI ingestion
- [x] **v0.2** — File watcher auto-ingestion, entity tagger
- [x] **v0.3** — Monthly ingestion pipeline, 22 tools (valuations, commodities, correlation, banking, trade)
- [x] **v0.4** — REST API layer, per-tenor auction breakdown, weekly stock volume, data freshness
- [x] **v0.5** — Commodity expansion (17 commodities, 5 categories), mineral milestone tracking
- [x] **v0.6** — Live prices panel (22 price points — food, fuel, utilities)
- [x] **v0.7** — Tobacco intelligence layer (AHL + TIMB + USDA) + real estate yields + cross-asset yield matrix
- [ ] **v0.8** — Live data pipeline: TobaccoAuctionExtractor + TIMBScraper (⚠️ before April 2026)
- [ ] **v0.9** — Live data pipeline: FuelPriceExtractor (MERA), RealEstatePriceCollector
- [ ] **v1.0** — Dashboard live data integration (replace all static arrays with API calls)
- [ ] **v1.1** — Historical backfill: all weekly + monthly PDFs
- [ ] **v1.2** — Budget ingestion pipeline + ColQwen2 visual embeddings
- [ ] **v1.3** — HTTPS deployment + ChatGPT OAuth + tier-based access control
- [ ] **v1.4** — Multi-tenancy, billing integration, user authentication
- [ ] **v1.5** — Sector taxonomy + peer group analysis tools
- [ ] **v2.0** — SSA expansion (Zambia, Zimbabwe, Mozambique data streams)

---

## Acknowledgements

- [Bridgepath Capital](https://www.bridgepathcapitalmw.com) — weekly
  and monthly research publications
- [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk)
  — official C# SDK maintained in collaboration with Microsoft
- [Docling](https://github.com/DS4SD/docling) by IBM Research — PDF parsing
- [TimescaleDB](https://www.timescale.com/) — time-series PostgreSQL
- IFPRI, OPEC, NSO Malawi, RBM, MSE, USDA FAS — data sources

---

## License

MIT — see [LICENSE](./LICENSE) for details.

> **Disclaimer**: For analytical and research purposes only. All outputs
> should be independently verified before use in investment or advisory
> decisions. Data sourced from the Government of Malawi, Reserve Bank of
> Malawi, Malawi Stock Exchange, IFPRI, OPEC, EIU, World Bank, USDA FAS,
> AHL, TIMB, MERA, ESCOM, and Bridgepath Capital publications.
