-- 004_AddTobaccoTables.sql
-- Tobacco intelligence layer (AHL + TIMB + USDA)

CREATE TABLE IF NOT EXISTS tobacco_ahl_auctions (
    id BIGSERIAL PRIMARY KEY,
    auction_date DATE NOT NULL,
    auction_week INTEGER,
    season INTEGER NOT NULL,
    grade TEXT,
    volume_kg DOUBLE PRECISION,
    avg_price_usd DOUBLE PRECISION,
    total_value_usd DOUBLE PRECISION,
    rejection_rate DOUBLE PRECISION,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS tobacco_timb_reference (
    id BIGSERIAL PRIMARY KEY,
    reference_date DATE NOT NULL,
    season INTEGER NOT NULL,
    grade TEXT,
    reference_price_usd DOUBLE PRECISION,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS tobacco_global_balance (
    id BIGSERIAL PRIMARY KEY,
    year INTEGER NOT NULL,
    country TEXT NOT NULL,
    production_tonnes DOUBLE PRECISION,
    exports_tonnes DOUBLE PRECISION,
    imports_tonnes DOUBLE PRECISION,
    consumption_tonnes DOUBLE PRECISION,
    source TEXT DEFAULT 'USDA FAS',
    created_at TIMESTAMPTZ DEFAULT NOW()
);
