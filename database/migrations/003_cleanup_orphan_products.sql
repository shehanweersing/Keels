-- PriceWise LK: Safe Cleanup of Orphan & Duplicate Canonical Products
-- Migration: 003_cleanup_orphan_products.sql
-- DO NOT EXECUTE WITHOUT VERIFIED BACKUP AND EXPLICIT APPROVAL.
--
-- Background:
-- Prior to fixing SupabaseStorageService.cs, repeated collector runs created new
-- duplicate rows in `products` and reassigned `store_products.product_id`, leaving
-- ~1,376 orphan rows in `products` (0 references in `store_products`).
--
-- Foreign-key safety:
-- - `store_products.product_id` references `products(id) ON DELETE SET NULL`
-- - `product_matches.product_id` references `products(id) ON DELETE CASCADE`
-- This script ONLY deletes rows in `products` that have ZERO references in `store_products`,
-- preserving 100% of active store listings, price observations, and valid canonical IDs.

-- STEP 1: Create safety backup tables before any deletion
CREATE TABLE IF NOT EXISTS public._backup_products_20261125 AS
SELECT * FROM public.products;

CREATE TABLE IF NOT EXISTS public._backup_product_matches_20261125 AS
SELECT * FROM public.product_matches;

-- STEP 2: Read-only preview (verify counts before committing)
SELECT
    (SELECT COUNT(*) FROM public.products) AS total_products_before,
    (SELECT COUNT(*) FROM public.products p
     WHERE NOT EXISTS (
         SELECT 1 FROM public.store_products sp WHERE sp.product_id = p.id
     )) AS orphan_products_to_delete,
    (SELECT COUNT(*) FROM public.product_matches pm
     WHERE NOT EXISTS (
         SELECT 1 FROM public.store_products sp WHERE sp.product_id = pm.product_id
     )) AS orphan_product_matches_to_cascade;

-- STEP 3: Transactional cleanup (uncomment COMMIT after verifying preview counts)
BEGIN;

DELETE FROM public.products p
WHERE NOT EXISTS (
    SELECT 1
    FROM public.store_products sp
    WHERE sp.product_id = p.id
);

-- Verify post-cleanup integrity: 0 orphans, 0 broken store_products links
SELECT
    (SELECT COUNT(*) FROM public.products) AS total_products_after,
    (SELECT COUNT(*) FROM public.store_products WHERE product_id IS NULL) AS broken_store_products,
    (SELECT COUNT(*) FROM public.price_observations) AS preserved_price_observations;

COMMIT;
