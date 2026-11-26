# PriceWise LK — Data Integrity & Security Remediation Report

**Date:** 2026-11-25  
**Scope:** Priorities 1–6 (Credential Security, Canonical Product Duplication Fix, Safe DB Cleanup Plan, Zero-Dummy-Data Fixes, Cross-Store Product Matching, End-to-End Verification).

---

## 1. Exact Root Causes & Files Modified

### Root Cause A: Canonical Product Duplication & Orphan Cascade
- **Cause 1:** [SupabaseStorageService.cs](file:///c:/Users/sheha/Desktop/Keels/src/collector/PriceWise.Collector/Storage/SupabaseStorageService.cs) previously fetched only `product_id` from `store_products` and excluded already-linked canonical IDs from `candidateProducts` *before* checking whether `(supermarket_id, source_product_id)` already existed. Consequently, every repeat run treated existing SKUs as new products, inserted duplicate rows into `products`, and reassigned `store_products.product_id`—leaving `1,376` orphan rows in `products`.
- **Cause 2:** `GetCanonicalProductsAsync()` queried `products?select=*` without PostgREST pagination (`limit`/`offset`), silently capping reads at 1,000 rows.
- **Fix:** Updated `SupabaseStorageService.cs` and [LocalStorageService.cs](file:///c:/Users/sheha/Desktop/Keels/src/collector/PriceWise.Collector/Storage/LocalStorageService.cs) to paginate `GetCanonicalProductsAsync()` and `store_products` with `limit=1000&offset=...`, look up `(supermarket_id, source_product_id)` first, and reuse the existing `product_id` across repeated collections.

### Root Cause B: Unverified Defaults & Production JSON Fallback
- **Files Modified:**
  - [CargillsCollector.cs](file:///c:/Users/sheha/Desktop/Keels/src/collector/PriceWise.Collector/Adapters/CargillsCollector.cs) & [ArpicoCollector.cs](file:///c:/Users/sheha/Desktop/Keels/src/collector/PriceWise.Collector/Adapters/ArpicoCollector.cs): Changed hardcoded `AvailabilityStatus = "in_stock"` to `"unknown"` (neither source exposes verified stock counts in search endpoints); changed unverified quantity fallback from `1 pcs` to `0 pcs` (unspecified); delegated brand extraction to `ProductMatcher.ExtractBrandFromName`.
  - [KeellsCollector.cs](file:///c:/Users/sheha/Desktop/Keels/src/collector/PriceWise.Collector/Adapters/KeellsCollector.cs): Changed unverified quantity fallback to `0 pcs` and delegated brand extraction to `ProductMatcher.ExtractBrandFromName`.
  - [ProductMatcher.cs](file:///c:/Users/sheha/Desktop/Keels/src/collector/PriceWise.Collector/Matching/ProductMatcher.cs): Expanded Sri Lankan brand dictionary (`KnownBrands`), added grocery abbreviation normalization (`AbbreviationMap`), replaced `"Generic"` fallback with `"Unknown"`, and prevented auto-merging items with `"Unknown"` brand or `Quantity <= 0`.
  - [supabase.ts](file:///c:/Users/sheha/Desktop/Keels/src/frontend/src/lib/supabase.ts): Blocked silent fallback to `/data/*.json` when `import.meta.env.PROD` is true.
  - [ProductCard.tsx](file:///c:/Users/sheha/Desktop/Keels/src/frontend/src/components/ProductCard.tsx), [DetailsPage.tsx](file:///c:/Users/sheha/Desktop/Keels/src/frontend/src/pages/DetailsPage.tsx), [HomePage.tsx](file:///c:/Users/sheha/Desktop/Keels/src/frontend/src/pages/HomePage.tsx): Replaced `0 pcs` fallback display with `"Unit unspecified"` and updated hero text to include Keells Super.
  - [StorageTests.cs](file:///c:/Users/sheha/Desktop/Keels/src/collector/PriceWise.Collector.Tests/StorageTests.cs): Added `SaveCollectedProductsAsync_RepeatedCollection_IsIdempotent_AndReusesCanonicalProductId`.
  - [003_cleanup_orphan_products.sql](file:///c:/Users/sheha/Desktop/Keels/database/migrations/003_cleanup_orphan_products.sql): Created safe, reviewed cleanup script (not executed).

---

## 2. Security Findings (Without Exposing Credentials)

- **Git Tracking & History:** `.env` is gitignored and **not** tracked in Git. Git history commits `e2ec500` and `35d703c` contain only unit-test mock strings (`sb_secret_test_mock_identifier_for_unit_test`, `sb_secret_abcdef1234567890`) in `StorageTests.cs`, not real secrets.
- **Local `.env` & Previous Build Artifact:** Earlier, `.env` had `VITE_SUPABASE_ANON_KEY` set to `ssb_secret_mQi...` (containing the `service_role` secret), which was embedded into the gitignored `src/frontend/dist/assets/index-7KaCACva.js` build.
- **Remediation:** `.env` has been corrected to use the public `sb_publishable_...` key for `VITE_SUPABASE_ANON_KEY`, `supabase.ts` now refuses `sb_secret_*`/`ssb_secret_*` keys in the browser, and `src/frontend/dist` was rebuilt cleanly with zero secret tokens.
- **Recommended Rotation:** Because the `sb_secret_` value was briefly compiled into a local `dist/` bundle, rotate the `service_role` / secret key in **Supabase Dashboard → Project Settings → API Keys** and update the `SUPABASE_SERVICE_ROLE_KEY` GitHub Secret + local `.env`.

---

## 3. Automated Test Results

- **`.NET 10 Test Suite`** (`dotnet test src/collector/PriceWise.sln --verbosity normal`): **38 passed, 0 failed, 0 skipped** (includes new `SaveCollectedProductsAsync_RepeatedCollection_IsIdempotent_AndReusesCanonicalProductId`).
- **Frontend Production Build** (`npm run build` in `src/frontend`): `tsc && vite build` succeeded cleanly (`0` errors).

---

## 4. Safe Database Cleanup Preview (Not Executed)

Read-only audit of Supabase tables before cleanup:
- `products`: `2,069` rows
- `store_products`: `698` rows (`keells: 328`, `cargills: 280`, `arpico: 90`)
- `orphan products` (0 references in `store_products`): **`1,376` rows**
- `valid active canonical products`: **`693` rows**

[003_cleanup_orphan_products.sql](file:///c:/Users/sheha/Desktop/Keels/database/migrations/003_cleanup_orphan_products.sql) creates `_backup_products_20261125` and `_backup_product_matches_20261125`, previews exact deletion counts, and deletes only `products` rows where `NOT EXISTS (SELECT 1 FROM store_products sp WHERE sp.product_id = p.id)`, preserving 100% of `store_products` and `price_observations`. **Awaiting your explicit approval before executing.**

---

## 5. Real Before / After Database Counts (Idempotency Proof)

Executed `--supermarket cargills --terms milk` **twice** live against Supabase:
- **Before Run 1:** `products = 2,069` | `store_products = 698` | `price_observations = 6,768`
- **After Run 1 (64 Cargills items):** `products = 2,069 (+0)` | `store_products = 698 (+0)` | `price_observations = 6,832 (+64)`
- **After Run 2 (64 Cargills items):** `products = 2,069 (+0)` | `store_products = 698 (+0)` | `price_observations = 6,896 (+64)`

Followed by live **Arpico** (`15` verified observations) and **Keells** (`60` verified observations) runs against Supabase—all completing with exit code `0`.

---

## 6. Cargills `Inventory` Field Verification

- `POST https://cargillsonline.com/Web/GetSearchBarProductsV1` (`Search=milk`) returned `Milkmaid Condensed Milk` `510.00 g` with `Inventory: "990.00"` and `390.00 g` with `Inventory: "790.00"`.
- `ProductDetails` HTML is client-rendered via AngularJS; the `Inventory` field in `GetSearchBarProductsV1` represents the LKR shelf price, not stock quantity (which is why `AvailabilityStatus` was updated from `"in_stock"` to `"unknown"`).

---

## 7. Verified Cross-Store Product Matching Candidates

Evaluating the `698` live `store_products` with our improved brand + abbreviation + unit normalization identified **13 high-confidence cross-store candidates (`>= 0.70`)**, including 3-way matches:
1. **`Kandos Chocolate Biscuit Pudding 400g` (`sim = 1.0` across all 3 stores):**
   - Cargills (`Kandos Chocolate Biscuit Pudding`), Arpico (`Kandos Biscuit Pudding Chocolate 400G`), Keells (`Kandos Chocolate Biscuit Pudding 400g`)
2. **`Maliban Less Sugar Lemon Puff 100g` (`sim = 1.0 / 0.80` across all 3 stores)**
3. **`Maliban Less Sugar Chocolate Cream Biscuit 100g` (`sim = 1.0 / 0.80` across all 3 stores)**
4. **`Anchor Non-Fat Milk Powder 400g` (`sim = 1.0` Keells <-> Arpico)**
5. **`Anchor Full Cream Milk Powder 400g` (`sim = 1.0` Keells <-> Cargills)**

---

## 8. Remaining Blockers & Exact Actions Required

1. **Approve `003_cleanup_orphan_products.sql`:** Confirm if you want me to run the cleanup SQL in Supabase (or run it yourself in Supabase SQL Editor) to remove the `1,376` historical orphan rows.
2. **Rotate Supabase Secret Key (Recommended):** Rotate your `sb_secret_` key in Supabase Dashboard and update `SUPABASE_SERVICE_ROLE_KEY` in GitHub Secrets + local `.env`.
3. **Approve `git push`:** Confirm if you want all verified collector, matcher, security, and frontend fixes pushed to `origin/master`.
