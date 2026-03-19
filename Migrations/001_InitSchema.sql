-- 001_InitSchema.sql
-- Core tables for weekly PDF ingestion

CREATE EXTENSION IF NOT EXISTS timescaledb;

CREATE TABLE IF NOT EXISTS financial_indicators (
    id BIGSERIAL,
    report_date DATE NOT NULL,
    indicator_name TEXT NOT NULL,
    indicator_value DOUBLE PRECISION,
    unit TEXT,
    category TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (id, report_date)
);

SELECT create_hypertable('financial_indicators', 'report_date', if_not_exists => TRUE);

CREATE TABLE IF NOT EXISTS market_events (
    id BIGSERIAL PRIMARY KEY,
    report_date DATE NOT NULL,
    event_text TEXT NOT NULL,
    source_type TEXT NOT NULL DEFAULT 'weekly',
    entities JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS auction_events (
    id BIGSERIAL PRIMARY KEY,
    report_date DATE NOT NULL,
    tenor TEXT NOT NULL,
    offered DOUBLE PRECISION,
    applied DOUBLE PRECISION,
    allotted DOUBLE PRECISION,
    yield DOUBLE PRECISION,
    pct_of_total_applications DOUBLE PRECISION,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS data_freshness (
    id SERIAL PRIMARY KEY,
    source_name TEXT NOT NULL UNIQUE,
    last_report_date DATE,
    last_ingested_at TIMESTAMPTZ,
    record_count INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS weekly_stock_volume (
    id BIGSERIAL PRIMARY KEY,
    report_date DATE NOT NULL,
    ticker TEXT NOT NULL,
    volume BIGINT,
    turnover DOUBLE PRECISION,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
