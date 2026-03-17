# 🇲🇼 Malawi Financial Intelligence — MCP Server

> **An MCP (Model Context Protocol) server exposing Malawi's weekly financial
> market data as structured, queryable tools for AI assistants. Built for
> advisory firms like [Bridgepath Capital](https://www.bridgepathcapitalmw.com)
> whose analysts use ChatGPT, Claude, and Copilot to answer institutional-grade
> financial questions over a longitudinal dataset of weekly market updates.**

Built with the **official [.NET MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)**
(`ModelContextProtocol` v1.1.0), maintained in collaboration with Microsoft.

---

## Table of Contents

- [What Data Is Indexed](#what-data-is-indexed)
- [Architecture](#architecture)
- [The 16 Tools](#the-16-tools)
  - [Coverage Matrix](#coverage-matrix)
  - [Honest Gaps](#honest-gaps)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Configuration](#configuration)
- [Ingestion Pipeline](#ingestion-pipeline)
- [Running the Server](#running-the-server)
- [Connecting to AI Clients](#connecting-to-ai-clients)
  - [ChatGPT](#chatgpt-recommended)
  - [Claude Desktop](#claude-desktop)
  - [Claude.ai](#claudeai)
  - [GitHub Copilot in VS Code](#github-copilot-in-vs-code)
  - [Cursor](#cursor)
- [Data Schema](#data-schema)
- [Contributing](#contributing)
- [Roadmap](#roadmap)

---

## What Data Is Indexed

**Source**: Bridgepath Capital weekly financial market updates (PDF, every Friday).
**Cadence**: 52 reports per year.
**History target**: Full backfill to earliest available report.

Each weekly PDF contains four data zones, each handled differently at ingestion:

| Zone | Pages | Content | Ingestion Method |
|---|---|---|---|
| **Narrative** | 1–2 | Market events, policy news, corporate announcements | Docling → BM25 index + entity tagger → `market_events` table |
| **Equity market** | 3 | MASI/DSI/FSI indices, 16 stock prices, value traded | Docling table extractor → `financial_indicators` |
| **Govt securities** | 4 | TB auction applied/awarded per tenor, yield curve 8 points | Docling table extractor → `financial_indicators` + `auction_events` |
| **Appendix** | 5 | 13-month rolling history: FX, inflation, interest rates, yields, returns | Docling structured extractor → TimescaleDB hypertable (~40 rows per PDF) |

### Indicators Tracked (Appendix)

| Category | Indicators |
|---|---|
| **Exchange Rates** | MK/USD, MK/GBP, MK/EUR, MK/ZAR |
| **FX Reserves** | Total Reserves (USD mn) |
| **Inflation** | Headline CPI, Food CPI, Non-food CPI |
| **Interest Rates** | MPR, Interbank Rate (overnight), Lombard Rate, Commercial Bank Reference Rate |
| **Govt Securities Yields** | 91-day TB, 182-day TB, 364-day TB, 2-yr TN, 3-yr TN, 5-yr TN, 7-yr TN, 10-yr TN |
| **Equity Index Returns** | MASI YTD, DSI YTD, FSI YTD |
| **Stock Prices** | AIRTEL, BHL, FDHB, FMBCH, ICON, ILLOVO, MPICO, NBM, NBS, NICO, NITL, OMU, PCL, STANDARD, SUNBIRD, TNM |

---

## Architecture

```
┌───────────────────────────────────────────────────────────┐
│                   INGESTION PIPELINE                       │
│                                                            │
│  Weekly PDFs (dropped into /data/raw/weekly/)             │
│       │                                                    │
│       ├── Docling ──────────► Text chunks (narrative)     │
│       │                       Tables (prices, yields)      │
│       │                              │                     │
│       │   ┌───────────────────────────┼────────────────┐   │
│       │   ▼                           ▼                │   │
│       │ BM25 Index             TimescaleDB             │   │
│       │ + Entity Tagger        financial_indicators     │   │
│       │ → market_events        auction_events           │   │
│       │                                                 │   │
│       └── APScheduler Watcher (auto-ingest on drop) ───┘   │
└───────────────────────────────────────────────────────────┘
                        │
┌───────────────────────────────────────────────────────────┐
│              .NET MCP SERVER (C#, ASP.NET Core)           │
│                                                            │
│  MarketDataTools      SignalTools      EquityTools         │
│  ─────────────────    ──────────────   ─────────────────   │
│  GetLatestSnapshot    DetectSignals    GetStockHistory      │
│  QueryIndicators      ComparePeriods   GetIndexDivergence   │
│  ComputeRealRate      ComputeSpread    GetLiquidityProfile  │
│  GetYieldCurve                                             │
│                                                            │
│  NarrativeTools            SearchTools (ChatGPT DR)        │
│  ──────────────────────    ──────────────────────────      │
│  GetMarketEvents           Search  ← required for          │
│  SearchByEntity            Fetch   ← Deep Research mode    │
│  GetCorporateActions                                       │
│  GetAuctionHistory                                         │
│                                                            │
│  Transport: HTTP/HTTPS (production) | stdio (local dev)    │
└───────────────────────────────────────────────────────────┘
                        │  MCP protocol (HTTP)
     ┌──────────────────┼──────────────────┬──────────────┐
     ▼                  ▼                  ▼              ▼
  ChatGPT          Claude.ai       GitHub Copilot      Cursor
  (web + mobile)   (web)           (VS Code)
```

---

## The 16 Tools

Tools are grouped into five `[McpServerToolType]` classes. All tools use
constructor-injected repository/service interfaces — independently testable
and swappable.

> **Note on `Search` and `Fetch`**: These two tools are required by ChatGPT's
> Deep Research mode, which is the most powerful way analysts will use this
> server. Without them, Deep Research rejects the server entirely. They are
> documented separately in the [SearchTools](#searchtools--chatgpt-deep-research)
> section below.

---

### `MarketDataTools` — Core time-series retrieval and computed analytics

---

#### `GetLatestSnapshot`
Returns the most recent week's complete snapshot of all ~40 financial
indicators in a single call.

**Returns**: `week_ending` and all indicators grouped by category (exchange
rates, yields, inflation, interest rates, index returns).

**Typical use**: *"What are current market conditions?"* — the first call
an AI makes for any context-setting question.

---

#### `QueryIndicators`
Raw time-series query for one or more named indicators over a date range.
The foundational retrieval tool — other computed tools call this internally.

**Parameters**:
- `indicators` — list of exact indicator names (see full list in
  [Data Schema](#data-schema))
- `startDate` — `yyyy-MM-dd`
- `endDate` — optional, defaults to latest available

**Returns**: Weekly observations per indicator, ordered by date ascending.

**Typical use**: *"Show me the MPR for the last 12 months"*,
*"What has MK/USD done since June 2025?"*

---

#### `ComputeRealRate`
Computed tool. Subtracts concurrent inflation from a nominal yield to return
the real rate of return series. Runs server-side in typed C# — does not
require the AI to mentally compute the subtraction across two raw series.

**Parameters**:
- `yieldIndicator` — e.g. `"364-day TB"`, `"5-yr TN"`,
  `"Commercial Bank Reference Rate"`
- `inflationMeasure` — `"Headline CPI"` (default), `"Food CPI"`,
  `"Non-food CPI"`
- `weeks` — history length, defaults to 52

**Returns**: Weekly series of `{ nominal_rate, inflation, real_rate }`.

**Typical use**: *"Is the 364-day TB offering a positive real return?"*
As of 20 Feb 2026: 18% yield minus 24.9% inflation = **−6.9% real rate**.

---

#### `GetYieldCurveSnapshot`
Returns all 8 points of the government securities yield curve (91-day TB
through 10-year TN) for a given week as a single structured object, with
automatic curve shape classification.

**Parameters**:
- `weekEnding` — defaults to latest available
- `compareToDate` — optional second date; returns both curves side-by-side
  with basis point differences per tenor

**Shape classifications**: `normal`, `flat`, `inverted`, `humped`.

**Returns**: `{ week_ending, shape, points: [{tenor, yield}], comparison?: [...] }`

**Typical use**: *"Has the yield curve shape changed since last year?"*,
*"Show me the yield curve as of 6 February 2026 vs 20 February 2026"*

---

### `SignalTools` — Anomaly detection and period comparison

---

#### `DetectMarketSignals`
Proactively scans the database for significant anomalies without requiring
an analyst to know what to look for. This is the tool that flags the
6 Feb 2026 full TB bid rejection as a precursor to the 20 Feb yield
compression.

**Parameters**:
- `startDate` — defaults to 52 weeks ago
- `signalType` — filter: `"auction_rejection"`, `"masi_move"`,
  `"yield_shift"`, `"fx_move"`, `"oversubscription"`. Null returns all.
- `minSeverity` — 1–5 threshold (1 = any movement, 5 = extreme only).
  Defaults to 2.

**Signal types detected**:
- `auction_rejection` — any week where TB awarded = 0 on any tenor
- `masi_move` — MASI weekly change exceeds ±5% (configurable)
- `yield_shift` — any single-tenor yield moves >100bps week-on-week
- `fx_move` — MK/USD moves >1% in a week
- `oversubscription` — applied/awarded ratio exceeds 5× on any tenor

**Returns**: Chronological list of `{ date, signal_type, severity,
indicator, value_before, value_after, delta }`.

**Typical use**: *"What were the significant market stress events in 2025?"*,
*"Were there early warning signals before the February 2026 yield
compression?"*

---

#### `ComparePeriods`
Takes any single indicator and two time points or ranges, returns delta,
percentage change, and trend direction. Handles the entire class of
"how does X compare to a year ago" questions as a single deterministic call.

**Parameters**:
- `indicator` — exact indicator name
- `periodA` — single date `"yyyy-MM-dd"` or range `"yyyy-MM-dd:yyyy-MM-dd"`
  (range uses period average)
- `periodB` — same format

**Returns**: `{ period_a_value, period_b_value, absolute_delta, pct_change,
trend }` where trend is `"improving"`, `"deteriorating"`, or `"stable"`.

**Typical use**: *"How does MASI YTD compare to the same point last year?"*

---

#### `ComputeSpread`
Returns the spread (difference) between two indicators at the same weekly
time points. Handles spread analysis without requiring the AI to call
`QueryIndicators` twice and subtract mentally.

Closes three specific analytical gaps:
- **Monetary policy transmission**: MPR minus Commercial Bank Reference Rate
- **Inflation divergence**: Food CPI minus Non-food CPI
- **Term premium**: 10-yr TN yield minus 91-day TB yield

**Parameters**:
- `indicatorA` — the minuend (e.g. `"Commercial Bank Reference Rate"`)
- `indicatorB` — the subtrahend (e.g. `"MPR"`)
- `weeks` — history length, defaults to 52
- `label` — optional label for the spread in output

**Returns**: Weekly series of `{ week_ending, indicator_a, indicator_b,
spread, spread_direction }` where `spread_direction` is `"widening"`,
`"narrowing"`, or `"stable"` relative to prior week.

**Typical use**: *"Is the rate cut transmission happening — is the gap
between the commercial bank rate and the MPR narrowing?"*

---

### `EquityTools` — Malawi Stock Exchange data

---

#### `GetStockHistory`
Closing prices and week-on-week percentage changes for any or all of the
16 MSE-listed equities over a rolling window.

**Available tickers**: AIRTEL, BHL, FDHB, FMBCH, ICON, ILLOVO, MPICO,
NBM, NBS, NICO, NITL, OMU, PCL, STANDARD, SUNBIRD, TNM.

**Parameters**:
- `tickers` — list of symbols; null or empty returns all 16
- `weeks` — defaults to 8

**Returns**: Per ticker: `{ symbol, closing_price, prior_price,
week_on_week_pct, week_ending }` ordered by date ascending.

**Typical use**: *"Which stocks declined this week?"*

---

#### `GetIndexDivergence`
Returns MASI, DSI, and FSI year-to-date returns together for a date range,
with the FSI–MASI spread computed per week.

The FSI reaching 503% YTD in November 2025 while MASI was at 260% is a
major signal — financial sector stocks massively outperforming the broader
market. This tool surfaces that pattern directly.

**Parameters**:
- `startDate` — defaults to start of current calendar year
- `endDate` — defaults to latest available

**Returns**: Weekly series of `{ week_ending, masi_ytd, dsi_ytd, fsi_ytd,
fsi_masi_spread, fsi_dsi_spread }`.

**Typical use**: *"Is the financial sector still outperforming the broader
market?"*

---

#### `GetLiquidityProfile`
Trading volume and market liquidity concentration. In the week ending
6 Feb 2026, STANDARD alone accounted for 44% of total MSE turnover — a
significant concentration signal invisible to price-only analysis.

**Parameters**:
- `weeks` — aggregation window, defaults to 4 (monthly view)
- `topN` — return top N stocks by volume, defaults to 5

**Returns**: `{ period, total_turnover_mk, top_stocks: [{symbol,
value_traded, pct_of_total}], concentration_ratio_top3 }`.

**Typical use**: *"Which stocks are actually tradeable right now?"*

---

### `NarrativeTools` — Weekly event intelligence

---

#### `GetMarketEvents`
Full-text BM25 search over the numbered narrative items from the
*"What happened this week"* section of each weekly update.

**Parameters**:
- `query` — natural language search query
- `weekEnding` — filter to specific week, optional
- `maxResults` — defaults to 10

**Returns**: Ranked list of `{ week_ending, item_number, headline,
full_text, entities, event_type, source_citation }`.

**Typical use**: *"What has been reported about the MOMA power
interconnector?"*

---

#### `SearchByEntity`
Entity-based lookup using the structured entity tags applied at ingestion.
More precise than BM25 for known named entities — avoids false positives
from incidental mentions.

**Parameters**:
- `entity` — exact entity tag (e.g. `"RBM"`, `"NBM"`, `"World Bank"`,
  `"Ministry of Finance"`, `"AIRTEL"`)
- `startDate`, `endDate` — optional date range filters

**Returns**: All narrative items where the entity was explicitly tagged,
with full text and source citations.

**Typical use**: *"Show me everything reported about the World Bank's
position on Malawi's fiscal situation in the last 3 months"*

---

#### `GetCorporateActions`
Structured corporate action events filtered by
`event_type = 'corporate_action'`. More reliable than full-text search
for portfolio monitoring — tagged at ingestion by type, not found by
keyword.

**Action types**: `dividend`, `profit_warning`, `earnings_guidance`,
`rights_issue`, `board_change`.

**Parameters**:
- `ticker` — filter to specific company; null returns all
- `weeks` — defaults to 12
- `actionType` — filter by action type; null returns all

**Returns**: `{ week_ending, company, ticker, action_type, summary,
source_citation }`.

**Typical use**: *"What dividend announcements have there been this
quarter?"*, *"Has ILLOVO issued any profit guidance recently?"*

---

#### `GetAuctionHistory`
Treasury Bill auction history: applied vs awarded per tenor,
oversubscription ratios, full bid rejection flags.

**Parameters**:
- `tenor` — `"91-day"`, `"182-day"`, `"364-day"`, or null for all
- `rejectionsOnly` — `true` returns only full rejection weeks
- `weeks` — defaults to 12

**Returns**: `{ week_ending, tenor, applied_mk_bn, awarded_mk_bn,
oversubscription_ratio, is_full_rejection }`.

**Typical use**: *"When has the RBM rejected all bids?"*,
*"What is the trend in TB oversubscription ratios?"*

---

### `SearchTools` — ChatGPT Deep Research

These two tools exist specifically to satisfy ChatGPT's Deep Research
mode requirements. Deep Research rejects any MCP server that does not
expose both a `search` and a `fetch` tool. In Deep Research, only these
two tools are called — the AI uses them to systematically retrieve and
synthesise information across multiple queries.

If you are only using Claude or Copilot, these tools are not required
but do no harm.

---

#### `Search`
Entry point for Deep Research queries. Accepts a natural language query
and returns matching record IDs from both the `financial_indicators`
time-series and `market_events` narrative datasets.

**Parameters**:
- `query` — natural language search query

**Returns**: `{ ids: [string] }` — a list of record identifiers that
Deep Research will then retrieve individually using `Fetch`.

**Note**: The `ids` format encodes the source type and primary key:
e.g. `"indicator:MK/USD:2026-02-20"` or `"event:uuid-here"`.

---

#### `Fetch`
Retrieves a complete record by ID. Called by Deep Research after `Search`
to get full content for each matching result.

**Parameters**:
- `id` — a record identifier as returned by `Search`

**Returns**: Full record content including all fields, metadata, and
source citation. Format varies by record type (indicator vs event).

**Typical Deep Research use**: *"Compile a comprehensive analysis of
Malawi's monetary policy stance over the last 12 months, including TB
auction behaviour, yield movements, and relevant policy announcements."*

---

### Coverage Matrix

| Analyst Question | Primary Tool(s) | Coverage |
|---|---|---|
| Yield curve movement over 8 weeks | `GetYieldCurveSnapshot` | ✅ Full |
| TB bid rejections + consequences | `GetAuctionHistory` + `DetectMarketSignals` | ✅ Full |
| Real rate of return on T-bills | `ComputeRealRate` | ✅ Full |
| MPR transmission to bank lending rates | `ComputeSpread` | ✅ Full |
| TB application volume trend | `GetAuctionHistory` | ✅ Full |
| Stock cumulative declines over N weeks | `GetStockHistory` + `ComparePeriods` | ✅ Full |
| MASI YTD vs prior year same week | `GetIndexDivergence` + `ComparePeriods` | ✅ Full |
| Most consistently traded stock | `GetLiquidityProfile` | ✅ Full |
| FSI vs MASI vs DSI divergence | `GetIndexDivergence` | ✅ Full |
| MK/USD stability and managed float signals | `QueryIndicators` + `DetectMarketSignals` | ✅ Full |
| Food vs non-food inflation divergence | `ComputeSpread` | ✅ Full |
| Economic events this week | `GetMarketEvents` | ✅ Full |
| Corporate announcements last N weeks | `GetCorporateActions` | ✅ Full |
| Trade/energy/infrastructure news | `GetMarketEvents` + `SearchByEntity` | ✅ Full |
| Cross-reference news with market data | `GetMarketEvents` → `QueryIndicators` | ✅ Full |
| TB oversubscription ratio trend | `GetAuctionHistory` | ✅ Full |
| Term premium (short vs long yield) | `ComputeSpread` | ✅ Full |
| Stress events + same-week stock moves | `DetectMarketSignals` + `GetStockHistory` | ✅ Full |
| Comprehensive multi-question research | `Search` + `Fetch` (Deep Research) | ✅ Full |

**Coverage: ~90–92% of typical analyst questions.**

---

### Honest Gaps

Two question types are not covered — this is an ingestion problem, not a
tool problem. The weekly Appendix table does not include trade balance data.

| Question | Gap | Resolution |
|---|---|---|
| *"What % of imports does tobacco export revenue cover?"* | Export/import commodity breakdown is in narrative text only — not structured | Add `TradeBalanceExtractor` to ingestion + `GetTradeBalance` tool (v0.4) |
| *"What is the FX reserve import cover in months?"* | Monthly import totals not in Appendix (FX reserves are) | Same: `trade_flows` table + tool (v0.4) |

---

## Project Structure

```
MalawiFinancialMcp/
│
├── MalawiFinancialMcp.csproj
│
├── Program.cs                          # Host builder, DI wiring, app.MapMcp()
│
├── Tools/
│   ├── MarketDataTools.cs             # GetLatestSnapshot, QueryIndicators,
│   │                                  # ComputeRealRate, GetYieldCurveSnapshot
│   ├── SignalTools.cs                 # DetectMarketSignals, ComparePeriods,
│   │                                  # ComputeSpread
│   ├── EquityTools.cs                 # GetStockHistory, GetIndexDivergence,
│   │                                  # GetLiquidityProfile
│   ├── NarrativeTools.cs             # GetMarketEvents, SearchByEntity,
│   │                                  # GetCorporateActions, GetAuctionHistory
│   └── SearchTools.cs                # Search, Fetch (ChatGPT Deep Research)
│
├── Data/
│   ├── MalawiDbContext.cs
│   ├── Repositories/
│   │   ├── IIndicatorRepository.cs
│   │   ├── IndicatorRepository.cs
│   │   ├── IMarketEventRepository.cs
│   │   ├── MarketEventRepository.cs
│   │   ├── IAuctionRepository.cs
│   │   └── AuctionRepository.cs
│   └── Models/
│       ├── FinancialIndicator.cs
│       ├── MarketEvent.cs
│       └── AuctionEvent.cs
│
├── Services/
│   ├── ISignalDetector.cs
│   ├── SignalDetector.cs
│   ├── INarrativeSearchService.cs
│   └── BM25NarrativeSearchService.cs
│
├── Ingestion/
│   ├── WeeklyPdfIngester.cs
│   ├── DoclingClient.cs
│   ├── AppendixExtractor.cs
│   ├── NarrativeExtractor.cs
│   ├── EntityTagger.cs
│   ├── AuctionExtractor.cs
│   └── FileWatcher.cs
│
├── Migrations/
│   └── 001_InitSchema.sql
│
├── Tests/
│   MalawiFinancialMcp.Tests/
│   ├── Tools/
│   │   ├── MarketDataToolsTests.cs
│   │   ├── SignalToolsTests.cs
│   │   ├── EquityToolsTests.cs
│   │   ├── NarrativeToolsTests.cs
│   │   └── SearchToolsTests.cs
│   ├── Ingestion/
│   │   ├── AppendixExtractorTests.cs  # Critical: numerical precision
│   │   └── EntityTaggerTests.cs
│   └── Repositories/
│       └── IndicatorRepositoryTests.cs
│
├── docker-compose.yml
├── appsettings.json
├── appsettings.Development.json
└── README.md
```

---

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download) or later
- [Docker](https://www.docker.com/) and Docker Compose
- A running [Docling](https://github.com/DS4SD/docling) instance
  (included in `docker-compose.yml`)

---

## Installation

### 1. Clone

```bash
git clone https://github.com/your-org/malawi-financial-mcp.git
cd malawi-financial-mcp
```

### 2. Start Docker Services

```bash
docker compose up -d
```

Starts **TimescaleDB** on `:5432` and **Docling** on `:8080`.

### 3. Initialise Database

```bash
psql postgresql://postgres:malawi_agent@localhost:5432/malawi_financial \
  -f Migrations/001_InitSchema.sql
```

### 4. Build

```bash
dotnet restore && dotnet build
```

---

## Configuration

```json
// appsettings.json
{
  "ConnectionStrings": {
    "TimescaleDB": "Host=localhost;Port=5432;Database=malawi_financial;Username=postgres;Password=malawi_agent"
  },
  "Docling": {
    "BaseUrl": "http://localhost:8080"
  },
  "Ingestion": {
    "WatchDirectory": "./data/raw/weekly",
    "AutoIngestEnabled": true
  },
  "SignalDetector": {
    "MasiMoveThresholdPct": 5.0,
    "YieldShiftThresholdBps": 100,
    "FxMoveThresholdPct": 1.0,
    "OversubscriptionThreshold": 5.0
  }
}
```

---

## Ingestion Pipeline

### File Naming Convention

```
bridgepath_market_update_YYYY_MM_DD.pdf
```

Drop files into `./data/raw/weekly/`. The `week_ending` date is parsed
from the filename and applied to every record written.

### Manual Ingestion

```bash
# Single file
dotnet run -- ingest --file ./data/raw/weekly/bridgepath_market_update_2026_02_20.pdf

# Full backfill
dotnet run -- ingest --directory ./data/raw/weekly --backfill
```

### Automatic Ingestion

When `AutoIngestEnabled: true`, any PDF dropped into the watch directory
is automatically ingested within 60 seconds.

### Ingestion Stages

| Stage | Class | Input | Output |
|---|---|---|---|
| 1. Parse | `DoclingClient` | PDF bytes | Structured JSON |
| 2. Extract narrative | `NarrativeExtractor` | Pages 1–2 | `market_events` rows |
| 3. Tag entities | `EntityTagger` | Narrative text | JSONB tags on each event |
| 4. Extract Appendix | `AppendixExtractor` | Page 5 | `financial_indicators` rows (~40) |
| 5. Extract auction data | `AuctionExtractor` | Page 4 | `auction_events` rows |
| 6. Extract equity data | `AppendixExtractor` | Page 3 + Appendix | `financial_indicators` rows |
| 7. Build BM25 index | `BM25NarrativeSearchService` | `market_events` table | In-memory BM25 (rebuilt on startup) |

> ⚠️ **Numerical precision is critical.** `AppendixExtractorTests.cs`
> enforces a <0.01% error tolerance on all extracted values.

---

## Running the Server

### stdio (local development)

```bash
dotnet run -- --transport stdio
```

### HTTP (production)

```bash
dotnet run -- --transport http --port 5000
```

For production deployments, the server must be reachable over **HTTPS**.
Use a reverse proxy (nginx, Caddy) or deploy to a cloud service that
provides TLS termination.

For local development exposed to the internet (required for ChatGPT
and Claude.ai testing), use [ngrok](https://ngrok.com/) or
[Cloudflare Tunnel](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/):

```bash
# Expose local server to the internet for testing
ngrok http 5000
# → Forwarding: https://abc123.ngrok.app → localhost:5000

# or with Cloudflare Tunnel
cloudflared tunnel --url http://localhost:5000
```

---

## Connecting to AI Clients

The server runs on HTTP transport and works with any MCP-compatible client.
Your public endpoint will be `https://your-server/mcp`.

---

### ChatGPT (Recommended)

ChatGPT is the primary intended client and supports the server in two modes:

**Chat Mode** — interactive Q&A using all 16 tools.

**Deep Research Mode** — systematic multi-step research using the `Search`
and `Fetch` tools. This is the highest-value mode for analysts compiling
comprehensive briefings across multiple weeks of data.

#### Requirements

- ChatGPT **Pro, Team, Enterprise, or Education** plan
- Developer Mode enabled (Settings → Connectors → Advanced → Developer Mode)
- Server accessible over **HTTPS**

#### Setup

1. Ensure your server is running and reachable over HTTPS
2. In ChatGPT, go to **Settings → Connectors → Create**
3. Fill in the connector form:
   - **Name**: Malawi Financial Intelligence
   - **Description**: Weekly financial market data for Malawi — yields,
     FX rates, equity prices, inflation, and market events
   - **Connector URL**: `https://your-server/mcp`
4. Click **Create** — ChatGPT will display the list of available tools
   if the connection succeeds
5. To use in chat: click the **+** icon in the message bar → **More** →
   **Developer Mode** → enable your connector

#### Deep Research

In a new chat, start a message with *"Use deep research to..."* and
ChatGPT will use the `Search` and `Fetch` tools to compile a
comprehensive, cited analysis. Example:

> *"Use deep research to compile a briefing on Malawi's monetary policy
> stance over the last 12 months, including TB auction behaviour, yield
> movements, and relevant RBM policy announcements."*

#### Refreshing Tools

After updating tool definitions or descriptions, go to
**Settings → Connectors**, click your connector, and select **Refresh**
to pull the updated tool list.

---

### Claude Desktop

For local use on macOS or Windows.

1. Run the server in stdio mode (no HTTPS required):

```bash
dotnet run -- --transport stdio
```

2. Edit `claude_desktop_config.json`:
   - **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
   - **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "malawi-financial": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/absolute/path/to/MalawiFinancialMcp",
        "--",
        "--transport",
        "stdio"
      ]
    }
  }
}
```

3. Restart Claude Desktop — the tools will appear automatically.

---

### Claude.ai

For web-based use via Claude.ai (requires a published HTTPS endpoint).

1. Go to **Claude.ai → Settings → Integrations → Add MCP Server**
2. Enter your server URL: `https://your-server/mcp`
3. The tools will be available in any new conversation

---

### GitHub Copilot in VS Code

1. Open (or create) `.vscode/mcp.json` in your project:

```json
{
  "servers": {
    "malawi-financial": {
      "type": "http",
      "url": "https://your-server/mcp"
    }
  }
}
```

2. A **Start** button appears at the top of the file — click it to
   connect
3. Open Copilot Chat (`Ctrl+Alt+I`), switch to **Agent Mode**, and click
   the tools icon to verify the server is listed
4. Use tools directly in chat: `#GetLatestSnapshot` or just ask naturally

---

### Cursor

Cursor uses the same configuration format as VS Code. Add to
`~/.cursor/mcp.json` (user-wide) or `.cursor/mcp.json` (project-level):

```json
{
  "mcpServers": {
    "malawi-financial": {
      "type": "http",
      "url": "https://your-server/mcp"
    }
  }
}
```

Restart Cursor — tools appear automatically in the Composer and Chat.

---

## Data Schema

### `financial_indicators` (TimescaleDB hypertable)

| Column | Type | Notes |
|---|---|---|
| `time` | `TIMESTAMPTZ` | Hypertable partition key |
| `week_ending` | `DATE` | Source week |
| `category` | `TEXT` | `exchange_rate`, `yield`, `inflation`, `interest_rate`, `stock_price`, `stock_return`, `volume`, `reserve` |
| `indicator` | `TEXT` | Exact indicator name |
| `value` | `NUMERIC(18,6)` | Preserves decimal precision |
| `prior_value` | `NUMERIC(18,6)` | Auto-populated from prior week on insert |
| `week_delta` | `NUMERIC(18,6)` | `value - prior_value` |
| `source_doc` | `TEXT` | Source PDF filename |

### `market_events`

| Column | Type | Notes |
|---|---|---|
| `id` | `UUID` | Primary key |
| `week_ending` | `DATE` | |
| `item_number` | `INT` | Position in the weekly list |
| `headline` | `TEXT` | Auto-extracted first sentence |
| `full_text` | `TEXT` | Complete narrative item |
| `entities` | `JSONB` | `{ tickers: [], institutions: [], ministries: [], event_type: "" }` |
| `event_type` | `TEXT` | `corporate_action`, `policy`, `macro`, `infrastructure`, `trade`, `health`, `social` |
| `source_citation` | `TEXT` | e.g. `"The Nation, 20 February 2026"` |

### `auction_events`

| Column | Type | Notes |
|---|---|---|
| `week_ending` | `DATE` | |
| `tenor` | `TEXT` | `"91-day"`, `"182-day"`, `"364-day"` |
| `applied_mk_bn` | `NUMERIC` | Total bids received |
| `awarded_mk_bn` | `NUMERIC` | Total awarded (0 = full rejection) |
| `oversubscription_ratio` | `NUMERIC` | `applied / awarded`; NULL if awarded = 0 |
| `is_full_rejection` | `BOOLEAN` | `awarded_mk_bn = 0` |

---

## Contributing

```bash
dotnet restore
dotnet test
```

### Contribution Areas

| Area | Description | Priority |
|---|---|---|
| **Historical backfill** | Ingest all available historical weekly PDFs | High |
| **`AppendixExtractor` hardening** | Edge cases: N/A values, merged cells, footnotes | High |
| **Entity tagger expansion** | Add more institution/ministry entity tags | Medium |
| **`TradeBalanceExtractor`** | Parse trade data from narrative → `trade_flows` table | Medium |
| **`GetTradeBalance` tool** | New tool over `trade_flows` (closes known coverage gaps) | Medium |
| **Signal threshold tuning** | Calibrate `DetectMarketSignals` against historical data | Medium |
| **BM25 index persistence** | Currently rebuilt on startup; persist to disk | Low |

### Commit Convention

```
feat(tools): add ComputeSpread for transmission gap analysis
fix(ingestion): handle N/A values in AppendixExtractor
data(weekly): backfill market updates Jan 2024–Feb 2026
test(tools): add precision tests for ComputeRealRate
```

### Pull Request Checklist

- [ ] `dotnet test` passes
- [ ] `AppendixExtractorTests` pass with <0.01% numerical error tolerance
- [ ] Tool `[Description]` attributes are complete and accurate —
  AI clients use these to decide which tool to call
- [ ] New tools have a corresponding repository interface + mock
- [ ] `CHANGELOG.md` updated

---

## Roadmap

- [ ] **v0.1** — 16 tools + TimescaleDB schema + manual ingestion CLI
- [ ] **v0.2** — File watcher auto-ingestion + entity tagger
- [ ] **v0.3** — Historical backfill of all available weekly PDFs
- [ ] **v0.4** — `TradeBalanceExtractor` + `GetTradeBalance` tool
- [ ] **v0.5** — HTTPS deployment guide + OAuth for ChatGPT
- [ ] **v1.0** — Production hardening: auth, rate limiting,
  TimescaleDB replication

---

## Acknowledgements

- [Bridgepath Capital](https://www.bridgepathcapitalmw.com) for weekly
  research publications
- [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk)
  — official C# MCP SDK, maintained in collaboration with Microsoft
- [Docling](https://github.com/DS4SD/docling) by IBM Research
- [TimescaleDB](https://www.timescale.com/)

---

## License

MIT — see [LICENSE](./LICENSE) for details.

> **Disclaimer**: For analytical and research purposes only. All outputs
> should be independently verified before use in investment or advisory
> decisions. Data sourced from Malawi Government, Reserve Bank of Malawi,
> Malawi Stock Exchange, and Bridgepath Capital publications.
