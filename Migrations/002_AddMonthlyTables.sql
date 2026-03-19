-- 002_AddMonthlyTables.sql
-- Tables for monthly PDF ingestion

CREATE TABLE IF NOT EXISTS stock_valuations (
    id BIGSERIAL PRIMARY KEY,
    report_date DATE NOT NULL,
    ticker TEXT NOT NULL,
    pe_ratio DOUBLE PRECISION,
    pb_ratio DOUBLE PRECISION,
    dividend_yield DOUBLE PRECISION,
    market_cap DOUBLE PRECISION,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS institutional_forecasts (
    id BIGSERIAL PRIMARY KEY,
    report_date DATE NOT NULL,
    institution TEXT NOT NULL,
    indicator TEXT NOT NULL,
    forecast_year INTEGER NOT NULL,
    forecast_value DOUBLE PRECISION,
    unit TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS trade_flows (
    id BIGSERIAL PRIMARY KEY,
    report_date DATE NOT NULL,
    flow_type TEXT NOT NULL,
    category TEXT,
    value DOUBLE PRECISION,
    unit TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS banking_metrics (
    id BIGSERIAL PRIMARY KEY,
    report_date DATE NOT NULL,
    metric_name TEXT NOT NULL,
    metric_value DOUBLE PRECISION,
    unit TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS export_structure (
    id BIGSERIAL PRIMARY KEY,
    report_date DATE NOT NULL,
    commodity TEXT NOT NULL,
    export_value DOUBLE PRECISION,
    pct_of_total DOUBLE PRECISION,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS monthly_market_activity (
    id BIGSERIAL PRIMARY KEY,
    report_date DATE NOT NULL,
    metric_name TEXT NOT NULL,
    metric_value DOUBLE PRECISION,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
