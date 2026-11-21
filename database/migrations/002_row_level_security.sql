-- PriceWise LK: Row Level Security (RLS) Policies
-- Migration: 002_row_level_security.sql

-- Enable RLS on all operational tables
ALTER TABLE supermarkets ENABLE ROW LEVEL SECURITY;
ALTER TABLE store_locations ENABLE ROW LEVEL SECURITY;
ALTER TABLE products ENABLE ROW LEVEL SECURITY;
ALTER TABLE store_products ENABLE ROW LEVEL SECURITY;
ALTER TABLE price_observations ENABLE ROW LEVEL SECURITY;
ALTER TABLE collector_runs ENABLE ROW LEVEL SECURITY;
ALTER TABLE product_matches ENABLE ROW LEVEL SECURITY;

-- 1. Public Read-Only Access (anon and authenticated users can only SELECT)
CREATE POLICY "Public read-only supermarkets" ON supermarkets
    FOR SELECT TO anon, authenticated USING (true);

CREATE POLICY "Public read-only store_locations" ON store_locations
    FOR SELECT TO anon, authenticated USING (true);

CREATE POLICY "Public read-only products" ON products
    FOR SELECT TO anon, authenticated USING (true);

CREATE POLICY "Public read-only store_products" ON store_products
    FOR SELECT TO anon, authenticated USING (true);

CREATE POLICY "Public read-only price_observations" ON price_observations
    FOR SELECT TO anon, authenticated USING (true);

CREATE POLICY "Public read-only collector_runs" ON collector_runs
    FOR SELECT TO anon, authenticated USING (true);

CREATE POLICY "Public read-only product_matches" ON product_matches
    FOR SELECT TO anon, authenticated USING (true);

-- 2. Collector Privileged Access (Service Role full read/write)
-- Note: In Supabase, the service_role key automatically bypasses RLS by default.
-- However, explicit policies ensure defensive lockdown if service role operates under strict RLS.

CREATE POLICY "Service role full access supermarkets" ON supermarkets
    FOR ALL TO service_role USING (true) WITH CHECK (true);

CREATE POLICY "Service role full access store_locations" ON store_locations
    FOR ALL TO service_role USING (true) WITH CHECK (true);

CREATE POLICY "Service role full access products" ON products
    FOR ALL TO service_role USING (true) WITH CHECK (true);

CREATE POLICY "Service role full access store_products" ON store_products
    FOR ALL TO service_role USING (true) WITH CHECK (true);

CREATE POLICY "Service role full access price_observations" ON price_observations
    FOR ALL TO service_role USING (true) WITH CHECK (true);

CREATE POLICY "Service role full access collector_runs" ON collector_runs
    FOR ALL TO service_role USING (true) WITH CHECK (true);

CREATE POLICY "Service role full access product_matches" ON product_matches
    FOR ALL TO service_role USING (true) WITH CHECK (true);
