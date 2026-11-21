-- PriceWise LK: Initial Metadata Seeder
-- STRICT COMPLIANCE: Contains ONLY confirmed supermarket platforms and verified store locations.
-- NO fictional products, sample prices, or dummy inventory records.

-- 1. Insert Verified Supermarket Entities
INSERT INTO supermarkets (id, name, slug, website_url, integration_status, status_reason)
VALUES 
    (
        'a1000000-0000-0000-0000-000000000001',
        'Cargills Food City',
        'cargills',
        'https://cargillsonline.com/Web/',
        'active',
        'Verified live public JSON search API with full product metadata and exact LKR pricing.'
    ),
    (
        'a2000000-0000-0000-0000-000000000002',
        'Arpico Supercentre',
        'arpico',
        'https://myarpico.com/shop/',
        'active',
        'Verified live public OpenCart search catalog permitted under robots.txt content signals.'
    ),
    (
        'a3000000-0000-0000-0000-000000000003',
        'Keells Super',
        'keells',
        'https://www.keellssuper.com/',
        'blocked',
        'Direct API calls to zebraliveback require encrypted client session tokens and anti-bot challenge cookies. Automated collection blocked pending authorized enterprise data feed.'
    )
ON CONFLICT (slug) DO UPDATE SET
    website_url = EXCLUDED.website_url,
    integration_status = EXCLUDED.integration_status,
    status_reason = EXCLUDED.status_reason;

-- 2. Insert Confirmed Store Locations (Real fulfillment stores)
INSERT INTO store_locations (id, supermarket_id, location_name, location_type, delivery_area, external_store_id)
VALUES
    (
        'b1000000-0000-0000-0000-000000000001',
        'a1000000-0000-0000-0000-000000000001',
        'Cargills Online Colombo Central',
        'online_fulfillment',
        'Colombo 01-15 & Greater Colombo Suburbs',
        '1031'
    ),
    (
        'b2000000-0000-0000-0000-000000000002',
        'a2000000-0000-0000-0000-000000000002',
        'Arpico Supercentre Hyde Park Corner',
        'physical_store',
        'Colombo District & Western Province',
        '1'
    )
ON CONFLICT (supermarket_id, external_store_id) DO NOTHING;
