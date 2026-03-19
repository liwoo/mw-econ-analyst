# REST API — Endpoint Reference

> Documentation for the `MalawiFinancialApi` REST layer that powers the
> React dashboard and any non-AI client. For MCP tool documentation see
> [MCP.md](./MCP.md). For setup and installation see [README](./README.md).

**Version**: 1.4.0 · **Base URL**: `/api/v1` · **20 endpoints**

---

## Table of Contents

- [Architecture](#architecture)
- [Project Structure](#project-structure)
- [Authentication](#authentication)
- [Common Parameters](#common-parameters)
- [Endpoints](#endpoints)
  - [Market Data](#market-data)
    - [GET /snapshot](#get-snapshot)
    - [GET /indices](#get-indices)
    - [GET /data-freshness](#get-data-freshness)
  - [Equities](#equities)
    - [GET /equities](#get-equities)
    - [GET /equities/volume](#get-equitiesvolume)
  - [Fixed Income](#fixed-income)
    - [GET /yields](#get-yields)
    - [GET /yields/auctions](#get-yieldsauctions)
    - [GET /yields/auctions/tenors](#get-yieldsauctionstenors)
    - [GET /rates](#get-rates)
  - [FX & Macro](#fx--macro)
    - [GET /fx](#get-fx)
    - [GET /macro](#get-macro)
    - [GET /forecasts](#get-forecasts)
  - [Commodities](#commodities)
    - [GET /commodities](#get-commodities)
    - [GET /commodities/milestones](#get-commoditiesmilestones)
    - [GET /commodities/tobacco](#get-commoditiestobacco)
  - [Trade & Structure](#trade--structure)
    - [GET /trade](#get-trade)
    - [GET /export-structure](#get-export-structure)
  - [Banking](#banking)
    - [GET /banking](#get-banking)
  - [Real Estate & Cross-Asset](#real-estate--cross-asset)
    - [GET /real-estate](#get-real-estate)
    - [GET /cross-asset/yields](#get-cross-assetyields)
- [Dashboard Integration](#dashboard-integration)
- [CORS Configuration](#cors-configuration)
- [Tier-Based Access Control](#tier-based-access-control)
- [Error Responses](#error-responses)

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    SHARED DATA LAYER                             │
│  TimescaleDB  ·  BM25 Index  ·  Repository Interfaces           │
└────────────────────┬────────────────────────────────────────────┘
                     │
        ┌────────────┴────────────┐
        ▼                         ▼
┌───────────────┐        ┌────────────────────┐
│  MCP Server   │        │  REST API Server   │
│  (AI clients) │        │  (Dashboard, apps) │
│  :5000/mcp    │        │  :5001/api/v1      │
└───────────────┘        └────────────────────┘
        │                         │
  Claude, ChatGPT,          React Dashboard,
  Copilot, Cursor           Mobile apps,
                            3rd party clients
```

Both servers share the same repository interfaces — `IIndicatorRepository`,
`IMarketEventRepository`, `IValuationRepository`, `ICommodityRepository`,
`ITobaccoRepository`, `IRealEstateRepository`, and `ICorrelationService`.
No duplication of data access logic. Add the REST project to
`docker-compose.yml` alongside the MCP server.

---

## Project Structure

```
MalawiFinancialApi/
├── MalawiFinancialApi.csproj
├── Program.cs                         # Minimal API setup, CORS, auth, tier middleware
├── Controllers/
│   ├── SnapshotController.cs          # GET /snapshot, /data-freshness
│   ├── EquitiesController.cs          # GET /equities, /equities/volume
│   ├── YieldsController.cs            # GET /yields, /yields/auctions, /yields/auctions/tenors
│   ├── RatesController.cs             # GET /rates
│   ├── FxController.cs                # GET /fx
│   ├── MacroController.cs             # GET /macro
│   ├── ForecastsController.cs         # GET /forecasts
│   ├── CommoditiesController.cs       # GET /commodities, /commodities/milestones, /commodities/tobacco
│   ├── TradeController.cs             # GET /trade, /export-structure
│   ├── BankingController.cs           # GET /banking
│   ├── IndicesController.cs           # GET /indices
│   ├── RealEstateController.cs        # GET /real-estate
│   └── CrossAssetController.cs        # GET /cross-asset/yields
└── DTOs/
    ├── SnapshotDto.cs
    ├── EquityDto.cs
    ├── YieldDto.cs
    ├── AuctionDto.cs
    ├── TenorAuctionDto.cs
    ├── FxRateDto.cs
    ├── RateDto.cs
    ├── MacroIndicatorDto.cs
    ├── ForecastDto.cs
    ├── CommodityDto.cs
    ├── MilestoneDto.cs
    ├── TobaccoDto.cs
    ├── TradeDto.cs
    ├── ExportStructureDto.cs
    ├── BankingDto.cs
    ├── IndexReturnDto.cs
    ├── RealEstateDto.cs
    └── CrossAssetYieldDto.cs
```

---

## Authentication

All endpoints require an `X-Api-Key` header. Keys are issued per account
and scoped to a tier. Requests without a valid key return `401`.

```http
GET /api/v1/snapshot HTTP/1.1
X-Api-Key: your-api-key-here
```

```csharp
// Program.cs — API key middleware
app.Use(async (ctx, next) => {
    if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var key) ||
        !ApiKeyStore.IsValid(key, out var tier)) {
        ctx.Response.StatusCode = 401;
        return;
    }
    ctx.Items["tier"] = tier;
    await next();
});
```

For external or multi-tenant deployment, upgrade to OAuth 2.0 with the
same Auth0 or Entra ID provider used for the MCP server.

---

## Common Parameters

All endpoints accept:

| Parameter | Type | Description |
|---|---|---|
| `from` | `yyyy-MM-dd` | Start date filter |
| `to` | `yyyy-MM-dd` | End date filter. Defaults to latest available. |

All endpoints return `Content-Type: application/json`.

Standard error shape:
```json
{ "error": "Unauthorized", "message": "Missing or invalid X-Api-Key header" }
```

---

## Endpoints

---

## Market Data

### GET /snapshot

Latest week's complete market snapshot. Powers the dashboard KPI strip.
Equivalent to MCP `GetLatestSnapshot`.

**MCP equivalent**: `GetLatestSnapshot`
**Tier**: Free+

```http
GET /api/v1/snapshot
```

**Response**:
```json
{
  "week_ending": "2026-02-28",
  "masi": {
    "value": 574680,
    "ytd_pct": -3.91,
    "prior_year_ytd_pct": 64.92,
    "mom_pct": -2.42
  },
  "cpi": {
    "headline": 24.9,
    "food": 22.1,
    "non_food": 29.8,
    "period": "Jan 26"
  },
  "mpr": 26.00,
  "tb_364d": {
    "yield": 17.90,
    "prior_month": 26.00,
    "delta_bps": -810
  },
  "real_rate_364d": -7.00,
  "mk_usd": 1750.45,
  "fx_reserves_usd_mn": 530.0,
  "fx_reserves_as_of": "2025-11-30",
  "fx_reserves_stale": true
}
```

---

### GET /indices

MASI, DSI, and FSI year-to-date returns with divergence spreads and
prior-year benchmark.
Equivalent to MCP `GetIndexDivergence`.

**MCP equivalent**: `GetIndexDivergence`
**Tier**: Free (current only) · Professional+ (history)

**Query params**: `?months=13`

```http
GET /api/v1/indices?months=13
```

**Response**:
```json
[
  {
    "month": "2026-02",
    "masi_ytd": -3.91,
    "dsi_ytd": -0.75,
    "fsi_ytd": -13.69,
    "fsi_masi_spread": -9.78,
    "fsi_dsi_spread": -12.94,
    "masi_prior_year_ytd": 64.92
  },
  {
    "month": "2025-11",
    "masi_ytd": 259.98,
    "dsi_ytd": 217.65,
    "fsi_ytd": 503.79,
    "fsi_masi_spread": 243.81,
    "fsi_dsi_spread": 286.14,
    "masi_prior_year_ytd": null
  }
]
```

---

### GET /data-freshness

Staleness metadata for all tracked indicators. Surfaces publication lags
so consumers know when data is stale. FX reserves have a 3+ month lag;
CPI Jan-26 only available in weekly body text.

**MCP equivalent**: `QueryIndicators` → `data_freshness` table
**Tier**: Free+

```http
GET /api/v1/data-freshness
```

**Response**:
```json
[
  {
    "indicator_name": "FX Reserves",
    "last_known_date": "2025-11-30",
    "report_date": "2026-02-20",
    "lag_months": 2.7,
    "source_cadence": "weekly",
    "notes": "Dec/Jan/Feb show N/A in Appendix 1 — publication lag from RBM"
  },
  {
    "indicator_name": "Headline CPI",
    "last_known_date": "2026-01-31",
    "report_date": "2026-02-20",
    "lag_months": 0.7,
    "source_cadence": "weekly",
    "notes": "Jan 2026 value in weekly body text only, not appendix table"
  }
]
```

---

## Equities

### GET /equities

All 16 MSE equities with closing price, valuation metrics, liquidity
classification, and valuation flags.
Equivalent to MCP `GetStockHistory` + `GetValuationMetrics`.

**MCP equivalent**: `GetStockHistory` + `GetValuationMetrics`
**Tier**: Free (top 5, current price only) · Professional+ (all 16, full metrics)

**Query params**: `?sector=bank` `?sort=mom_pct` `?order=desc`
`?include_turnover=true`

```http
GET /api/v1/equities?sector=bank&sort=mom_pct&order=asc
```

**Response**:
```json
[
  {
    "ticker": "FMBCH",
    "close": 2743.90,
    "prev_close": 2965.07,
    "mom_pct": -7.5,
    "pe_ratio": 57.0,
    "pbv_ratio": 14.2,
    "div_yield_pct": 0.1,
    "market_cap_mk_bn": 6745,
    "sector": "bank",
    "liquidity": "high",
    "flags": ["expensive"]
  }
]
```

**Sector values**: `bank`, `telco`, `agri`, `ins`, `hotel`, `congl`, `inv`, `prop`

**Liquidity values**: `high` (large cap, actively traded), `mid` (moderate),
`thin` (illiquid — % moves may not be repeatable)

**Flag values**: `value` (P/BV < 1×), `income` (Div > 3%), `loss` (negative EPS),
`expensive` (P/BV > 20×), `thin` (illiquid market)

---

### GET /equities/volume

Per-stock weekly trading volume extracted from bar charts in weekly reports.
Enables concentration analysis.
Equivalent to MCP `GetWeeklyStockVolume`.

**MCP equivalent**: `GetWeeklyStockVolume`
**Tier**: Professional+

**Query params**: `?ticker=STANDARD` `?weeks=8`

```http
GET /api/v1/equities/volume?weeks=4
```

**Response**:
```json
[
  {
    "week_ending": "2026-02-06",
    "ticker": "STANDARD",
    "value_mk_mn": 1817.1,
    "pct_of_weekly_total": 44.3,
    "rank": 1
  },
  {
    "week_ending": "2026-02-20",
    "ticker": "FDHB",
    "value_mk_mn": 259.2,
    "pct_of_weekly_total": 23.3,
    "rank": 1
  }
]
```

**Key insight**: Total MSE volume fell 73% week-on-week (MK4.1bn → MK1.1bn)
coinciding with the February yield compression.

---

## Fixed Income

### GET /yields

Full 8-point yield curve snapshot with shape classification and optional
intramonth comparison.
Equivalent to MCP `GetYieldCurveSnapshot`.

**MCP equivalent**: `GetYieldCurveSnapshot`
**Tier**: Free (current only) · Professional+ (history + comparison)

**Query params**: `?date=2026-02-28` `?compare=2026-02-06`
`?include_auctions=true`

```http
GET /api/v1/yields?compare=2026-02-06&include_auctions=true
```

**Response**:
```json
{
  "week_ending": "2026-02-28",
  "shape": "normal",
  "curve": [
    { "tenor": "91d",  "yield": 12.00, "prior_month": 15.00, "delta_bps": -300, "year_ago": 16.00 },
    { "tenor": "182d", "yield": 15.00, "prior_month": 20.00, "delta_bps": -500, "year_ago": 20.00 },
    { "tenor": "364d", "yield": 17.90, "prior_month": 26.00, "delta_bps": -810, "year_ago": 26.00 },
    { "tenor": "2yr",  "yield": 20.65, "prior_month": 28.75, "delta_bps": -813, "year_ago": 28.75 },
    { "tenor": "3yr",  "yield": 30.00, "prior_month": 30.00, "delta_bps":    0, "year_ago": 30.00 },
    { "tenor": "5yr",  "yield": 32.00, "prior_month": 32.00, "delta_bps":    0, "year_ago": 32.00 },
    { "tenor": "7yr",  "yield": 34.00, "prior_month": 34.00, "delta_bps":    0, "year_ago": 34.00 },
    { "tenor": "10yr", "yield": 35.00, "prior_month": 35.00, "delta_bps":    0, "year_ago": 35.00 }
  ],
  "auctions": [
    {
      "month": "2026-02",
      "instrument": "TB",
      "applied_mk_bn": 258.29,
      "awarded_mk_bn": 113.48,
      "rejection_rate_pct": 72.87,
      "is_full_rejection": false,
      "avg_yield_pct": 17.55,
      "system_liquidity_mk_bn": 620.12,
      "total_raised_mk_bn": 314.96
    }
  ]
}
```

---

### GET /yields/auctions

TB and TN auction history with avg yields, system liquidity, and total
raised. Includes full rejection flags.
Equivalent to MCP `GetAuctionHistory`.

**MCP equivalent**: `GetAuctionHistory`
**Tier**: Professional (TB only) · Institutional+ (TB + TN, avg yields, liquidity)

**Query params**: `?instrument=TB` `?instrument=TN` `?months=6`
`?rejections_only=true`

```http
GET /api/v1/yields/auctions?months=6
```

**Response**:
```json
[
  {
    "month": "2026-02",
    "instrument": "TB",
    "applied_mk_bn": 258.29,
    "awarded_mk_bn": 113.48,
    "rejection_rate_pct": 72.87,
    "is_full_rejection": false,
    "avg_yield_pct": 17.55,
    "system_liquidity_mk_bn": 620.12,
    "total_raised_mk_bn": 314.96
  },
  {
    "month": "2026-02",
    "instrument": "TN",
    "applied_mk_bn": 403.97,
    "awarded_mk_bn": 103.09,
    "rejection_rate_pct": 74.00,
    "is_full_rejection": false,
    "avg_yield_pct": 30.33,
    "system_liquidity_mk_bn": null,
    "total_raised_mk_bn": null
  },
  {
    "month": "2026-02-06",
    "instrument": "TB",
    "applied_mk_bn": 145.58,
    "awarded_mk_bn": 0,
    "rejection_rate_pct": 100.0,
    "is_full_rejection": true,
    "avg_yield_pct": null,
    "system_liquidity_mk_bn": null,
    "total_raised_mk_bn": null
  }
]
```

---

### GET /yields/auctions/tenors

Per-tenor TB auction breakdown. The key leading indicator: the 91-day
share collapsed from 22.7% to 5.8% while 182-day surged from 16.1%
to 40.1% between 6 Feb and 20 Feb — the market shortening duration
ahead of the yield reset.
Equivalent to MCP `GetAuctionHistory(includePerTenor=true)`.

**MCP equivalent**: `GetAuctionHistory(includePerTenor=true)`
**Tier**: Institutional+

**Query params**: `?weeks=8` `?tenor=182-day`

```http
GET /api/v1/yields/auctions/tenors?weeks=4
```

**Response**:
```json
[
  {
    "week_ending": "2026-02-06",
    "tenor": "91-day",
    "applied_mk_bn": 33.02,
    "pct_of_total": 22.68,
    "awarded_mk_bn": 0,
    "is_full_rejection": true
  },
  {
    "week_ending": "2026-02-06",
    "tenor": "182-day",
    "applied_mk_bn": 23.49,
    "pct_of_total": 16.13,
    "awarded_mk_bn": 0,
    "is_full_rejection": true
  },
  {
    "week_ending": "2026-02-06",
    "tenor": "364-day",
    "applied_mk_bn": 89.07,
    "pct_of_total": 61.18,
    "awarded_mk_bn": 0,
    "is_full_rejection": true
  },
  {
    "week_ending": "2026-02-20",
    "tenor": "91-day",
    "applied_mk_bn": 14.85,
    "pct_of_total": 5.75,
    "awarded_mk_bn": 14.74,
    "is_full_rejection": false
  },
  {
    "week_ending": "2026-02-20",
    "tenor": "182-day",
    "applied_mk_bn": 103.58,
    "pct_of_total": 40.10,
    "awarded_mk_bn": 84.26,
    "is_full_rejection": false
  },
  {
    "week_ending": "2026-02-20",
    "tenor": "364-day",
    "applied_mk_bn": 139.86,
    "pct_of_total": 54.15,
    "awarded_mk_bn": 14.47,
    "is_full_rejection": false
  }
]
```

---

### GET /rates

Interest rate dashboard — MPR, Lombard, commercial bank reference,
overnight interbank, TB yields at all tenors, and real rate. Includes
basis point delta, budget targets, and easing/tightening/stable status.
Equivalent to MCP `ComputeRealRate` + `QueryIndicators`.

**MCP equivalent**: `ComputeRealRate` + `QueryIndicators`
**Tier**: Professional+

```http
GET /api/v1/rates
```

**Response**:
```json
[
  {
    "name": "Monetary Policy Rate",
    "value": 26.00,
    "prev": 26.00,
    "delta_bps": 0,
    "budget_target": 18.0,
    "gap_pp": 8.0,
    "status": "stable",
    "note": "Unchanged 18+ months"
  },
  {
    "name": "TB 364d Yield",
    "value": 17.90,
    "prev": 26.00,
    "delta_bps": -810,
    "budget_target": null,
    "gap_pp": null,
    "status": "easing",
    "note": "↓ 810bps in Feb alone"
  },
  {
    "name": "Overnight Interbank",
    "value": 16.50,
    "prev": 23.98,
    "delta_bps": -748,
    "budget_target": null,
    "gap_pp": null,
    "status": "easing",
    "note": "↓ 748bps within February (23.98% → 16.50%)"
  },
  {
    "name": "Real Rate (364d minus CPI)",
    "value": -7.00,
    "prev": -9.90,
    "delta_bps": null,
    "budget_target": 0,
    "gap_pp": -7.0,
    "status": "negative",
    "note": "Negative 13 consecutive months"
  }
]
```

**Status values**: `stable`, `easing` (delta < −50bps), `tightening` (delta > +50bps),
`negative` (real rate below zero)

---

## FX & Macro

### GET /fx

MK/USD, MK/GBP, MK/EUR, MK/ZAR with buy/sell spread, MoM and YoY %
changes, and trend classification.
Equivalent to MCP `QueryIndicators` filtered to exchange rates.

**MCP equivalent**: `QueryIndicators`
**Tier**: Free (current) · Professional+ (history + changes)

```http
GET /api/v1/fx
```

**Response**:
```json
[
  {
    "pair": "MK/USD",
    "rate": 1750.45,
    "buy": 1749.91,
    "sell": 1751.00,
    "prev_month": 1749.55,
    "year_ago": 1749.65,
    "mom_pct": 0.05,
    "yoy_pct": 0.05,
    "trend": "stable",
    "note": "Held 1,748–1,751 band for 13+ months. EIU projects 2,127 by 2027."
  },
  {
    "pair": "MK/ZAR",
    "rate": 113.20,
    "buy": null,
    "sell": null,
    "prev_month": 113.67,
    "year_ago": 97.04,
    "mom_pct": -0.41,
    "yoy_pct": 16.65,
    "trend": "mk_weaker",
    "note": "+16.6% YoY — significant given SA trade dependency."
  }
]
```

**Trend values**: `stable` (MoM < 0.1%), `mk_weaker` (rate rising), `mk_stronger` (rate falling)

---

### GET /macro

Macroeconomic indicator snapshot — CPI components, GDP growth, fiscal
deficit, current account, FX reserves, maize price, oil price.

**MCP equivalent**: `QueryIndicators` + `ComputeRealRate`
**Tier**: Free (headline CPI only) · Professional+ (all indicators)

```http
GET /api/v1/macro
```

**Response**:
```json
[
  {
    "indicator": "Headline CPI",
    "value": 24.9,
    "prev": 26.0,
    "unit": "%",
    "period": "Jan 26",
    "mom_delta": -1.1,
    "direction": "improving",
    "budget_target": 15.0,
    "stale": false
  },
  {
    "indicator": "FX Reserves",
    "value": 530.0,
    "prev": 523.9,
    "unit": "USD mn",
    "period": "Nov 25",
    "mom_delta": 6.1,
    "direction": "improving",
    "budget_target": null,
    "stale": true,
    "stale_note": "Last known Nov 2025 — 3+ month publication lag"
  }
]
```

**Direction**: context-aware — falling CPI is `"improving"`, falling GDP is `"deteriorating"`.

---

### GET /forecasts

Per-institution macro projections for GDP growth, CPI average, and
MK/USD — stored individually so forecast divergence is queryable.
Equivalent to MCP `GetInstitutionalForecasts`.

**MCP equivalent**: `GetInstitutionalForecasts`
**Tier**: Professional+

**Query params**: `?indicator=gdp` `?year=2026` `?institution=World+Bank`

```http
GET /api/v1/forecasts?year=2026
```

**Response**:
```json
[
  { "institution": "Govt (SONA)",  "indicator": "gdp", "forecast_year": 2026, "value": 3.8,  "unit": "%", "published_month": "2026-02", "note": "SONA address Feb 2026" },
  { "institution": "World Bank",   "indicator": "gdp", "forecast_year": 2026, "value": 2.3,  "unit": "%", "published_month": "2026-02", "note": "WB Malawi Economic Monitor" },
  { "institution": "World Bank",   "indicator": "gdp", "forecast_year": 2027, "value": 2.7,  "unit": "%", "published_month": "2026-02", "note": "WB Malawi Economic Monitor" },
  { "institution": "Oxford Econ",  "indicator": "gdp", "forecast_year": 2026, "value": 2.2,  "unit": "%", "published_month": "2026-02", "note": "Oxford Economics Feb 2026" },
  { "institution": "EIU",          "indicator": "gdp", "forecast_year": 2026, "value": 2.0,  "unit": "%", "published_month": "2026-02", "note": "EIU Country Report" },
  { "institution": "Govt (Budget)","indicator": "cpi", "forecast_year": 2026, "value": 15.0, "unit": "%", "published_month": "2026-02", "note": "End-period target FY2026/27" },
  { "institution": "Oxford Econ",  "indicator": "cpi", "forecast_year": 2026, "value": 34.8, "unit": "%", "published_month": "2026-02", "note": "Oxford Economics Feb 2026" },
  { "institution": "EIU",          "indicator": "fx",  "forecast_year": 2027, "value": 2127, "unit": "MK/USD", "published_month": "2026-02", "note": "EIU average rate forecast" }
]
```

---

## Commodities

### GET /commodities

All 17 tracked commodities across five categories: food, energy,
minerals, agro-processing, and trade. Pending commodities return
`value: null` with `status: "pending"` and source details.
Equivalent to MCP `GetCommodityPrices`.

**MCP equivalent**: `GetCommodityPrices`
**Tier**: Free (food only) · Professional+ (all categories)

**Query params**: `?commodity=maize_national` `?category=energy`
`?months=12` `?include_pending=true`

```http
GET /api/v1/commodities?category=food
```

**Response**:
```json
[
  {
    "month": "2026-01",
    "commodity": "maize_national",
    "value": 978.0,
    "unit": "MK/kg",
    "source": "IFPRI",
    "category": "food",
    "status": "live",
    "change_pct": -15.8,
    "note": "Below NFRA farmgate min of MK1,050/kg. Contra-seasonal."
  },
  {
    "month": "2026-02",
    "commodity": "oil_opec_basket",
    "value": 67.90,
    "unit": "USD/bbl",
    "source": "OPEC",
    "category": "energy",
    "status": "live",
    "change_pct": 9.0,
    "note": "↑9% MoM. −11.6% YoY from $76.81."
  },
  {
    "month": "2026-02",
    "commodity": "uranium_kayelekera",
    "value": null,
    "unit": "klbs/mo",
    "source": "Lotus Resources",
    "category": "minerals",
    "status": "pending",
    "change_pct": null,
    "note": "First export Q2 2026 — target 200,000 lbs/month. USD53mn raised Feb 2026."
  }
]
```

**Commodity vocabulary**: `maize_national`, `maize_northern`, `maize_central`,
`maize_southern`, `maize_nfra_purchase`, `fertiliser_imports`, `oil_opec_basket`,
`diesel_imports`, `petrol_imports`, `fuel_pump_diesel_mwk`, `uranium_kayelekera`,
`graphite_kasiya`, `rutile_kasiya`, `tobacco_exports`, `soybeans_exports`,
`groundnuts_exports`, `macadamia_exports`

---

### GET /commodities/milestones

Development milestone records for pre-revenue mineral projects —
Kayelekera uranium (Lotus Resources) and Kasiya graphite/rutile
(Sovereign Metals).
Equivalent to MCP `GetMineralMilestones`.

**MCP equivalent**: `GetMineralMilestones`
**Tier**: Professional+

**Query params**: `?project=kayelekera_uranium`
`?milestone_type=capital_raise`

```http
GET /api/v1/commodities/milestones
```

**Response**:
```json
[
  {
    "event_date": "2026-02-01",
    "project": "kayelekera_uranium",
    "company": "Lotus Resources",
    "milestone_type": "capital_raise",
    "value_usd_mn": 53.0,
    "description": "Raised USD53mn via 35.4mn new shares at AUD2.15. Funds sulphuric acid plant and national grid connection for 200,000 lbs/month production target. First export Q2 2026."
  },
  {
    "event_date": "2026-02-01",
    "project": "kasiya_graphite",
    "company": "Sovereign Metals",
    "milestone_type": "offtake_mou",
    "value_usd_mn": null,
    "description": "Non-binding offtake MOU with Traxys North America. Initial 40,000 MT/yr graphite concentrate, rising to 80,000 MT/yr. 6% Traxys commission."
  }
]
```

**Milestone types**: `capital_raise`, `offtake_mou`, `first_export`,
`production_target`, `regulatory`

---

### GET /commodities/tobacco

Tobacco intelligence layer: AHL Malawi weekly auction results, Zimbabwe
TIMB reference price, season status, and USDA global supply/demand
balance. The three data layers required to answer tobacco questions fully.
Equivalent to MCP `GetTobaccoAuction` + `GetTobaccoOutlook`.

**MCP equivalent**: `GetTobaccoAuction` + `GetTobaccoOutlook`
**Tier**: Institutional+

**Query params**: `?season=2026` `?weeks=8` `?include_timb=true`
`?include_usda=true`

```http
GET /api/v1/commodities/tobacco?season=2026&include_usda=true
```

**Response**:
```json
{
  "season": {
    "year": 2026,
    "status": "pre_season",
    "floors_open_date": "2026-04-07",
    "ahl_ytd_volume_mt": null,
    "ahl_ytd_avg_price_usd": null,
    "ahl_ytd_value_usd_mn": null,
    "timb_current_ref_usd_kg": 2.18,
    "outlook": "TIMB prices soft entering season — buyers cautious on global leaf stocks. Below the USD2.34 recorded at the same point in 2025."
  },
  "ahl_weekly": [],
  "timb_weekly": [
    {
      "week_ending": "2026-02-28",
      "avg_price_usd_kg": 2.18,
      "volume_mt": 4120,
      "flue_cured_pct": 87.3,
      "basis_spread": null
    }
  ],
  "usda_global": {
    "year": 2025,
    "malawi_production_mt": 112000,
    "malawi_exports_mt": 95000,
    "malawi_avg_export_price_usd_kg": 2.21,
    "world_production_mt": 6850000,
    "malawi_share_world_trade_pct": 4.2
  },
  "equity_linkages": [
    { "ticker": "PCL",    "relationship": "direct",   "note": "Tobacco value chain interests via subsidiaries" },
    { "ticker": "ILLOVO", "relationship": "indirect", "note": "Strong season → rural income → consumer spending on ILLOVO products" }
  ]
}
```

**Season status values**: `pre_season`, `active`, `closed`

**Note on AHL vs TIMB**: TIMB prices flue-cured Virginia; AHL prices burley.
Not identical leaf types but correlated through the same multinational buyer
pool. TIMB in March–April is a leading indicator for AHL prices when Lilongwe
floors open in April.

---

## Trade & Structure

### GET /trade

Monthly trade balance — exports, imports, deficit, export-to-import
ratio, and top commodity categories.
Equivalent to MCP `GetTradeBalance`.

**MCP equivalent**: `GetTradeBalance`
**Tier**: Professional+

**Query params**: `?months=6` `?category=exports`

```http
GET /api/v1/trade?months=3
```

**Response**:
```json
[
  {
    "period": "2025-12",
    "total_exports_usd_mn": 60.9,
    "total_imports_usd_mn": 332.1,
    "trade_deficit_usd_mn": 271.2,
    "export_import_ratio": 0.18,
    "top_exports": [
      { "category": "tobacco",     "value_usd_mn": 3.9,  "pct_of_total": 6.4 }
    ],
    "top_imports": [
      { "category": "fertiliser",  "value_usd_mn": 44.3, "pct_of_total": 13.3 },
      { "category": "diesel",      "value_usd_mn": 43.9, "pct_of_total": 13.2 },
      { "category": "petrol",      "value_usd_mn": 35.3, "pct_of_total": 10.6 }
    ]
  }
]
```

---

### GET /export-structure

10-year export structure series and Africa benchmark comparisons from
World Bank special topic analysis. Tracks the structural decline in
Malawi's export base since 2003.
Equivalent to MCP `GetExportStructure`.

**MCP equivalent**: `GetExportStructure`
**Tier**: Professional+

**Query params**: `?metric=benchmarks` `?start_year=2003`

```http
GET /api/v1/export-structure
```

**Response**:
```json
{
  "series": [
    { "year": 2003, "trade_deficit_pct_gdp": 6.0,  "num_products": null, "num_markets": null, "tobacco_share_pct": null },
    { "year": 2009, "trade_deficit_pct_gdp": 10.0, "num_products": 1070, "num_markets": 126,  "tobacco_share_pct": 69.0 },
    { "year": 2023, "trade_deficit_pct_gdp": 19.0, "num_products": 643,  "num_markets": 104,  "tobacco_share_pct": 61.0 }
  ],
  "benchmarks": [
    { "metric": "Export value change (2014–2023)", "malawi": "-31%",   "africa_avg": "+42%", "note": "Goods exports USD terms" },
    { "metric": "Trade deficit % GDP",             "malawi": "19%",    "africa_avg": "7%",   "note": "2023 vs 6% baseline in 2003" },
    { "metric": "Exporters per 100k people",       "malawi": "3.2",    "africa_avg": "28",   "note": "8.75× structural gap" },
    { "metric": "New exporter 1yr survival",       "malawi": "16-25%", "africa_avg": "38%",  "note": "Inability to sustain market entry" },
    { "metric": "Top 5% exporter value share",     "malawi": "82.6%",  "africa_avg": "65%",  "note": "Extreme concentration" }
  ]
}
```

---

## Banking

### GET /banking

Sector-level banking health metrics — ROE, ROA, NPL ratio, government
securities exposure. Contextualises why banking stocks trade at 50–57× P/E.
Equivalent to MCP `GetBankingSectorMetrics`.

**MCP equivalent**: `GetBankingSectorMetrics`
**Tier**: Professional+

**Query params**: `?months=12`

```http
GET /api/v1/banking
```

**Response**:
```json
{
  "period": "2026-02",
  "roe_pct": 60.9,
  "roa_pct": 7.7,
  "npl_ratio_pct": 4.6,
  "govt_sec_exposure": "high",
  "note": "Highest ROE in region. Heavy govt securities exposure crowding private sector lending. TB yield compression creates mark-to-market headwind on bank TB portfolios."
}
```

---

## Real Estate & Cross-Asset

### GET /real-estate

Rental prices and implied yields by property type and city — commercial
(office Grade A/B, retail, warehouse) and residential (2-bed, 3-bed,
4-bed executive) in Blantyre and Lilongwe.
Equivalent to MCP `GetRealEstatePrices`.

**MCP equivalent**: `GetRealEstatePrices`
**Tier**: Institutional+

**Query params**: `?city=blantyre` `?type=commercial` `?quarter=2026-Q1`

```http
GET /api/v1/real-estate?city=blantyre
```

**Response**:
```json
[
  {
    "quarter": "2026-Q1",
    "property_type": "office_a",
    "city": "blantyre",
    "area_tier": "cbd",
    "rent_value": 18.0,
    "rent_currency": "USD",
    "rent_unit": "m2_month",
    "occupancy_pct": null,
    "implied_yield_pct": 8.5,
    "capital_value_basis": "Agent estimate — comparable sales",
    "source": "Agent survey",
    "source_quality": "estimated"
  },
  {
    "quarter": "2026-Q1",
    "property_type": "residential_3bed",
    "city": "blantyre",
    "area_tier": "suburban",
    "rent_value": 350000,
    "rent_currency": "MK",
    "rent_unit": "unit_month",
    "occupancy_pct": null,
    "implied_yield_pct": 6.8,
    "capital_value_basis": "Agent estimate",
    "source": "Agent survey",
    "source_quality": "estimated"
  }
]
```

**Property types**: `office_a`, `office_b`, `retail_prime`, `warehouse`,
`residential_2bed`, `residential_3bed`, `residential_4bed`

**Source quality**: `actual` (verified transaction), `listed` (advertised price),
`estimated` (agent estimate)

**Note on currency**: Most commercial leases in Malawi are USD-denominated.
Residential rents are MK-quoted but many landlords informally peg to USD.

---

### GET /cross-asset/yields

The flagship analytical endpoint. Returns a unified yield comparison
across all asset classes: government securities (all tenors), listed
equity earnings yields, real estate rental yields, and the real rate.
Enables allocation decisions that no other Malawi platform supports.
Equivalent to MCP `GetCrossAssetYieldMatrix`.

**MCP equivalent**: `GetCrossAssetYieldMatrix`
**Tier**: Institutional+

**Query params**: `?adjust_currency=true` (converts USD yields to
MK-adjusted using EIU forecast)

```http
GET /api/v1/cross-asset/yields?adjust_currency=false
```

**Response**:
```json
{
  "as_of": "2026-02-28",
  "currency_base": "MK",
  "assets": [
    { "name": "91-day TB",              "yield_pct": 12.00, "currency": "MK",  "category": "govtsec",         "risk": "sovereign" },
    { "name": "364-day TB",             "yield_pct": 17.90, "currency": "MK",  "category": "govtsec",         "risk": "sovereign" },
    { "name": "2-year TN",              "yield_pct": 20.65, "currency": "MK",  "category": "govtsec",         "risk": "sovereign_duration" },
    { "name": "10-year TN",             "yield_pct": 35.00, "currency": "MK",  "category": "govtsec",         "risk": "sovereign_duration" },
    { "name": "STANDARD (dividend)",    "yield_pct": 3.80,  "currency": "MK",  "category": "equity",          "risk": "equity_liquid" },
    { "name": "ICON (earnings)",        "yield_pct": 22.90, "currency": "MK",  "category": "equity",          "risk": "equity_illiquid" },
    { "name": "MPICO (earnings)",       "yield_pct": 19.11, "currency": "MK",  "category": "equity",          "risk": "equity_illiquid" },
    { "name": "Office Grade A, BT",     "yield_pct": 8.50,  "currency": "USD", "category": "real_estate",     "risk": "property_vacancy" },
    { "name": "Residential 3-bed, BT",  "yield_pct": 6.80,  "currency": "MK",  "category": "real_estate",     "risk": "property_residential" },
    { "name": "Real Rate (364d−CPI)",   "yield_pct": -7.00, "currency": "MK",  "category": "macro",           "risk": "inflation" }
  ]
}
```

**Key insight (Feb 2026)**: ICON earnings yield (22.90% MK) exceeds
the 364-day TB (17.90% MK) by 300bps. However ICON trades at P/B 0.73
with thin liquidity — the illiquidity discount and operating risk must
be weighed against the yield advantage. Property (8.5% USD) is below TB
in MK terms at current FX; becomes competitive if TB compresses toward
the 15% budget target.

---

## Dashboard Integration

Replace static data arrays in `MalawiDashboard.jsx` with API calls at
mount time. Use React Query or SWR for caching and auto-refresh.

```jsx
import { useQuery } from '@tanstack/react-query'

const API = import.meta.env.VITE_API_BASE  // 'https://your-server.com/api/v1'
const KEY = import.meta.env.VITE_API_KEY

const headers = { 'X-Api-Key': KEY }

// KPI strip — refresh every 60 minutes (weekly data)
const { data: snapshot } = useQuery({
  queryKey: ['snapshot'],
  queryFn: () => fetch(`${API}/snapshot`, { headers }).then(r => r.json()),
  staleTime: 60 * 60 * 1000,
})

// Cross-asset yield matrix — refresh every 24 hours (quarterly data)
const { data: yields } = useQuery({
  queryKey: ['cross-asset-yields'],
  queryFn: () => fetch(`${API}/cross-asset/yields`, { headers }).then(r => r.json()),
  staleTime: 24 * 60 * 60 * 1000,
})

// Tobacco — refresh every 7 days off-season, every 24 hours during season
const { data: tobacco } = useQuery({
  queryKey: ['tobacco'],
  queryFn: () => fetch(`${API}/commodities/tobacco?include_timb=true`, { headers }).then(r => r.json()),
  staleTime: tobacco?.season?.status === 'active'
    ? 24 * 60 * 60 * 1000
    : 7 * 24 * 60 * 60 * 1000,
})
```

**Recommended refresh intervals**:

| Data type | Stale time | Reason |
|---|---|---|
| Snapshot, equities, yields | 60 min | Weekly reports — no intraday change |
| FX rates | 60 min | Daily RBM opening rates |
| Live prices (MERA, ESCOM) | 24 hrs | Monthly updates at most |
| Commodities (maize, oil) | 24 hrs | Monthly source data |
| Tobacco (active season) | 24 hrs | Weekly AHL bulletins |
| Tobacco (off-season) | 7 days | TIMB reference only |
| Real estate | 7 days | Quarterly source data |
| Forecasts | 30 days | Monthly publications |

---

## CORS Configuration

```csharp
// Program.cs
builder.Services.AddCors(options => {
    options.AddPolicy("DashboardPolicy", policy => {
        policy.WithOrigins(
            "http://localhost:3000",
            "https://mw-fin-intel.vercel.app",
            "https://your-production-domain.com"
        )
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

app.UseCors("DashboardPolicy");
```

---

## Tier-Based Access Control

Endpoints enforce access based on the subscription tier attached to the
API key. The middleware injects the tier into `ctx.Items["tier"]` and
controllers check it before returning restricted fields or applying
data depth limits.

| Tier | Historical depth | Restricted endpoints |
|---|---|---|
| Free | 3 months | `/yields/auctions`, `/yields/auctions/tenors`, `/commodities/tobacco`, `/real-estate`, `/cross-asset/yields` |
| Professional | 12 months | `/yields/auctions/tenors`, `/commodities/tobacco`, `/real-estate`, `/cross-asset/yields` |
| Institutional | 3 years | `/cross-asset/yields` (included), all others open |
| Enterprise | Full history | All endpoints + API key |

```csharp
// Example tier check in a controller
[HttpGet]
public IActionResult GetTenorBreakdown()
{
    var tier = (string)HttpContext.Items["tier"];
    if (tier is "free" or "professional")
        return StatusCode(403, new { error = "Institutional tier required" });

    var data = _auctionRepo.GetPerTenorBreakdown();
    return Ok(data);
}
```

---

## Error Responses

All errors return a consistent JSON shape:

```json
{ "error": "string", "message": "string" }
```

| Status | Meaning |
|---|---|
| `400` | Bad request — invalid query parameter |
| `401` | Missing or invalid `X-Api-Key` |
| `403` | Valid key but insufficient tier for this endpoint |
| `404` | Resource not found (e.g. unknown ticker) |
| `429` | Rate limit exceeded |
| `500` | Internal server error |

**Rate limits** (per API key):

| Tier | Requests / hour |
|---|---|
| Free | 60 |
| Professional | 300 |
| Institutional | 1,000 |
| Enterprise | 5,000 |

---
