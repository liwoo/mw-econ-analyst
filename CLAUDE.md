# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Malawi Financial Intelligence — an MCP server + REST API exposing Malawi's financial market data (equities, fixed income, FX, commodities, tobacco, real estate, macro) as structured tools for AI assistants. Built with .NET 9 and the official MCP C# SDK. Data is sourced from Bridgepath Capital PDF reports (weekly/monthly/annual) and external live feeds.

**31 MCP tools · 20 REST endpoints · 17 DB tables · 12 tool classes**

## Key Commands

```bash
# Build
dotnet restore && dotnet build

# Run MCP server (stdio for local dev / Claude Desktop)
dotnet run --project MalawiFinancialMcp -- --transport stdio

# Run MCP server (HTTP for production / ChatGPT / Claude.ai)
dotnet run --project MalawiFinancialMcp -- --transport http --port 5000

# Run REST API server
dotnet run --project MalawiFinancialApi -- --port 5001

# Run tests
dotnet test

# Run scraper (downloads Bridgepath Capital PDFs)
cd scraper && pip install requests beautifulsoup4 && python scraper.py

# Database migrations (TimescaleDB/PostgreSQL)
psql postgresql://postgres:malawi_agent@localhost:5432/malawi_financial \
  -f Migrations/001_InitSchema.sql \
  -f Migrations/002_AddMonthlyTables.sql \
  -f Migrations/003_AddCommodityExpansion.sql \
  -f Migrations/004_AddTobaccoTables.sql \
  -f Migrations/005_AddRealEstateAndMinerals.sql

# Ingest PDFs manually
dotnet run -- ingest --type weekly --directory ./data/raw/weekly --backfill
dotnet run -- ingest --type monthly --directory ./data/raw/monthly --backfill
dotnet run -- ingest --type budget --file ./data/raw/budget/malawi_budget_brief_2026_27.pdf
```

## Architecture

The system has four layers:

1. **Ingestion Pipeline** — PDF ingesters (`WeeklyPdfIngester`, `MonthlyPdfIngester`, `BudgetIngester`) parse Bridgepath Capital reports via Docling. Each runs domain-specific extractors (AppendixExtractor, ValuationExtractor, CommodityExtractor, etc.). A `LiveDataPipeline` handles external feeds (tobacco auctions, fuel prices, real estate). A FileWatcher auto-ingests PDFs dropped into watch directories.

2. **Data Stores** — TimescaleDB (PostgreSQL) with 17 tables (`financial_indicators` as hypertable, `stock_valuations`, `commodity_prices`, `auction_events`, `institutional_forecasts`, etc.) plus an in-memory BM25 index for narrative search. Qdrant is only needed for budget ingestion (ColQwen2 visual embeddings).

3. **MCP Server** (`MalawiFinancialMcp`) — 31 tools across 12 `[McpServerToolType]` classes. Supports stdio and HTTP transports. Connects to ChatGPT, Claude, Copilot, Cursor.

4. **REST API** (`MalawiFinancialApi`) — 20 endpoints under `/api/v1` with tier-based access control (Free/Professional/Institutional/Enterprise). Powers the React dashboard.

## Key Documentation Files

- `MCP.md` — All 31 tool definitions, coverage matrix (55 questions), statistical guardrails, DB table schemas
- `REST.md` — All 20 REST endpoints, request/response shapes, tier access, rate limits
- `openapi.json` — OpenAPI 3.0.3 spec for the REST API

## Data Directory

The `data/` directory contains scraped Bridgepath Capital PDFs organized by `{month} {year}/weekly/` with monthly economic reports at the month level and weekly market updates in the `weekly/` subdirectory. The scraper (`scraper/scraper.py`) walks the Bridgepath Capital website backward month-by-month.

## Important Patterns

- **Repository pattern**: Each DB table has an interface + implementation pair in `Data/Repositories/` (e.g., `IIndicatorRepository` / `IndicatorRepository`)
- **Extractor pattern**: Each data type has a dedicated extractor in `Ingestion/` that parses specific sections of PDFs
- **Statistical guardrails**: Correlation tools enforce minimum observations (15), near-zero variance checks, and significance level (0.05) — configured in `appsettings.json` under `Correlation`
- **Signal detection thresholds**: MASI move (5%), yield shift (100bps), FX move (1%), oversubscription (5x) — configured under `SignalDetector`
