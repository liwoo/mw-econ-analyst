-- 006_AddUniqueConstraints.sql
-- Required for idempotent upserts (INSERT ... ON CONFLICT DO UPDATE)

ALTER TABLE financial_indicators
    ADD CONSTRAINT uq_indicator UNIQUE (report_date, indicator_name);

ALTER TABLE auction_events
    ADD CONSTRAINT uq_auction UNIQUE (report_date, tenor);

ALTER TABLE stock_valuations
    ADD CONSTRAINT uq_valuation UNIQUE (report_date, ticker);

ALTER TABLE commodity_prices
    ADD CONSTRAINT uq_commodity UNIQUE (report_date, commodity_name, region);

ALTER TABLE institutional_forecasts
    ADD CONSTRAINT uq_forecast UNIQUE (report_date, institution, indicator, forecast_year);

ALTER TABLE banking_metrics
    ADD CONSTRAINT uq_banking UNIQUE (report_date, metric_name);

ALTER TABLE export_structure
    ADD CONSTRAINT uq_export UNIQUE (report_date, commodity);

ALTER TABLE weekly_stock_volume
    ADD CONSTRAINT uq_stock_volume UNIQUE (report_date, ticker);

ALTER TABLE monthly_market_activity
    ADD CONSTRAINT uq_monthly_activity UNIQUE (report_date, metric_name);
