# REST API — Endpoint Reference

> Documentation for the `MalawiFinancialApi` REST layer that powers
> the React dashboard and any non-AI client. For MCP tool documentation
> see [MCP.md](./MCP.md). For setup see the main [README](./README.md).

---

## Table of Contents

- [Architecture](#architecture)
- [Project Structure](#project-structure-addition)
- [Endpoints](#endpoints)
  - [GET /api/v1/snapshot](#get-apiv1snapshot)
  - [GET /api/v1/equities](#get-apiv1equities)
  - [GET /api/v1/yields](#get-apiv1yields)
  - [GET /api/v1/fx](#get-apiv1fx)
  - [GET /api/v1/rates](#get-apiv1rates)
  - [GET /api/v1/macro](#get-apiv1macro)
  - [GET /api/v1/commodities](#get-apiv1commodities)
  - [GET /api/v1/banking](#get-apiv1banking)
  - [GET /api/v1/trade](#get-apiv1trade)
  - [GET /api/v1/indices](#get-apiv1indices)
- [Dashboard Integration](#dashboard-integration)
- [CORS Configuration](#cors-configuration)
- [Authentication](#authentication)

---

## REST API Layer — Powering the Analytics Dashboard

The MCP server is designed for AI assistant consumption. To power a
React dashboard or any non-AI client, a thin REST API layer sits in
front of the same TimescaleDB and BM25 data stores. This is implemented
as a separate ASP.NET Core controller project that shares the repository
layer with the MCP server.

### Architecture

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

Both servers share the same `IIndicatorRepository`,
`IMarketEventRepository`, `IValuationRepository`,
`ICommodityRepository`, and `ICorrelationService` — no duplication
of data access logic. Add the REST project to `docker-compose.yml`
alongside the MCP server.

### Project Structure Addition

```
MalawiFinancialApi/                  ← new sibling project
├── MalawiFinancialApi.csproj
├── Program.cs                       # Minimal API setup, CORS, auth
├── Controllers/
│   ├── SnapshotController.cs        # GET /api/v1/snapshot
│   ├── EquitiesController.cs        # GET /api/v1/equities
│   ├── YieldsController.cs          # GET /api/v1/yields
│   ├── FxController.cs              # GET /api/v1/fx
│   ├── RatesController.cs           # GET /api/v1/rates
│   ├── MacroController.cs           # GET /api/v1/macro
│   ├── CommoditiesController.cs     # GET /api/v1/commodities
│   ├── BankingController.cs         # GET /api/v1/banking
│   └── TradeController.cs           # GET /api/v1/trade
└── DTOs/                            # Response shape contracts
    ├── SnapshotDto.cs
    ├── EquityDto.cs
    └── ...
```

### Endpoints

All endpoints return `application/json`. Authentication via API key
header `X-Api-Key`. All time-series endpoints accept optional
`?from=yyyy-MM-dd&to=yyyy-MM-dd` query parameters.

#### `GET /api/v1/snapshot`
Latest week's complete indicator snapshot — the dashboard KPI strip.
Equivalent to MCP `GetLatestSnapshot`.

```json
{
  "week_ending": "2026-02-28",
  "masi": { "value": 574680, "ytd_pct": -3.91, "prior_year_ytd_pct": 64.92 },
  "cpi": { "headline": 24.9, "food": 22.1, "non_food": 29.8 },
  "mpr": 26.00,
  "tb_364d": { "yield": 17.90, "prior_month": 26.00, "delta_bps": -810 },
  "real_rate_364d": -7.00,
  "mk_usd": 1750.45
}
```

---

#### `GET /api/v1/equities`
All 16 MSE equities with price, valuation metrics, and liquidity.
Equivalent to MCP `GetStockHistory` + `GetValuationMetrics`.

Query params: `?sector=bank` `?sort=mom_pct` `?order=desc`

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

---

#### `GET /api/v1/yields`
Full yield curve snapshot + TB auction history.
Equivalent to MCP `GetYieldCurveSnapshot` + `GetAuctionHistory`.

Query params: `?date=2026-02-28` `?compare=2026-02-06`
`?include_auctions=true`

```json
{
  "week_ending": "2026-02-28",
  "curve": [
    { "tenor": "91d",  "yield": 12.00, "prior_month": 15.00, "delta_bps": -300 },
    { "tenor": "182d", "yield": 15.00, "prior_month": 20.00, "delta_bps": -500 },
    { "tenor": "364d", "yield": 17.90, "prior_month": 26.00, "delta_bps": -810 }
  ],
  "shape": "normal",
  "auctions": [
    { "week": "20-Feb", "applied_mk_bn": 258.29, "awarded_mk_bn": 113.48,
      "acceptance_rate_pct": 44.0, "is_full_rejection": false }
  ]
}
```

---

#### `GET /api/v1/fx`
FX rates with MoM and YoY changes.
Equivalent to MCP `QueryIndicators` filtered to exchange rates.

```json
[
  { "pair": "MK/USD", "rate": 1750.45, "buy": 1749.91, "sell": 1751.00,
    "prev_month": 1749.55, "year_ago": 1749.65,
    "mom_pct": 0.05, "yoy_pct": 0.05, "trend": "stable" }
]
```

---

#### `GET /api/v1/rates`
Interest rate dashboard — MPR, TB yields, interbank, reference rates,
real rate.
Equivalent to MCP `ComputeRealRate` + `QueryIndicators`.

```json
[
  { "name": "MPR",          "value": 26.00, "prev": 26.00,
    "delta_bps": 0, "budget_target": 18.0, "gap_pp": 8.0, "status": "stable" },
  { "name": "TB 364d",      "value": 17.90, "prev": 26.00,
    "delta_bps": -810, "budget_target": null, "gap_pp": null, "status": "easing" },
  { "name": "Real Rate",    "value": -7.00, "prev": -9.90,
    "delta_bps": null, "budget_target": 0, "gap_pp": -7.0, "status": "negative" }
]
```

---

#### `GET /api/v1/macro`
Macroeconomic indicator snapshot including inflation, GDP, fiscal, FX
reserves.

```json
[
  { "indicator": "Headline CPI", "value": 24.9, "prev": 26.0,
    "unit": "%", "period": "Jan 26", "mom_delta": -1.1,
    "direction": "improving", "budget_target": 15.0 }
]
```

---

#### `GET /api/v1/commodities`
Maize prices (national + regional) and oil price.
Equivalent to MCP `GetCommodityPrices`.

Query params: `?commodity=maize` `?commodity=oil` `?months=12`

```json
{
  "maize": {
    "national": { "value": 978, "unit": "MK/kg", "period": "Jan 26",
                  "mom_pct": -15.8, "farmgate_min": 1050 },
    "regions": {
      "northern": 923, "central": 950, "southern": 1022
    }
  },
  "oil": {
    "opec_basket": { "value": 67.90, "unit": "USD/bbl", "period": "Feb 26",
                     "mom_pct": 9.0, "yoy_pct": -11.6 }
  }
}
```

---

#### `GET /api/v1/banking`
Banking sector health metrics.
Equivalent to MCP `GetBankingSectorMetrics`.

```json
{
  "period": "Feb 2026",
  "roe_pct": 60.9,
  "roa_pct": 7.7,
  "npl_ratio_pct": 4.6,
  "govt_sec_exposure": "high",
  "note": "Highest ROE in region. Heavy govt securities exposure crowding private lending."
}
```

---

#### `GET /api/v1/trade`
Trade balance — exports, imports, deficit, commodity breakdown.
Equivalent to MCP `GetTradeBalance`.

Query params: `?months=6`

```json
{
  "period": "Dec 2025",
  "total_exports_usd_mn": 60.9,
  "total_imports_usd_mn": 332.1,
  "trade_deficit_usd_mn": 271.2,
  "export_import_ratio": 0.18,
  "top_imports": [
    { "category": "fertiliser", "value_usd_mn": 44.3, "pct_of_total": 13.3 },
    { "category": "diesel",     "value_usd_mn": 43.9, "pct_of_total": 13.2 },
    { "category": "petrol",     "value_usd_mn": 35.3, "pct_of_total": 10.6 }
  ],
  "top_exports": [
    { "category": "tobacco", "value_usd_mn": 3.9, "pct_of_total": 6.4 }
  ]
}
```

---

#### `GET /api/v1/indices`
MASI, DSI, and FSI YTD returns with divergence metrics.
Equivalent to MCP `GetIndexDivergence`.

Query params: `?months=13`

```json
[
  { "month": "2026-02", "masi_ytd": -3.91, "dsi_ytd": -0.75,
    "fsi_ytd": -13.69, "fsi_masi_spread": -9.78 }
]
```

---

### Dashboard Integration

The React dashboard (`MalawiDashboard.jsx`) replaces its static data
arrays with API calls at mount time:

```jsx
// Example — replace static KPI_DATA with live fetch
const [snapshot, setSnapshot] = useState(null)

useEffect(() => {
  fetch('/api/v1/snapshot', {
    headers: { 'X-Api-Key': import.meta.env.VITE_API_KEY }
  })
    .then(r => r.json())
    .then(setSnapshot)
}, [])
```

For time-series charts, use React Query or SWR to cache responses
and auto-refresh on a configurable interval (suggested: 60 minutes
for weekly data, 24 hours for monthly data).

### CORS Configuration

```csharp
// Program.cs — REST API server
builder.Services.AddCors(options => {
    options.AddPolicy("DashboardPolicy", policy => {
        policy.WithOrigins(
            "http://localhost:3000",           // local dev
            "https://your-dashboard.com"       // production
        )
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});
```

### Authentication

For internal use, API key authentication is sufficient:

```csharp
// Simple API key middleware
app.Use(async (ctx, next) => {
    if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var key) ||
        key != builder.Configuration["ApiKey"]) {
        ctx.Response.StatusCode = 401;
        return;
    }
    await next();
});
```

For external or multi-tenant deployment, upgrade to OAuth 2.0 with
the same Auth0 or Entra ID provider used for the MCP server.

---
