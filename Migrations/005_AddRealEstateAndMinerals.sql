-- 005_AddRealEstateAndMinerals.sql
-- Real estate yields and mineral milestone tracking

CREATE TABLE IF NOT EXISTS real_estate_prices (
    id BIGSERIAL PRIMARY KEY,
    report_date DATE NOT NULL,
    city TEXT NOT NULL,
    property_type TEXT NOT NULL,
    bedrooms INTEGER,
    monthly_rent DOUBLE PRECISION,
    currency TEXT DEFAULT 'MWK',
    source TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS mineral_milestones (
    id BIGSERIAL PRIMARY KEY,
    event_date DATE NOT NULL,
    project_name TEXT NOT NULL,
    company TEXT NOT NULL,
    mineral TEXT NOT NULL,
    milestone_type TEXT NOT NULL,
    description TEXT,
    source_url TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_real_estate_city ON real_estate_prices (city);
CREATE INDEX IF NOT EXISTS idx_mineral_milestones_project ON mineral_milestones (project_name);
