-- 003_AddCommodityExpansion.sql
-- Expanded commodity tracking (2 -> 17 commodities, 5 categories)

CREATE TABLE IF NOT EXISTS commodity_prices (
    id BIGSERIAL PRIMARY KEY,
    report_date DATE NOT NULL,
    commodity_name TEXT NOT NULL,
    category TEXT NOT NULL,
    price DOUBLE PRECISION,
    unit TEXT,
    source TEXT,
    region TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_commodity_prices_date ON commodity_prices (report_date);
CREATE INDEX IF NOT EXISTS idx_commodity_prices_name ON commodity_prices (commodity_name);
CREATE INDEX IF NOT EXISTS idx_commodity_prices_category ON commodity_prices (category);
