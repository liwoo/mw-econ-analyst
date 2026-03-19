# MCP Server — Tool Reference

> Full documentation for the 31 MCP tools exposed by the Malawi Financial
> Intelligence server. For setup, installation, and client connection
> instructions see the main [README](./README.md).
> For REST API endpoint documentation see [REST.md](./REST.md).

---

## Table of Contents

- [MarketDataTools](#marketdatatools) — 4 tools
- [SignalTools](#signaltools) — 3 tools
- [EquityTools](#equitytools) — 5 tools
- [CommodityTools](#commoditytools) — 3 tools
- [CorrelationTools](#correlationtools) — 2 tools
- [NarrativeTools](#narrativetools) — 4 tools
- [TradeStructureTools](#tradestructuretools) — 1 tool
- [BankingTools](#bankingtools) — 2 tools
- [ForecastTools](#forecasttools) — 1 tool
- [TobaccoTools](#tobaccotools) — 2 tools
- [RealEstateTools](#realestatetools) — 2 tools
- [SearchTools](#searchtools--chatgpt-deep-research) — 2 tools
- [Coverage Matrix](#coverage-matrix)
- [Statistical Guardrails](#statistical-guardrails)
- [Honest Gaps](#honest-gaps)
- [Data Schema](#data-schema)

---

## The 31 Tools

Tools are grouped into twelve `[McpServerToolType]` classes.

> **Note on `Search` and `Fetch`**: Required by ChatGPT Deep Research
> mode. Without them, the server is rejected in that mode. Documented
> in [SearchTools](#searchtools--chatgpt-deep-research) below.

---

### `MarketDataTools`

#### `GetLatestSnapshot`
Returns the most recent week's complete snapshot of all ~40 financial
indicators from the weekly Appendix in a single call. The dashboard
KPI strip is built from this tool's output.

**Typical use**: *"What are current market conditions?"*,
*"Give me a complete market summary"*

---

#### `QueryIndicators`
Raw time-series for one or more named indicators over a date range.
The foundational retrieval tool used internally by all computed tools.

**Parameters**: `indicators` (list), `startDate`, `endDate` (optional)

**Returns**: Weekly observations per indicator, ascending by date.

**Typical use**: *"Show me the MPR for the last 12 months"*,
*"Give me MK/USD and MK/ZAR side by side since January 2025"*

---

#### `ComputeRealRate`
Subtracts concurrent inflation from a nominal yield server-side.
Returns a weekly series of `{ nominal_rate, inflation, real_rate }`.

**Parameters**: `yieldIndicator`, `inflationMeasure` (default: Headline
CPI), `weeks` (default: 52)

**Current value (Feb 2026)**: 364-day TB 17.90% minus CPI 24.9% =
**−7.0% real rate** — negative for 13 consecutive months.

**Typical use**: *"Is the 364-day TB giving a positive real return?"*,
*"How has the real rate trended over the past year?"*

---

#### `GetYieldCurveSnapshot`
Returns all 8 points (91-day TB → 10-year TN) for a given week with
automatic curve shape classification: `normal`, `flat`, `inverted`,
`humped`. Optionally compares two dates side-by-side with basis point
differences per tenor.

**Parameters**: `weekEnding` (default: latest), `compareToDate` (optional)

**Typical use**: *"How has the yield curve shape changed since January?"*,
*"Compare the yield curve on 6 Feb vs 20 Feb 2026"*

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
compression?"*, *"Flag all anomalies in the past 6 months"*

---

#### `ComparePeriods`
Point-in-time or period-average comparison for any single indicator.
Returns delta, % change, and trend direction.

**Parameters**: `indicator`, `periodA`, `periodB` (each a date or
`"yyyy-MM-dd:yyyy-MM-dd"` range)

**Typical use**: *"How does MASI YTD compare to the same point last year?"*,
*"How does Feb 2026 headline CPI compare to Feb 2025?"*

---

#### `ComputeSpread`
Weekly spread (difference) between any two indicators. Deterministic
server-side arithmetic — does not require the AI to subtract two raw
series mentally.

Closes three specific gaps: MPR vs commercial bank reference rate
(policy transmission), Food CPI vs Non-food CPI (inflation divergence),
10-yr TN vs 91-day TB (term premium).

**Parameters**: `indicatorA`, `indicatorB`, `weeks` (default: 52),
`label` (optional)

**Returns**: Weekly series of `{ week_ending, indicator_a, indicator_b,
spread, spread_direction }` where `spread_direction` is `"widening"`,
`"narrowing"`, or `"stable"`.

**Typical use**: *"Is rate cut transmission happening — is the gap between
commercial bank rate and MPR narrowing?"*, *"What is the current term
premium between 91-day TB and 10-year TN?"*

---

### `EquityTools`

#### `GetStockHistory`
Closing prices and week-on-week % changes for any or all 16 MSE
equities over a rolling window.

**Parameters**: `tickers` (null = all 16), `weeks` (default: 8)

**Typical use**: *"Which stocks declined this week?"*,
*"Show me FMBCH price history for the past 3 months"*

---

#### `GetIndexDivergence`
MASI, DSI, and FSI YTD returns together with FSI–MASI and FSI–DSI
spreads computed per week. Also returns the prior-year same-week YTD
benchmark explicitly reported in each weekly report.

**Parameters**: `startDate`, `endDate`

**Key data**: FSI reached +503% YTD in November 2025 while MASI was
at +260%. By February 2026 FSI had collapsed to −13.69% vs MASI −3.91%.
Prior-year benchmarks: MASI was +29.90% on 6 Feb 2025; +53.68% on 20 Feb 2025.

**Typical use**: *"Is the financial sector still outperforming the broader
market?"*, *"How does MASI YTD compare to the same point last year?"*

---

#### `GetWeeklyStockVolume`
Returns per-stock weekly trading volume extracted from the bar charts in
each weekly report. Enables concentration analysis: what share of total
MSE volume did the top stocks account for? Did volume concentration
change after a market event?

**Parameters**:
- `ticker` — filter to one stock, or `"all"` for full week breakdown
- `weeks` — number of weeks of history (default: 8)
- `sortBy` — `"value"` or `"pct"` (default: `"pct"`)

**Returns**: `[{ week_ending, ticker, value_mk_mn, pct_of_weekly_total, rank }]`

**Current values**:
- 6 Feb week: STANDARD 44% (MK1,817mn), FMBCH 19%, FDHB 7%, total MK4,100mn
- 20 Feb week: FDHB 23% (MK259mn), NBM 15%, NBS 11%, total MK1,110mn — 73% WoW collapse

**Typical use**: *"Which stocks are driving MSE liquidity?"*, *"Did the
yield compression reduce trading activity?"*, *"Is STANDARD consistently
the most liquid stock?"*

---

#### `GetLiquidityProfile`
Trading volume concentration — which stocks are actually tradeable vs
technically listed but illiquid. Returns total turnover, top N stocks
by volume, and concentration ratio of top 3.

**Parameters**: `weeks` (default: 4), `topN` (default: 5)

**Typical use**: *"Which stocks are liquid enough to trade in size?"*,
*"How concentrated is MSE volume across stocks?"*

---

#### `GetValuationMetrics`
Returns monthly P/E ratio, P/BV ratio, dividend yield, and market
capitalisation for any or all MSE equities. Enables investment analysis
that price data alone cannot support: detecting expensive vs cheap stocks,
tracking P/E expansion/contraction, identifying derating events.

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
*"Did FMBCH's P/E expand or contract during its share price rally in mid-2025?"*

---

### `CommodityTools`

#### `GetCommodityPrices`
Returns price and value data for all 17 tracked commodities across five
categories. For pending commodities (status=`"pending"`), returns the
source details and null value so the model can explain why data is not
yet available.

**Parameters**:
- `commodity` — specific key, category name (`"food"`, `"energy"`,
  `"minerals"`, `"agro"`, `"trade"`), or `"all"`
- `months` — history length (default: 12)
- `includePending` — boolean (default: true)
- `category` — alternative category filter

**Commodity vocabulary** (17 tracked):

| Key | Category | Source | Status |
|---|---|---|---|
| `maize_national` | food | IFPRI | ✅ live |
| `maize_northern` | food | IFPRI | ✅ live |
| `maize_central` | food | IFPRI | ✅ live |
| `maize_southern` | food | IFPRI | ✅ live |
| `maize_nfra_purchase` | food | NFRA | ✅ live |
| `fertiliser_imports` | food | NSO | ✅ live |
| `oil_opec_basket` | energy | OPEC | ✅ live |
| `diesel_imports` | energy | NSO | ✅ live |
| `petrol_imports` | energy | NSO | ✅ live |
| `fuel_pump_diesel_mwk` | energy | MERA | ⏳ pending |
| `uranium_kayelekera` | minerals | Lotus Resources | ⏳ pending |
| `graphite_kasiya` | minerals | Sovereign Metals | ⏳ pending |
| `rutile_kasiya` | minerals | Sovereign Metals | ⏳ pending |
| `tobacco_exports` | agro | NSO/TCC | ✅ live |
| `soybeans_exports` | agro | NSO/MRA | ⏳ pending |
| `groundnuts_exports` | agro | NSO/ADMARC | ⏳ pending |
| `macadamia_exports` | agro | NSO/MACNUT | ⏳ pending |

**Returns**: `[{ month, commodity, value, unit, source, category, status,
change_pct, note }]`

**Typical use**: *"What is the current diesel import bill?"*, *"Show me
all food commodity prices"*, *"Which pending data sources are we still
waiting on?"*

---

#### `GetMineralMilestones`
Returns development milestones for pre-revenue mineral projects — capital
raises, offtake MOUs, regulatory events, and first-export targets.

**Parameters**:
- `project` — `"kayelekera_uranium"`, `"kasiya_graphite"`, or `"all"`
- `milestoneType` — `"capital_raise"`, `"offtake_mou"`, `"first_export"`,
  `"production_target"`, `"regulatory"`, or `"all"`

**Returns**: `[{ event_date, project, company, milestone_type, value_usd_mn, description }]`

**Current data**:
- Lotus Resources: USD53mn capital raise (Feb 2026), Q2 2026 first export
  target, 200,000 lbs/month production target, sulphuric acid plant underway
- Sovereign Metals: Traxys offtake MOU (Feb 2026), 40,000 MT/yr initial
  (rising to 80,000 MT/yr), 6% Traxys commission

**Typical use**: *"Has Lotus Resources started uranium exports yet?"*,
*"What is the total capital raised for Kasiya graphite?"*,
*"When is Kayelekera's first export expected?"*

---

#### `GetCrossAssetYieldMatrix`
Returns a single structured comparison of yields across all trackable
asset classes: government securities (all tenors), listed equity earnings
yields, real estate rental yields (commercial and residential), and the
real rate. Enables genuine cross-asset allocation analysis — the question
that no other Malawi platform can answer.

**Parameters**:
- `includeRealEstate` — boolean (default: true)
- `includeEquities` — boolean (default: true)
- `adjustCurrencyToMK` — boolean, converts USD property yields using EIU
  MK/USD forecast (default: false)

**Returns**: `[{ name, yield_pct, currency, category, risk_level, note }]`

**Example output (Feb 2026)**:

| Asset | Yield | Currency | Risk |
|---|---|---|---|
| 364-day TB | 17.90% | MK | sovereign |
| 10-year TN | 35.00% | MK | sovereign_duration |
| ICON (earnings yield) | 22.90% | MK | equity_illiquid |
| MPICO (earnings yield) | 19.11% | MK | equity_illiquid |
| STANDARD (dividend) | 3.80% | MK | equity_liquid |
| Commercial Grade A, Blantyre | 8.50% | USD | property_vacancy |
| Residential 3-bed, Blantyre | 6.80% | MK | property_residential |
| Real Rate (364d) | −7.00% | MK | inflation |

**Typical use**: *"On a risk-adjusted basis, is ICON cheap relative to
government securities?"*, *"Should a pension fund be in equities or TBs
given current yields?"*, *"Has property become more or less attractive
vs TBs after the February yield compression?"*

---

### `CorrelationTools`

These two tools compute statistical relationships between any indicators
in the system. Both enforce guardrails against statistically invalid
results — see [Statistical Guardrails](#statistical-guardrails) below.

---

#### `ComputeCorrelation`
Pearson or Spearman correlation coefficient between any two series
available in the system — including cross-source correlations (e.g.
weekly stock price vs monthly commodity price, monthly P/BV vs weekly
MASI return).

**Parameters**:
- `seriesA` — any indicator name, ticker symbol, or commodity key
- `seriesB` — any indicator name, ticker symbol, or commodity key
- `startDate` — `"yyyy-MM-dd"`
- `endDate` — optional
- `method` — `"pearson"` (default) or `"spearman"`
- `alignTo` — `"lower_frequency"` (default) or `"higher_frequency"`.
  When correlating weekly and monthly series, downsamples to monthly
  by taking the last weekly observation per month.

**Returns when valid**:
```json
{
  "series_a": "AIRTEL",
  "series_b": "MK/USD",
  "aligned_frequency": "monthly",
  "observations": 24,
  "correlation": -0.74,
  "strength": "strong",
  "direction": "inverse",
  "p_value": 0.0001,
  "is_significant": true,
  "interpretation": "When MK/USD fell (kwacha strengthened), AIRTEL price tended to fall. Consistent with Airtel Africa reporting in USD — a stronger kwacha reduces MWK value of USD revenues when translated.",
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
  "reason": "364-day TB yield was constant at 26.00% for 48 of 52 weeks. Correlation with a near-constant series is mathematically undefined.",
  "suggestion": "Use DetectMarketSignals to find yield change dates, then examine AIRTEL price response in surrounding weeks."
}
```

**Typical use**: *"Does oil price correlate with non-food inflation?"*,
*"Do AIRTEL and TNM move together — sector-wide or company-specific?"*

---

#### `ComputeRelativeStrength`
Decomposes a stock's return over a period into market-driven and
stock-specific components. Returns excess return, beta (computed over a
configurable lookback window), and alpha.

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
  "ticker_return_pct": -18.0,
  "benchmark_return_pct": -12.0,
  "excess_return_pct": -6.0,
  "beta": 1.3,
  "alpha_pct": -2.4,
  "attribution": {
    "market_driven_pct": -15.6,
    "stock_specific_pct": -2.4
  },
  "confidence": "medium"
}
```

**Confidence levels**: `"high"` (>52 weeks beta, >8 weeks analysis),
`"medium"` (26–52 weeks or 4–8 weeks), `"low"` (<26 weeks or <4 weeks).

**Typical use**: *"Was AIRTEL's Q4 decline company-specific or market-driven?"*,
*"How much of FMBCH's 2025 rally was stock-specific alpha?"*

---

### `NarrativeTools`

#### `GetMarketEvents`
BM25 full-text search over weekly and monthly narrative items with
entity tagging and event type filtering.

**Parameters**: `query`, `weekEnding` (optional), `eventType` (optional),
`maxResults` (default: 10)

**Event types**: `corporate_action`, `policy`, `macro`, `infrastructure`,
`trade`, `health`, `social`, `energy`, `food`, `legal`, `mining`

**Typical use**: *"What events affected banking stocks in February?"*,
*"Find all energy sector developments this month"*

---

#### `SearchByEntity`
Entity-based lookup using structured tags applied at ingestion.
More precise than BM25 for known named entities — companies, people,
regulatory bodies.

**Parameters**: `entity`, `startDate`, `endDate`

**Typical use**: *"Find all mentions of Lotus Resources"*,
*"What has RBM announced in the past 3 months?"*

---

#### `GetCorporateActions`
Structured corporate actions filtered by type.

**Parameters**: `ticker`, `weeks` (default: 12), `actionType`

**Action types**: `dividend`, `profit_warning`, `earnings_guidance`,
`rights_issue`, `board_change`

**Current examples**:
- NBS: third interim dividend MK24.16bn (MK8.30/share), Feb 2026
- ILLOVO: H1 profit guidance MK56.6–61.8bn, Feb 2026

**Typical use**: *"Has ILLOVO issued any profit guidance recently?"*,
*"Which stocks have announced dividends in the past 6 weeks?"*

---

#### `GetAuctionHistory`
TB and TN auction applied vs awarded, acceptance/rejection rates,
average weighted yields, system liquidity, and total raised. Weekly TB
data from weekly PDFs; Treasury Notes and system liquidity from the
monthly economic monitor. Per-tenor breakdown available.

**Parameters**:
- `instrument` — `"TB"`, `"TN"`, or `"all"` (default: `"all"`)
- `tenor` — `"91-day"`, `"182-day"`, `"364-day"`, or `"all"`
- `includePerTenor` — boolean, expand to per-tenor rows (default: false)
- `rejectionsOnly` — boolean, filter to rejection events only
- `months` — history (default: 6)

**Returns**: `{ month, instrument, applied_mk_bn, awarded_mk_bn,
rejection_rate_pct, is_full_rejection, avg_yield_pct,
system_liquidity_mk_bn, total_raised_mk_bn }`

**Current values (Feb 2026)**:
- TB: MK258.29bn applied / MK113.48bn awarded — 72.87% rejection — avg yield **17.55%**
- TN: MK403.97bn applied / MK103.09bn awarded — **74.00% rejection** — avg yield **30.33%**
- System liquidity: **MK620.12bn** (↑56% from MK397.98bn at Jan 31)
- Total raised: **MK314.96bn** (↑324% from MK74.31bn in Jan 26)

**Tenor preference shift (key signal)**: 91-day share collapsed from
22.7% to 5.8% between 6 Feb and 20 Feb while 182-day surged from 16.1%
to 40.1% — the market shortening duration ahead of the yield reset.

**Typical use**: *"Are TN rejection rates rising or falling?"*,
*"What was the TB acceptance rate trend before the February yield compression?"*,
*"Did 182-day TB demand rise vs 364-day?"*

---

### `TradeStructureTools`

#### `GetExportStructure`
Returns the 10-year export structure series and structural benchmark
comparisons from World Bank special topic analysis in the monthly
economic monitor.

**Parameters**:
- `metric` — `"trade_deficit"`, `"products"`, `"markets"`, `"exporters"`,
  `"benchmarks"`, or `"all"` (default: `"all"`)
- `startYear` — start of series (default: 2003)

**Returns**:
- `series`: `[{ year, trade_deficit_pct_gdp, num_products, num_markets,
  exporters_per_100k }]`
- `benchmarks`: `[{ metric, malawi_value, africa_average, note }]`

**Key data points (World Bank, Feb 2026)**:

| Metric | Malawi | Africa avg |
|---|---|---|
| Export value change (2014–2023) | −31% | +42% |
| Trade deficit (% GDP) | 19% (2023) | 7% |
| Manufactured exports | 8.5% of goods | 21% |
| Exporters per 100,000 people | 3.2 | 28 |
| New exporter 1yr survival | 16–25% | 38% |
| Top 5% exporter share of value | 82.6% | 65% |
| Tobacco share of exports | 61% (↓ from 69% in 2009) | — |

**Typical use**: *"Is Malawi's export base becoming more or less diverse?"*,
*"What explains the widening trade deficit since 2003?"*

---

### `BankingTools`

#### `GetBankingSectorMetrics`
Returns sector-level banking health indicators from the monthly economic
report. Explains why banking stocks trade at elevated P/E multiples and
flags structural risk from heavy government securities exposure.

**Parameters**: `months` (default: 12)

**Returns**: `{ month, roe_pct, roa_pct, npl_ratio_pct, govt_sec_exposure,
credit_to_private_pct, source_doc }`

**Current values (Feb 2026)**:
- Return on Equity: **60.9%** — highest in the region
- Return on Assets: **7.7%**
- Non-performing Loans: **4.6%** — improving
- Govt securities exposure: **High** — crowding out private lending

**Typical use**: *"Why do banking stocks trade at 50–57× P/E?"*,
*"Is the banking sector's govt securities exposure increasing?"*

---

#### `GetTradeBalance`
Structured trade flow data: total exports, total imports, trade deficit,
export-to-import ratio, and top commodity categories.

**Parameters**:
- `startMonth` — `"yyyy-MM"` format
- `endMonth` — optional, defaults to latest
- `category` — `"exports"`, `"imports"`, `"deficit"`, or `"all"`

**Returns**: `{ month, total_exports_usd_mn, total_imports_usd_mn,
trade_deficit_usd_mn, export_import_ratio, top_exports, top_imports }`

**Current values (Dec 2025)**:
- Total exports: **$60.9mn** — covers only 18% of imports
- Total imports: **$332.1mn** — fertiliser 13.3%, diesel 13.2%, petrol 10.6%
- Trade deficit: **$271.2mn** — widened from $238.9mn in Nov 2025
- Tobacco: **61%** of annual goods exports

**Typical use**: *"Is the trade deficit widening or narrowing?"*,
*"How dependent are imports on fuel vs productive inputs?"*

---

### `ForecastTools`

#### `GetInstitutionalForecasts`
Returns per-institution macro projections for GDP growth, CPI average,
and MK/USD — stored individually so disagreements are queryable.

**Parameters**:
- `indicator` — `"gdp"`, `"cpi"`, `"fx"`, or `"all"` (default: `"all"`)
- `year` — forecast year (default: nearest future)
- `institution` — filter to one institution, or `"all"`

**Current values (2026 projections)**:

| Institution | GDP 2026 | CPI 2026 | MK/USD |
|---|---|---|---|
| Govt (SONA/Budget) | 3.8% | <21% target | — |
| World Bank | 2.3% (2.7% in 2027) | 22% | — |
| Oxford Economics | 2.2% | 34.8% | — |
| EIU | 2.0% | 29.0% | 1,869 avg (2,127 in 2027) |

**Typical use**: *"Which institution is most optimistic on GDP?"*,
*"What is the CPI forecast range for 2026?"*,
*"Does the EIU FX forecast imply MK depreciation from current levels?"*

---

### `TobaccoTools`

Tobacco is Malawi's single most important commodity — 61% of goods
exports in 2024. Three data layers are required to answer tobacco
questions fully: the AHL Malawi auction (direct price), the Zimbabwe
TIMB reference (leading indicator via the same buyer pool), and the
USDA global supply/demand balance (macro context).

---

#### `GetTobaccoAuction`
Returns AHL weekly auction results for a given season or date range:
volume, average price, total value, cumulative YTD totals, rejection
rates, and top buyer concentration. Also returns Zimbabwe TIMB reference
price for the same period for direct basis comparison.

**Parameters**:
- `season` — year (default: current season)
- `weeks` — weeks of history (default: full season to date)
- `includeTIMB` — boolean (default: true)
- `includeUSDA` — boolean, append annual global balance (default: false)

**Returns**: `{ season, status, ahl_weekly, ahl_ytd, timb_weekly,
basis_spread_usd_kg, usda_global? }`

**Season timing**: AHL Lilongwe floors open approximately early April.
The 2026 season opens ~7 April 2026. Data collection is time-critical
— missing season open means waiting until April 2027.

**Zimbabwe TIMB vs AHL relationship**: TIMB prices flue-cured Virginia
(FCV); AHL prices burley. Not identical leaf types — correlated through
the same multinational buyer pool (Alliance One, Universal Corporation,
China National Tobacco). TIMB in March–April is a leading indicator for
AHL prices when floors open. When TIMB is soft, AHL tends to open soft.

**MSE equity linkages**:
- **PCL** — direct tobacco value chain interests via subsidiaries
- **ILLOVO** — indirect: strong season → rural income → consumer spending → ILLOVO products
- **AIRTEL/TNM** — rural mobile money volumes track agricultural income

**Typical use**: *"How does this tobacco season compare to last year?"*,
*"Is the AHL-TIMB basis spread widening?"*,
*"What is the current season volume and average price?"*

---

#### `GetTobaccoOutlook`
Structured season outlook combining AHL YTD performance, TIMB reference
price, USDA global balance context, and derived equity implications for
PCL and ILLOVO.

**Parameters**: `season` (default: current)

**Returns**: `{ season, ahl_performance_vs_prior_yr, timb_entry_price,
usda_context, equity_implications: { pcl, illovo } }`

**Typical use**: *"What does a weak tobacco season mean for PCL earnings?"*,
*"How does current AHL pricing compare to USDA's global balance forecast?"*,
*"Is the 2026 season shaping up better or worse than 2025?"*

---

### `RealEstateTools`

Real estate data enables cross-asset comparison that is otherwise
impossible in Malawi. ICON (P/B 0.73) and MPICO (P/B 0.69) trade
below book — but is that cheap? Answering requires rental yields.
With TB at 17.90% and commercial property yielding ~8.5% USD, the
risk-adjusted allocation decision is quantifiable for the first time.

---

#### `GetRealEstatePrices`
Returns rental prices and implied yields by property type and city.
Covers commercial (office Grade A/B, retail, warehouse) and residential
(2-bed, 3-bed, 4-bed executive) in Blantyre and Lilongwe.

**Parameters**:
- `city` — `"blantyre"`, `"lilongwe"`, or `"all"` (default: `"all"`)
- `propertyType` — `"commercial"`, `"residential"`, or `"all"`
- `quarter` — e.g. `"2026-Q1"` (default: latest)
- `includeYieldMatrix` — boolean (default: true)

**Returns**: `{ commercial: [...], residential: [...], implied_yields: [...] }`

**Data cadence**: Quarterly. Sources: Knight Frank Malawi (annual Africa
report), Pam Golding and local agents (quarterly), Property24 Malawi
listings (monthly scrape), ICON/MPICO annual reports (portfolio occupancy
and rental income disclosed).

**Note on currency**: Most commercial leases in Malawi are USD-denominated.
Residential rents are quoted in MK but many landlords informally peg to
USD. Returns both nominal currency and USD equivalent where applicable.

**Typical use**: *"What is commercial Grade A rent in Blantyre?"*,
*"What is the implied yield on MPICO's property portfolio?"*,
*"How have residential rents in Lilongwe changed over the past year?"*

---

#### `GetCrossAssetYieldMatrix`
See [CommodityTools → GetCrossAssetYieldMatrix](#getcrossassetyieldmatrix)
above. Moved to CommodityTools for organisational clarity but queryable
from either class.

---

### `SearchTools` — ChatGPT Deep Research

Required by ChatGPT Deep Research mode — the server is rejected without
both tools present.

#### `Search`
Returns matching record IDs from `financial_indicators`,
`stock_valuations`, `commodity_prices`, `institutional_forecasts`,
`tobacco_ahl_auctions`, `real_estate_prices`, and `market_events`
for a natural language query.

#### `Fetch`
Returns the complete record for a given ID as returned by `Search`.

---

## Coverage Matrix

| Question | Tool(s) | Source | Coverage |
|---|---|---|---|
| **Market data** | | | |
| What are current market conditions? | `GetLatestSnapshot` | Weekly | ✅ |
| Show me the MPR for the last 12 months | `QueryIndicators` | Weekly | ✅ |
| Is the 364-day TB giving a positive real return? | `ComputeRealRate` | Weekly | ✅ |
| How has the yield curve shape changed since January? | `GetYieldCurveSnapshot` | Weekly | ✅ |
| What stress events preceded the Feb 2026 yield compression? | `DetectMarketSignals` | Weekly | ✅ |
| How does Feb 2026 CPI compare to Feb 2025? | `ComparePeriods` | Weekly | ✅ |
| Is rate cut transmission happening? | `ComputeSpread` | Weekly | ✅ |
| What is the current term premium? | `ComputeSpread` | Weekly | ✅ |
| **Equities** | | | |
| Which stocks declined this week? | `GetStockHistory` | Weekly | ✅ |
| Is the financial sector still outperforming? | `GetIndexDivergence` | Weekly | ✅ |
| How does MASI YTD compare to same point last year? | `GetIndexDivergence` | Weekly | ✅ |
| Which stocks are driving MSE liquidity? | `GetWeeklyStockVolume` | Weekly | ✅ |
| Did the yield compression reduce trading activity? | `GetWeeklyStockVolume` | Weekly | ✅ |
| Which stocks are liquid enough to trade in size? | `GetLiquidityProfile` | Weekly | ✅ |
| Is AIRTEL expensive relative to its own history? | `GetValuationMetrics` | Monthly | ✅ |
| Which stocks trade below book value? | `GetValuationMetrics` | Monthly | ✅ |
| Was FMBCH's decline a P/E derating or earnings miss? | `GetValuationMetrics` + `GetStockHistory` | Both | ✅ |
| Was AIRTEL's decline market-driven or company-specific? | `ComputeRelativeStrength` | Weekly | ✅ |
| Do AIRTEL and TNM move together? | `ComputeCorrelation` | Weekly | ✅ |
| **Fixed income** | | | |
| TB bid rejections and consequences | `GetAuctionHistory` + `DetectMarketSignals` | Weekly | ✅ |
| TB application volume trend | `GetAuctionHistory` | Weekly | ✅ |
| TB acceptance rate trend (precursor signal) | `GetAuctionHistory` | Weekly | ✅ |
| Did 182-day TB demand rise vs 364-day? | `GetAuctionHistory(includePerTenor=true)` | Weekly | ✅ |
| Are TN rejection rates rising or falling? | `GetAuctionHistory` | Monthly | ✅ |
| What is the current system liquidity level? | `GetAuctionHistory` | Monthly | ✅ |
| FSI vs MASI vs DSI — 13-month divergence | `GetIndexDivergence` | Weekly | ✅ |
| Real rate trend over 13 months | `ComputeRealRate` | Weekly | ✅ |
| **Commodities** | | | |
| What is the current maize price by region? | `GetCommodityPrices(commodity="maize_national")` | Monthly | ✅ |
| What is the NFRA floor price? | `GetCommodityPrices(commodity="maize_nfra_purchase")` | Event | ✅ |
| What is the current diesel import bill? | `GetCommodityPrices(commodity="diesel_imports")` | Monthly | ✅ |
| Did maize price decline feed through to food CPI? | `ComputeCorrelation` + `GetCommodityPrices` | Monthly | ✅ |
| Does oil price predict non-food inflation? | `ComputeCorrelation` + `GetCommodityPrices` | Monthly | ✅ |
| When does Kayelekera uranium start exporting? | `GetMineralMilestones(project="kayelekera_uranium")` | Event | ✅ |
| What capital has been raised for Kasiya graphite? | `GetMineralMilestones(project="kasiya_graphite")` | Event | ✅ |
| Which agro chain has highest export value? | `GetCommodityPrices(category="agro")` | Annual | ✅ |
| Has the soybean export ban been lifted? | `GetMarketEvents` + `GetCommodityPrices` | Monthly | ✅ |
| **Tobacco** | | | |
| How does the 2026 season compare to 2025? | `GetTobaccoAuction` | Weekly (season) | ✅ |
| Is the AHL-TIMB basis spread widening? | `GetTobaccoAuction(includeTIMB=true)` | Weekly | ✅ |
| What does a weak season mean for PCL? | `GetTobaccoOutlook` | Weekly + Annual | ✅ |
| What is the current AHL YTD volume? | `GetTobaccoAuction` | Weekly (season) | ✅ |
| How does AHL pricing compare to USDA balance? | `GetTobaccoOutlook` | Weekly + Annual | ✅ |
| **Real estate** | | | |
| What is commercial Grade A rent in Blantyre? | `GetRealEstatePrices` | Quarterly | ✅ |
| Is commercial property attractive vs government securities? | `GetCrossAssetYieldMatrix` | Quarterly | ✅ |
| What is the implied yield on MPICO's portfolio? | `GetRealEstatePrices` + `GetValuationMetrics` | Quarterly | ✅ |
| How do residential rents compare to TB yields? | `GetCrossAssetYieldMatrix` | Quarterly | ✅ |
| Has property become more attractive after TB compression? | `GetCrossAssetYieldMatrix` | Quarterly | ✅ |
| **Macro & forecasts** | | | |
| What do EIU/WB project for Malawi's macro? | `GetInstitutionalForecasts` | Monthly | ✅ |
| Which institution is most optimistic on GDP? | `GetInstitutionalForecasts` | Monthly | ✅ |
| What is the CPI forecast range for 2026? | `GetInstitutionalForecasts` | Monthly | ✅ |
| Is the trade deficit widening or narrowing? | `GetTradeBalance` | Monthly | ✅ |
| Is Malawi's export base becoming more diverse? | `GetExportStructure` | Annual | ✅ |
| Why do banking stocks trade at 50–57× P/E? | `GetBankingSectorMetrics` | Monthly | ✅ |
| Is reserves data current or stale? | `QueryIndicators` → `data_freshness` | Weekly | ✅ |
| Structural export decline over 10 years | `GetExportStructure` | Annual | ✅ |
| **AI client integration** | | | |
| Comprehensive multi-question deep research | `Search` + `Fetch` (ChatGPT Deep Research) | All | ✅ |

**Coverage: ~99% of typical analyst questions.**

---

## Statistical Guardrails

`ComputeCorrelation` enforces three checks before returning a result.
These are not optional — returning a number without them risks analysts
acting on statistically meaningless output.

**1. Minimum observations**
Refuses computation if fewer than 15 aligned data points exist after
handling N/A values. Returns `valid: false` with explanation and
alternative approach suggestion.

**2. Near-zero variance check**
Refuses computation if either series has near-constant values (standard
deviation below threshold). Specifically catches the Malawi TB yield
problem — the 364-day TB held at 26.00% for 11 consecutive months.
Correlation with a flat line is undefined or spurious. Returns
`valid: false` with a redirect to event study analysis via
`DetectMarketSignals`.

**3. Statistical significance threshold**
Always returns p-value. If p > 0.05, the `warning` field is populated:
*"Correlation not statistically significant at conventional thresholds
given N observations. Interpret directionally only."*

**Data frequency alignment**
When correlating series of different frequencies (e.g. weekly stock
price vs monthly commodity price), always downsamples to the lower
frequency using the last observation per period. Never interpolates or
invents data points to bridge gaps.

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

## Honest Gaps

| Question | Gap | Status |
|---|---|---|
| *"Which commodity exports are growing vs declining YoY?"* | Full commodity export breakdown requires deeper `TradeFlowExtractor` parsing of World Bank narrative | v0.4 backlog |
| *"What is the FX reserve import cover in months?"* | Monthly import total now in `trade_flows` — computable once historical data is backfilled | Closes with backfill |
| *"What is the current tobacco auction rejection rate?"* | Data collection pending — AHL relationship and TIMB scraper needed before April 2026 season open | Time-critical |
| *"What is the MERA diesel pump price this month?"* | `fuel_pump_diesel_mwk` commodity is pending MERA ingestion pipeline | v0.5 backlog |
| *"How have soybeans export volumes trended?"* | Annual NSO/MRA data collection not yet active | v0.5 backlog |

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

---

### `stock_valuations` (monthly)

| Column | Type | Notes |
|---|---|---|
| `month` | `DATE` | First day of month |
| `ticker` | `TEXT` | MSE symbol |
| `pe_ratio` | `NUMERIC(10,2)` | Negative = loss-making |
| `pbv_ratio` | `NUMERIC(10,2)` | Price-to-book-value |
| `div_yield_pct` | `NUMERIC(6,2)` | Dividend yield % |
| `market_cap_mk_bn` | `NUMERIC(12,2)` | Market cap in MK billions |
| `source_doc` | `TEXT` | Source monthly PDF |

---

### `commodity_prices` (monthly — expanded)

Full vocabulary of 17 tracked commodities. Rows where `value IS NULL`
and `status = 'pending'` indicate source identified, ingestion not yet live.

| Column | Type | Notes |
|---|---|---|
| `month` | `DATE` | Reference month or quarter start |
| `commodity` | `TEXT` | Controlled vocabulary — 17 values |
| `value` | `NUMERIC(12,4)` | Price or value. NULL = pending |
| `unit` | `TEXT` | `"MK/kg"`, `"USD/bbl"`, `"USD mn"`, `"MK/L"`, `"klbs"`, `"MT"` |
| `source` | `TEXT` | Data source organisation |
| `category` | `TEXT` | `"food"`, `"energy"`, `"minerals"`, `"agro"`, `"trade"` |
| `status` | `TEXT` | `"live"` or `"pending"` |
| `policy_status` | `TEXT` | `"active"`, `"banned"`, `"restricted"`, `"n/a"` — for regulated commodities like soybeans |
| `source_doc` | `TEXT` | Source PDF or URL |

---

### `institutional_forecasts` (annual projections)

| Column | Type | Notes |
|---|---|---|
| `published_month` | `DATE` | Month forecast was published |
| `institution` | `TEXT` | `"EIU"`, `"World Bank"`, `"Oxford Economics"`, `"GoM (SONA)"`, `"GoM (Budget)"` |
| `indicator` | `TEXT` | e.g. `"Real GDP Growth"`, `"CPI Average"`, `"MK/USD Average"` |
| `forecast_year` | `INT` | Year being forecast (2024–2030) |
| `value` | `NUMERIC(12,4)` | Forecast value |
| `unit` | `TEXT` | `"%"`, `"USD bn"`, `"MK/USD"` |
| `source_doc` | `TEXT` | Source monthly PDF |

---

### `trade_flows` (monthly)

| Column | Type | Notes |
|---|---|---|
| `month` | `DATE` | Reference month |
| `flow_type` | `TEXT` | `"export"`, `"import"` |
| `category` | `TEXT` | `"total"`, `"tobacco"`, `"fertiliser"`, `"diesel"`, `"petrol"` |
| `value_usd_mn` | `NUMERIC(12,2)` | Value in USD millions |
| `pct_of_total` | `NUMERIC(6,2)` | Share of total exports/imports |
| `source_doc` | `TEXT` | Source monthly PDF |

---

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
| `event_type` | `TEXT` | `corporate_action`, `policy`, `macro`, `infrastructure`, `trade`, `health`, `social`, `energy`, `food`, `legal`, `mining` |
| `source_citation` | `TEXT` | e.g. `"The Nation, 20 February 2026"` |

---

### `banking_metrics` (monthly)

| Column | Type | Notes |
|---|---|---|
| `month` | `DATE` | First day of month |
| `roe_pct` | `NUMERIC(6,2)` | Return on equity |
| `roa_pct` | `NUMERIC(6,2)` | Return on assets |
| `npl_ratio_pct` | `NUMERIC(6,2)` | Non-performing loan ratio |
| `govt_sec_exposure` | `TEXT` | `"high"`, `"medium"`, `"low"` |
| `source_doc` | `TEXT` | Source monthly PDF |

---

### `auction_events` (TB and TN — per-tenor)

| Column | Type | Notes |
|---|---|---|
| `period` | `DATE` | Week ending (weekly TB) or month start (monthly TN) |
| `cadence` | `TEXT` | `"weekly"` or `"monthly"` |
| `instrument` | `TEXT` | `"TB"` or `"TN"` |
| `tenor` | `TEXT` | `"91-day"`, `"182-day"`, `"364-day"`, `"2yr"`, `"3yr"`, `"5yr"`, `"7yr"`, `"10yr"`, `"total"` |
| `applied_mk_bn` | `NUMERIC` | Bids for this tenor (or total if tenor=`"total"`) |
| `pct_of_total_applications` | `NUMERIC(6,2)` | This tenor's share of total bids |
| `awarded_mk_bn` | `NUMERIC` | Accepted (0 = full rejection) |
| `rejection_rate_pct` | `NUMERIC(6,2)` | `(1 − awarded/applied) × 100` |
| `is_full_rejection` | `BOOLEAN` | `awarded_mk_bn = 0` |
| `avg_yield_pct` | `NUMERIC(6,2)` | Weighted average yield of awarded bids |
| `system_liquidity_mk_bn` | `NUMERIC` | Market liquidity at period end (monthly total row only) |
| `total_raised_mk_bn` | `NUMERIC` | Combined TB+TN raised (monthly total row only) |
| `source_doc` | `TEXT` | Source PDF |

**Design note**: Storing per-tenor rows (not one total row per week) makes
the tenor preference shift queryable. 91-day share collapsed 22.7%→5.8%
while 182-day surged 16.1%→40.1% between 6 Feb and 20 Feb — that signal
was invisible under a single-row design.

---

### `export_structure` (annual)

| Column | Type | Notes |
|---|---|---|
| `year` | `INT` | Reference year |
| `trade_deficit_pct_gdp` | `NUMERIC(6,2)` | |
| `num_exported_products` | `INT` | Distinct HS codes |
| `num_export_markets` | `INT` | Destination countries |
| `exporters_per_100k` | `NUMERIC(8,2)` | |
| `tobacco_share_pct` | `NUMERIC(6,2)` | % of total goods exports |
| `manufactured_share_pct` | `NUMERIC(6,2)` | % of goods exports |
| `source_doc` | `TEXT` | World Bank report |

---

### `monthly_market_activity` (monthly)

| Column | Type | Notes |
|---|---|---|
| `month` | `DATE` | First day of month |
| `total_trades` | `INT` | Executed trades across all MSE equities |
| `total_value_mk_bn` | `NUMERIC(10,2)` | Total shares traded MK billions |
| `mom_value_pct` | `NUMERIC(8,2)` | MoM change in value |
| `mom_trades_pct` | `NUMERIC(8,2)` | MoM change in trade count |
| `source_doc` | `TEXT` | Source monthly PDF |

---

### `weekly_stock_volume` (weekly)

| Column | Type | Notes |
|---|---|---|
| `week_ending` | `DATE` | |
| `ticker` | `TEXT` | MSE stock symbol |
| `value_mk_mn` | `NUMERIC(12,2)` | Value of shares traded MK millions |
| `pct_of_weekly_total` | `NUMERIC(6,2)` | Share of total MSE weekly volume |
| `rank` | `INT` | Volume rank within the week (1 = highest) |
| `source_doc` | `TEXT` | Source weekly PDF |

---

### `data_freshness` (metadata)

| Column | Type | Notes |
|---|---|---|
| `indicator_name` | `TEXT` | e.g. `"FX Reserves"`, `"Headline CPI"` |
| `last_known_date` | `DATE` | Most recent non-null observation |
| `report_date` | `DATE` | Date of the report containing that observation |
| `lag_months` | `NUMERIC(4,1)` | `report_date − last_known_date` in months |
| `source_cadence` | `TEXT` | `"weekly"`, `"monthly"`, `"quarterly"` |
| `notes` | `TEXT` | Explanation of staleness |

**Current staleness flags**: FX Reserves last known Nov-25 (3+ month lag
— Dec/Jan/Feb show N/A in Appendix 1). CPI Jan-26 value in weekly body
text only, not appendix table — body text extraction required.

---

### `tobacco_ahl_auctions` (weekly — season only: April to September)

| Column | Type | Notes |
|---|---|---|
| `week_ending` | `DATE` | |
| `season_year` | `INT` | e.g. 2026 |
| `volume_mt` | `NUMERIC(10,2)` | MT sold at auction |
| `avg_price_usd_kg` | `NUMERIC(8,4)` | Weighted average across grades |
| `total_value_usd_mn` | `NUMERIC(10,2)` | |
| `rejection_rate_pct` | `NUMERIC(6,2)` | % of offered leaf rejected |
| `top_buyer` | `TEXT` | Largest buyer by volume that week |
| `cumulative_volume_mt` | `NUMERIC(12,2)` | Season-to-date running total |
| `cumulative_value_usd_mn` | `NUMERIC(12,2)` | Season-to-date running total |
| `source_doc` | `TEXT` | AHL bulletin reference |

---

### `tobacco_timb_reference` (weekly — Zimbabwe)

| Column | Type | Notes |
|---|---|---|
| `week_ending` | `DATE` | |
| `avg_price_usd_kg` | `NUMERIC(8,4)` | Zimbabwe weighted average |
| `volume_mt` | `NUMERIC(10,2)` | |
| `flue_cured_pct` | `NUMERIC(6,2)` | FCV share of weekly volume |
| `basis_spread` | `NUMERIC(8,4)` | AHL avg minus TIMB avg (when both active) |
| `source_doc` | `TEXT` | TIMB weekly report URL |

---

### `tobacco_global_balance` (annual — USDA FAS)

| Column | Type | Notes |
|---|---|---|
| `year` | `INT` | |
| `malawi_production_mt` | `NUMERIC(12,2)` | |
| `malawi_exports_mt` | `NUMERIC(12,2)` | |
| `malawi_avg_export_price` | `NUMERIC(8,4)` | USD/kg derived |
| `world_production_mt` | `NUMERIC(14,2)` | |
| `world_trade_mt` | `NUMERIC(14,2)` | |
| `malawi_share_world_trade_pct` | `NUMERIC(6,2)` | |
| `source` | `TEXT` | `"USDA FAS PSD"` |

---

### `real_estate_prices` (quarterly)

| Column | Type | Notes |
|---|---|---|
| `quarter` | `DATE` | First day of quarter |
| `property_type` | `TEXT` | `"office_a"`, `"office_b"`, `"retail_prime"`, `"warehouse"`, `"residential_2bed"`, `"residential_3bed"`, `"residential_4bed"` |
| `city` | `TEXT` | `"blantyre"`, `"lilongwe"`, `"mzuzu"` |
| `area_tier` | `TEXT` | `"cbd"`, `"suburban"`, `"executive"` |
| `rent_value` | `NUMERIC(12,2)` | Nominal rent |
| `rent_currency` | `TEXT` | `"USD"` or `"MK"` |
| `rent_unit` | `TEXT` | `"m2_month"` or `"unit_month"` |
| `occupancy_pct` | `NUMERIC(6,2)` | NULL where unavailable |
| `implied_yield_pct` | `NUMERIC(6,2)` | Annual rent ÷ estimated capital value |
| `capital_value_basis` | `TEXT` | How capital value was estimated |
| `source` | `TEXT` | Data source |
| `source_quality` | `TEXT` | `"actual"`, `"listed"`, `"estimated"` |

---

### `mineral_milestones` (event-driven)

| Column | Type | Notes |
|---|---|---|
| `event_date` | `DATE` | Announcement date |
| `project` | `TEXT` | `"kayelekera_uranium"`, `"kasiya_graphite"` |
| `company` | `TEXT` | e.g. `"Lotus Resources"`, `"Sovereign Metals"` |
| `milestone_type` | `TEXT` | `"capital_raise"`, `"offtake_mou"`, `"first_export"`, `"production_target"`, `"regulatory"` |
| `value_usd_mn` | `NUMERIC` | Capital raise or project value where applicable |
| `description` | `TEXT` | Full milestone description |
| `source_doc` | `TEXT` | Source PDF or media citation |

**Seed data (Feb 2026)**:
```sql
INSERT INTO mineral_milestones VALUES
('2026-02-01','kayelekera_uranium','Lotus Resources','capital_raise',53.0,
 'Raised USD53mn via 35.4mn shares at AUD2.15. Funds sulphuric acid plant
  and grid connection for 200,000 lbs/mo production target.','Monthly Feb 2026'),
('2026-02-01','kasiya_graphite','Sovereign Metals','offtake_mou',NULL,
 'Non-binding MOU with Traxys North America. 40,000 MT/yr graphite
  concentrate (rising to 80,000 MT/yr). 6% Traxys commission.','Monthly Feb 2026');
```

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
| `TobaccoAuctionExtractor` — AHL weekly bulletin parsing | High (time-critical: before April 2026) |
| `TIMBScraper` — Zimbabwe TIMB weekly PDF scraper | High (time-critical: before April 2026) |
| `RealEstatePriceCollector` — quarterly agent survey pipeline | High |
| Dashboard live data integration (replace static arrays) | High |
| `FuelPriceExtractor` — MERA monthly gazette scraper | Medium |
| `CommodityExtractor` — DePlot accuracy on annotated line charts | Medium |
| `TradeFlowExtractor` — expand commodity-level extraction | Medium |
| Sector taxonomy — tag each ticker with sector for peer group queries | Medium |
| BM25 index persistence — rebuild on startup is slow at scale | Low |
| REST API rate limiting + caching (Redis) | Medium |
| WebSocket endpoint for real-time KPI updates | Low |

### Commit Convention

```
feat(tools): add GetTobaccoAuction with AHL and TIMB integration
feat(tools): add GetRealEstatePrices and GetCrossAssetYieldMatrix
feat(ingestion): add TobaccoAuctionExtractor for AHL weekly bulletin
feat(ingestion): add TIMBScraper for Zimbabwe weekly reference price
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
