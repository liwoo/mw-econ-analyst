# MCP Server — Tool Reference

> Full documentation for the 22 MCP tools exposed by the Malawi Financial
> Intelligence server. For setup, installation, and client connection
> instructions see the main [README](./README.md).

---

## Table of Contents

- [MarketDataTools](#marketdatatools)
- [SignalTools](#signaltools)
- [EquityTools](#equitytools)
- [CommodityTools](#commoditytools)
- [CorrelationTools](#correlationtools)
- [NarrativeTools](#narrativetools)
- [BankingTools](#bankingtools)
- [SearchTools](#searchtools--chatgpt-deep-research)
- [Coverage Matrix](#coverage-matrix)
- [Statistical Guardrails](#statistical-guardrails)
- [Honest Gaps](#honest-gaps)
- [Data Schema](#data-schema)

---

## The 22 Tools

Tools are grouped into seven `[McpServerToolType]` classes.

> **Note on `Search` and `Fetch`**: Required by ChatGPT Deep Research
> mode. Without them, the server is rejected in that mode. Documented
> in [SearchTools](#searchtools--chatgpt-deep-research) below.

---

### `MarketDataTools`

#### `GetLatestSnapshot`
Returns the most recent week's complete snapshot of all ~40 financial
indicators from the weekly Appendix in a single call.

**Typical use**: *"What are current market conditions?"*

---

#### `QueryIndicators`
Raw time-series for one or more named indicators over a date range.
The foundational retrieval tool used internally by computed tools.

**Parameters**: `indicators` (list), `startDate`, `endDate` (optional)

**Returns**: Weekly observations per indicator, ascending by date.

**Typical use**: *"Show me the MPR for the last 12 months"*

---

#### `ComputeRealRate`
Subtracts concurrent inflation from a nominal yield server-side.
Returns a weekly series of `{ nominal_rate, inflation, real_rate }`.

**Parameters**: `yieldIndicator`, `inflationMeasure` (default: Headline
CPI), `weeks` (default: 52)

**Typical use**: *"Is the 364-day TB giving a positive real return?"*
As of Feb 2026: 17.90% yield minus 24.9% inflation = **−7.0% real rate.**

---

#### `GetYieldCurveSnapshot`
Returns all 8 points (91-day TB → 10-year TN) for a given week with
automatic curve shape classification: `normal`, `flat`, `inverted`,
`humped`. Optionally compares two dates side-by-side with basis point
differences per tenor.

**Parameters**: `weekEnding` (default: latest), `compareToDate`
(optional)

**Typical use**: *"How has the yield curve shape changed since January?"*

---

### `SignalTools`

#### `DetectMarketSignals`
Proactively scans for anomalies: TB bid rejections (awarded = 0),
MASI weekly moves >±5%, yield shifts >100bps week-on-week, FX moves
>1%, TB oversubscription ratios >5×.

**Parameters**: `startDate`, `signalType`, `minSeverity` (1–5)

**Returns**: `{ date, signal_type, severity, indicator, value_before,
value_after, delta }`

**Typical use**: *"What stress events preceded the Feb 2026 yield
compression?"*

---

#### `ComparePeriods`
Point-in-time or period-average comparison for any single indicator.
Returns delta, % change, and trend direction.

**Parameters**: `indicator`, `periodA`, `periodB` (each a date or
`"yyyy-MM-dd:yyyy-MM-dd"` range)

**Typical use**: *"How does MASI YTD compare to the same point last
year?"*

---

#### `ComputeSpread`
Weekly spread (difference) between any two indicators. Deterministic
server-side arithmetic — does not require the AI to subtract two raw
series mentally.

Closes three specific gaps: MPR vs commercial bank reference rate
(transmission), Food CPI vs Non-food CPI (divergence), 10-yr TN vs
91-day TB (term premium).

**Parameters**: `indicatorA`, `indicatorB`, `weeks` (default: 52),
`label` (optional)

**Returns**: Weekly series of `{ week_ending, indicator_a, indicator_b,
spread, spread_direction }` where `spread_direction` is `"widening"`,
`"narrowing"`, or `"stable"`.

**Typical use**: *"Is the rate cut transmission happening — is the gap
between commercial bank rate and MPR narrowing?"*

---

### `EquityTools`

#### `GetStockHistory`
Closing prices and week-on-week % changes for any or all 16 MSE
equities over a rolling window.

**Parameters**: `tickers` (null = all 16), `weeks` (default: 8)

**Typical use**: *"Which stocks declined this week?"*

---

#### `GetIndexDivergence`
MASI, DSI, and FSI YTD returns together with FSI–MASI and FSI–DSI
spreads computed per week. The FSI reaching 503% YTD in November 2025
while MASI was at 260% — this tool surfaces that directly.

**Parameters**: `startDate`, `endDate`

**Typical use**: *"Is the financial sector still outperforming the
broader market?"*

---

#### `GetLiquidityProfile`
Trading volume concentration — which stocks are actually tradeable vs
technically listed but illiquid. Returns total turnover, top N stocks
by volume, and concentration ratio of top 3.

**Parameters**: `weeks` (default: 4), `topN` (default: 5)

**Typical use**: *"Which stocks are liquid enough to trade in size?"*

---

#### `GetValuationMetrics` ← *New — requires monthly reports*
Returns monthly P/E ratio, P/BV ratio, dividend yield, and market
capitalisation for any or all MSE equities. Enables investment analysis
that price data alone cannot support: detecting expensive vs cheap
stocks, tracking P/E expansion/contraction, identifying derating events.

**Parameters**:
- `tickers` — list of symbols; null returns all 16
- `startMonth` — `"yyyy-MM"` format
- `endMonth` — optional, defaults to latest available
- `metrics` — list of `"pe"`, `"pbv"`, `"div_yield"`, `"market_cap"`;
  null returns all four

**Returns**: Monthly series per ticker of
`{ month, ticker, pe_ratio, pbv_ratio, div_yield_pct, market_cap_mk_bn }`.

**Validation**: Returns null for months where data is unavailable.
Does not interpolate missing valuation data — a gap means a gap.

**Typical use**: *"Is AIRTEL expensive relative to its own history?"*,
*"Which stocks are currently trading below book value?"*,
*"Did FMBCH's P/E expand or contract during its share price rally in
mid-2025?"*

---

### `CommodityTools` ← *New tool class — requires monthly reports*

#### `GetCommodityPrices`
Monthly maize prices (IFPRI) and OPEC oil reference basket prices.
Maize series starts from 2021 (~60 observations); oil from 2020
(~72 observations) — sufficient for meaningful correlation analysis
with inflation and equity data.

**Maize**: National average MK/kg plus Northern, Central, Southern
regional breakdown.

**Oil**: OPEC Reference Basket price in USD/barrel.

**Parameters**:
- `commodity` — `"maize"`, `"oil"`, or `"all"`
- `region` — for maize: `"national"` (default), `"northern"`,
  `"central"`, `"southern"`, `"all"`
- `startMonth` — `"yyyy-MM"` format
- `endMonth` — optional

**Returns**: Monthly series of
`{ month, commodity, region?, value, unit, source }`.

**Typical use**: *"Has the maize price decline in early 2026 fed
through to food inflation?"*, *"How did the February 2026 oil price
rise affect non-food inflation in subsequent months?"*

---

### `CorrelationTools` ← *New tool class*

These two tools compute statistical relationships between any
indicators in the system. Both enforce guardrails against
statistically invalid results — see
[Statistical Guardrails](#statistical-guardrails) below.

---

#### `ComputeCorrelation`
Pearson or Spearman correlation coefficient between any two series
available in the system — including cross-source correlations (e.g.
weekly stock price vs monthly commodity price, monthly P/BV vs weekly
MASI return).

**Parameters**:
- `seriesA` — any indicator name, ticker symbol, or commodity
- `seriesB` — any indicator name, ticker symbol, or commodity
- `startDate` — `"yyyy-MM-dd"`
- `endDate` — optional
- `method` — `"pearson"` (default) or `"spearman"`
- `alignTo` — `"lower_frequency"` (default) or `"higher_frequency"`.
  When correlating weekly and monthly series, the tool resamples to
  monthly by default (taking the last weekly observation per month).

**Returns when valid**:
```json
{
  "series_a": "AIRTEL",
  "series_b": "MK/USD",
  "aligned_frequency": "monthly",
  "observations": 24,
  "start_date": "2024-03-01",
  "end_date": "2026-02-28",
  "correlation": -0.74,
  "strength": "strong",
  "direction": "inverse",
  "p_value": 0.0001,
  "is_significant": true,
  "interpretation": "When MK/USD fell (kwacha strengthened against the dollar), AIRTEL price tended to fall. Consistent with Airtel Africa reporting in USD — a stronger kwacha reduces the MWK value of USD revenues when translated.",
  "warning": null
}
```

**Returns when invalid** (see guardrails):
```json
{
  "series_a": "AIRTEL",
  "series_b": "364-day TB",
  "correlation": null,
  "valid": false,
  "reason": "364-day TB yield was constant at 26.00% for 48 of 52 weeks in the requested period. Correlation with a near-constant series is mathematically undefined.",
  "suggestion": "Use DetectMarketSignals to find yield change dates, then examine AIRTEL price response in surrounding weeks using an event study approach."
}
```

**Typical use**: *"Does the oil price correlate with non-food
inflation?"*, *"Do AIRTEL and TNM move together — is this a
sector-wide story or company-specific?"*

---

#### `ComputeRelativeStrength`
Decomposes a stock's return over a period into market-driven and
stock-specific components. Returns excess return, beta (computed over
a configurable lookback window), and alpha (the residual unexplained
by market movement). The attribution breakdown — market-driven % vs
stock-specific % — is the primary output for investment analysis.

**Parameters**:
- `ticker` — MSE ticker symbol
- `benchmark` — `"MASI"` (default), `"DSI"`, or `"FSI"`
- `startDate` — analysis period start
- `endDate` — analysis period end
- `betaLookbackWeeks` — window for beta estimation, default 52.
  Always longer than the analysis period for stable beta estimates.

**Returns**:
```json
{
  "ticker": "AIRTEL",
  "benchmark": "MASI",
  "analysis_period": { "start": "2025-10-01", "end": "2025-12-31" },
  "beta_estimated_over_weeks": 52,
  "ticker_return_pct": -18.0,
  "benchmark_return_pct": -12.0,
  "excess_return_pct": -6.0,
  "beta": 1.3,
  "alpha_pct": -2.4,
  "attribution": {
    "market_driven_pct": -15.6,
    "stock_specific_pct": -2.4
  },
  "confidence": "medium",
  "interpretation": "AIRTEL underperformed MASI by 6.0pp. Of its -18.0% decline, approximately -15.6% is explained by broad market movement (beta-adjusted). The residual -2.4% alpha suggests stock-specific headwinds beyond market conditions. Investigate using SearchByEntity('AIRTEL') and GetCorporateActions('AIRTEL') for events in this period.",
  "warning": null
}
```

**Confidence levels**:
- `"high"` — >52 weekly observations for beta, >8 weeks analysis period
- `"medium"` — 26–52 weekly observations or 4–8 weeks analysis period
- `"low"` — <26 weekly observations for beta or <4 weeks analysis period

**Typical use**: *"Was AIRTEL's Q4 decline company-specific or was
the whole market selling off?"*, *"How much of FMBCH's 2025 rally
was market-driven vs stock-specific?"*

---

### `NarrativeTools`

#### `GetMarketEvents`
BM25 full-text search over weekly and monthly narrative items.

**Parameters**: `query`, `weekEnding` (optional), `maxResults`
(default: 10)

---

#### `SearchByEntity`
Entity-based lookup using structured tags applied at ingestion.
More precise than BM25 for known named entities.

**Parameters**: `entity`, `startDate`, `endDate`

---

#### `GetCorporateActions`
Structured corporate actions filtered by type: `dividend`,
`profit_warning`, `earnings_guidance`, `rights_issue`, `board_change`.

**Parameters**: `ticker`, `weeks` (default: 12), `actionType`

---

#### `GetAuctionHistory`
TB auction applied vs awarded, oversubscription ratios, full
rejection flags.

**Parameters**: `tenor`, `rejectionsOnly`, `weeks` (default: 12)

---


### `BankingTools` ← *New tool class — requires monthly reports*

#### `GetBankingSectorMetrics`
Returns sector-level banking health indicators extracted from the monthly
economic report. These metrics explain *why* banking stocks trade at
elevated P/E multiples — high ROE justifies premium valuation relative to
non-financial sectors — and flag the structural risk from heavy government
securities exposure crowding out private lending.

**Parameters**:
- `months` — number of months of history, defaults to 12

**Returns**: Monthly series of `{ month, roe_pct, roa_pct, npl_ratio_pct,
govt_sec_exposure, credit_to_private_pct, source_doc }`.

**Current values (Feb 2026)**:
- Return on Equity: **60.9%** — highest in the region
- Return on Assets: **7.7%** — strong profitability
- Non-performing Loans: **4.6%** — improving asset quality
- Govt securities exposure: **High** — crowding out private sector lending

**Typical use**: *"Why do banking stocks trade at 50–57× P/E when other
sectors trade at 15–25×?"*, *"Is the banking sector's govt securities
exposure increasing or decreasing?"*

---

#### `GetTradeBalance`
Returns structured trade flow data parsed from the monthly economic report
narrative: total exports, total imports, trade deficit, export-to-import
ratio, and top commodity categories (tobacco, fertiliser, diesel, petrol).
Closes the gap identified in earlier versions — trade data was present in
narrative text but not structured.

**Parameters**:
- `startMonth` — `"yyyy-MM"` format
- `endMonth` — optional, defaults to latest available
- `category` — filter by `"exports"`, `"imports"`, `"deficit"`, or `"all"`

**Returns**: Monthly series of `{ month, total_exports_usd_mn,
total_imports_usd_mn, trade_deficit_usd_mn, export_import_ratio,
top_exports: [{category, value_usd_mn, pct_of_total}],
top_imports: [{category, value_usd_mn, pct_of_total}] }`.

**Current values (Dec 2025)**:
- Total exports: **$60.9mn** — covers only 18% of imports
- Total imports: **$332.1mn** — fertiliser 13.3%, diesel 13.2%, petrol 10.6%
- Trade deficit: **$271.2mn** — widened from $238.9mn in Nov 2025
- Tobacco share of exports: **61%** — structural concentration risk

**Typical use**: *"Is the trade deficit widening or narrowing?"*,
*"How dependent are imports on fuel vs productive inputs?"*,
*"What % of imports does tobacco export revenue cover?"*

---

### `SearchTools` — ChatGPT Deep Research

Required by ChatGPT Deep Research mode — the server is rejected
without both tools present.

#### `Search`
Returns matching record IDs from `financial_indicators`,
`stock_valuations`, `commodity_prices`, `institutional_forecasts`,
and `market_events` for a natural language query.

#### `Fetch`
Returns the complete record for a given ID as returned by `Search`.

---

### Coverage Matrix

| Question | Tool(s) | Source | Coverage |
|---|---|---|---|
| Yield curve movement over 8 weeks | `GetYieldCurveSnapshot` | Weekly | ✅ |
| TB bid rejections + consequences | `GetAuctionHistory` + `DetectMarketSignals` | Weekly | ✅ |
| Real rate of return on T-bills | `ComputeRealRate` | Weekly | ✅ |
| MPR transmission to bank lending rate | `ComputeSpread` | Weekly | ✅ |
| TB application volume trend | `GetAuctionHistory` | Weekly | ✅ |
| Stock cumulative change over N weeks | `GetStockHistory` + `ComparePeriods` | Weekly | ✅ |
| MASI YTD vs prior year | `GetIndexDivergence` + `ComparePeriods` | Weekly | ✅ |
| Most consistently traded stock | `GetLiquidityProfile` | Weekly | ✅ |
| FSI vs MASI vs DSI divergence | `GetIndexDivergence` | Weekly | ✅ |
| MK/USD stability + managed float signals | `QueryIndicators` + `DetectMarketSignals` | Weekly | ✅ |
| Food vs non-food inflation divergence | `ComputeSpread` | Weekly | ✅ |
| Economic events this week | `GetMarketEvents` | Weekly | ✅ |
| Corporate announcements last N weeks | `GetCorporateActions` | Weekly/Monthly | ✅ |
| Trade/energy/infrastructure news | `GetMarketEvents` + `SearchByEntity` | Weekly/Monthly | ✅ |
| Cross-reference news with market data | `GetMarketEvents` → `QueryIndicators` | Both | ✅ |
| Is AIRTEL expensive vs its own history? | `GetValuationMetrics` | Monthly | ✅ |
| Which stocks trade below book value? | `GetValuationMetrics` | Monthly | ✅ |
| Was a decline a P/E derating or earnings miss? | `GetValuationMetrics` + `GetStockHistory` | Both | ✅ |
| Does oil price predict non-food inflation? | `ComputeCorrelation` + `GetCommodityPrices` | Monthly | ✅ |
| Did maize price decline feed through to food CPI? | `ComputeCorrelation` + `GetCommodityPrices` | Monthly | ✅ |
| Are AIRTEL and TNM correlated (sector vs company)? | `ComputeCorrelation` | Weekly | ✅ |
| Was AIRTEL's Q4 decline market-driven or company-specific? | `ComputeRelativeStrength` | Weekly | ✅ |
| What do EIU/WB project for Malawi's macro? | `GetMarketEvents` / `QueryIndicators` | Monthly | ✅ |
| Term premium (short vs long yield) | `ComputeSpread` | Weekly | ✅ |
| Comprehensive multi-question research | `Search` + `Fetch` (ChatGPT Deep Research) | All | ✅ |

| FSI vs MASI vs DSI — 13-month divergence | `GetIndexDivergence` | Weekly | ✅ |
| TB acceptance rate trend (precursor signal) | `GetAuctionHistory` | Weekly | ✅ |
| Maize price → food CPI linkage | `ComputeCorrelation` + `GetCommodityPrices` | Monthly | ✅ |
| Oil price → non-food CPI forward signal | `ComputeCorrelation` + `GetCommodityPrices` | Monthly | ✅ |
| Real rate trend over 13 months | `ComputeRealRate` | Weekly | ✅ |
| Why do banking stocks trade at 50–57× P/E? | `GetBankingSectorMetrics` | Monthly | ✅ |
| Trade deficit trajectory | `GetTradeBalance` | Monthly | ✅ |
| What % of imports do exports cover? | `GetTradeBalance` | Monthly | ✅ |

**Coverage: ~95–97% of typical analyst questions.**

---

### Statistical Guardrails

`ComputeCorrelation` enforces three checks before returning a result.
These are not optional — returning a number without them risks analysts
acting on statistically meaningless output.

**1. Minimum observations**
Refuses computation if fewer than 15 aligned data points exist after
handling N/A values. Returns `valid: false` with explanation and
alternative approach suggestion.

**2. Near-zero variance check**
Refuses computation if either series has near-constant values
(standard deviation below threshold). This specifically catches the
Malawi TB yield problem — the 364-day TB held at 26.00% for 11
consecutive months. Correlation with a flat line is undefined or
spurious. Returns `valid: false` with a redirect to event study
analysis via `DetectMarketSignals`.

**3. Statistical significance threshold**
Always returns p-value. If p > 0.05, the `warning` field is populated:
*"Correlation not statistically significant at conventional thresholds
given N observations. Interpret directionally only."*

**Data frequency alignment**
When correlating series of different frequencies (e.g. weekly stock
price vs monthly commodity price), the tool always downsamples to the
lower frequency using the last observation per period. It never
interpolates or invents data points to bridge gaps.

**Data sufficiency reference**

| Available Data | Detectable Correlation |
|---|---|
| 260 weekly points (5yr stock prices) | r ≥ 0.12 |
| 60 monthly points (5yr indicators) | r ≥ 0.26 |
| 52 weekly points (1yr stock prices) | r ≥ 0.27 |
| 13 weekly points (1 quarter) | r ≥ 0.55 (flag as low confidence) |
| <15 points (any) | Refuse computation |

`ComputeRelativeStrength` confidence levels are described in the tool
documentation above. Beta is always estimated over a longer lookback
than the analysis period — never from the same short window being
analysed.

---

### Honest Gaps

| Question | Gap | Status |
|---|---|---|
| *"Which commodity exports are growing vs declining YoY?"* | Full commodity export breakdown requires deeper `TradeFlowExtractor` parsing of World Bank report narrative | v0.4 backlog |
| *"What is the FX reserve import cover in months?"* | Monthly import total now in `trade_flows` — `GetTradeBalance` can compute this once historical data is backfilled | Closes with backfill |

---


---

## Data Schema

### `financial_indicators` (TimescaleDB hypertable — weekly)

| Column | Type | Notes |
|---|---|---|
| `time` | `TIMESTAMPTZ` | Hypertable partition key |
| `week_ending` | `DATE` | Source week |
| `category` | `TEXT` | `exchange_rate`, `yield`, `inflation`, `interest_rate`, `stock_price`, `stock_return`, `volume`, `reserve` |
| `indicator` | `TEXT` | Exact indicator name |
| `value` | `NUMERIC(18,6)` | Preserves decimal precision |
| `prior_value` | `NUMERIC(18,6)` | Auto-populated from prior week |
| `week_delta` | `NUMERIC(18,6)` | `value - prior_value` |
| `source_doc` | `TEXT` | Source PDF filename |

### `stock_valuations` (monthly — new)

| Column | Type | Notes |
|---|---|---|
| `month` | `DATE` | First day of month |
| `ticker` | `TEXT` | MSE symbol |
| `pe_ratio` | `NUMERIC(10,2)` | Price-to-earnings; negative = loss-making |
| `pbv_ratio` | `NUMERIC(10,2)` | Price-to-book-value |
| `div_yield_pct` | `NUMERIC(6,2)` | Dividend yield % |
| `market_cap_mk_bn` | `NUMERIC(12,2)` | Market cap in MK billions |
| `source_doc` | `TEXT` | Source monthly PDF |

### `commodity_prices` (monthly — new)

| Column | Type | Notes |
|---|---|---|
| `month` | `DATE` | First day of month |
| `commodity` | `TEXT` | `"maize"`, `"oil"` |
| `region` | `TEXT` | `"national"`, `"northern"`, `"central"`, `"southern"`, `"global"` |
| `value` | `NUMERIC(12,4)` | MK/kg for maize; USD/barrel for oil |
| `unit` | `TEXT` | `"MK/kg"`, `"USD/barrel"` |
| `source` | `TEXT` | `"IFPRI"`, `"OPEC"` |
| `source_doc` | `TEXT` | Source monthly PDF |

### `institutional_forecasts` (annual projections — new)

| Column | Type | Notes |
|---|---|---|
| `published_month` | `DATE` | Month forecast was published |
| `institution` | `TEXT` | `"EIU"`, `"World Bank"`, `"Oxford Economics"`, `"GoM"` |
| `indicator` | `TEXT` | e.g. `"Real GDP Growth"`, `"CPI Average"`, `"MK/USD Average"` |
| `forecast_year` | `INT` | Year being forecast (2024–2030) |
| `value` | `NUMERIC(12,4)` | Forecast value |
| `unit` | `TEXT` | `"%"`, `"USD bn"`, `"MK/USD"` |
| `source_doc` | `TEXT` | Source monthly PDF |

### `trade_flows` (monthly — new)

| Column | Type | Notes |
|---|---|---|
| `month` | `DATE` | Reference month |
| `flow_type` | `TEXT` | `"export"`, `"import"` |
| `category` | `TEXT` | `"total"`, `"tobacco"`, `"fertiliser"`, `"diesel"`, `"petrol"` |
| `value_usd_mn` | `NUMERIC(12,2)` | Value in USD millions |
| `pct_of_total` | `NUMERIC(6,2)` | Share of total exports/imports |
| `source_doc` | `TEXT` | Source monthly PDF |

### `market_events` (weekly + monthly narrative)

| Column | Type | Notes |
|---|---|---|
| `id` | `UUID` | Primary key |
| `source_cadence` | `TEXT` | `"weekly"` or `"monthly"` |
| `week_ending` | `DATE` | For weekly events |
| `report_month` | `DATE` | For monthly events |
| `item_number` | `INT` | Position in list |
| `headline` | `TEXT` | First sentence |
| `full_text` | `TEXT` | Complete item |
| `entities` | `JSONB` | Tagged entities |
| `event_type` | `TEXT` | `corporate_action`, `policy`, `macro`, `infrastructure`, `trade`, `health`, `social` |
| `source_citation` | `TEXT` | e.g. `"The Nation, 20 February 2026"` |

### `banking_metrics` (monthly — new)

| Column | Type | Notes |
|---|---|---|
| `month` | `DATE` | First day of month |
| `roe_pct` | `NUMERIC(6,2)` | Return on equity |
| `roa_pct` | `NUMERIC(6,2)` | Return on assets |
| `npl_ratio_pct` | `NUMERIC(6,2)` | Non-performing loan ratio |
| `govt_sec_exposure` | `TEXT` | `"high"`, `"medium"`, `"low"` |
| `source_doc` | `TEXT` | Source monthly PDF |

### `auction_events`

| Column | Type | Notes |
|---|---|---|
| `week_ending` | `DATE` | |
| `tenor` | `TEXT` | `"91-day"`, `"182-day"`, `"364-day"` |
| `applied_mk_bn` | `NUMERIC` | |
| `awarded_mk_bn` | `NUMERIC` | 0 = full rejection |
| `oversubscription_ratio` | `NUMERIC` | NULL if awarded = 0 |
| `is_full_rejection` | `BOOLEAN` | `awarded_mk_bn = 0` |

---

## Contributing

```bash
dotnet restore && dotnet test
```

### Contribution Areas

| Area | Priority |
|---|---|
| Historical backfill — all weekly PDFs | High |
| Historical backfill — all monthly PDFs | High |
| `ValuationExtractor` hardening — edge cases in Appendix 2 layout | High |
| `CorrelationServiceTests` — validate guardrail logic exhaustively | High |
| `CommodityExtractor` — DePlot accuracy on annotated line charts | Medium |
| `TradeFlowExtractor` — expand commodity-level extraction | Medium |
| Sector taxonomy — tag each ticker with sector for peer group queries | Medium |
| BM25 index persistence — rebuild on startup is slow at scale | Low |
| REST API rate limiting + caching (Redis) | Medium |
| Dashboard live data integration (replace static arrays) | High |
| WebSocket endpoint for real-time KPI updates | Low |

### Commit Convention

```
feat(tools): add GetValuationMetrics for monthly P/E and P/BV data
feat(tools): add ComputeCorrelation with statistical guardrails
feat(ingestion): add MonthlyPdfIngester and ValuationExtractor
fix(correlation): handle near-zero variance in TB yield series
test(ingestion): add precision tests for ValuationExtractor
data(monthly): backfill economic reports Jan 2024–Feb 2026
```

### Pull Request Checklist

- [ ] `dotnet test` passes
- [ ] `AppendixExtractorTests` and `ValuationExtractorTests` pass with
  <0.01% numerical error tolerance
- [ ] `CorrelationServiceTests` cover all three guardrail conditions
- [ ] Tool `[Description]` attributes are complete — AI clients use
  these to decide which tool to call
- [ ] New tools have corresponding repository interface + mock
- [ ] `CHANGELOG.md` updated

---

---
