# PriceWise LK — Keells API Integration & Audit Report

**Date:** 2026-11-25  
**Endpoints Verified:**
- `POST https://zebraliveback.keellssuper.com/1.0/Login/GuestLogin` (Public Guest Session Initialization)
- `GET https://zebraliveback.keellssuper.com/2.0/Web/GetItemDetails` (Public Guest Catalog & Search)  
**Integration Status:** `active` (Public Guest Browsing Flow Verified Live)

---

## 1. Actual API Behavior and Supported Fields

Investigating Keells' public storefront SPA (`https://www.keellssuper.com/`) revealed that Keells provides an explicit **"Continue As Guest"** flow (`Just want to browse the store?`) for unauthenticated visitors without requiring an account login or private API key:

1. **Guest Session Initialization:**  
   `POST https://zebraliveback.keellssuper.com/1.0/Login/GuestLogin` with empty JSON `{}` returns `HTTP 200 OK`, standard HTTP session cookies (`ARRAffinity`, `TiPMix`, `__cf_bm`), `preferredOutlet` (`SCDR` — Darley Road), and a public guest `userSessionID` (`isGuest: true`, `email: "guest@keellsonline.com"`).
2. **Product & Price Retrieval:**  
   `GET https://zebraliveback.keellssuper.com/2.0/Web/GetItemDetails` with the guest `CookieContainer` and `usersessionid` header returns `HTTP 200 OK` with JSON wrapped in `{ statusCode: 200, result: { itemDetailsList: [...], promotionItemDetailsList: [...] } }`.

Supported query parameters verified live:
- `fromCount` — 0-based row offset (`pageSize * page`)
- `toCount` — page size / max rows per request (e.g., `20`)
- `outletCode` — branch/fulfillment hub (`SCDR`)
- `isPromotionOnly` — `false` returns full catalog/search results; `true` filters to promotions only
- `stockStatus` — `true` restricts to in-stock items
- `itemPricefrom` / `itemPriceTo` — LKR price range filter
- `subDepartmentCode` — `ALL` or specific department code
- `itemDescription` — keyword search filter (e.g., `milk`)

Observed item fields: `itemID`, `itemCode`, `name`, `amount`, `imageUrl`, `stockInHand`, `isAvailable`, `uom`, `promotionDiscountValue`, `discountPercentage`, `isPromotionApplied`.

---

## 2. Permission Status

**Status: PERMITTED via Public Guest Browsing Flow (No Login / No Bypass).**

- Direct calls to `GetItemDetails` without a guest session return `HTTP 401 Unauthorized`.
- Calling Keells' own public `Login/GuestLogin` endpoint with standard `HttpClientHandler` cookies (`CookieContainer`) and passing the returned guest `userSessionID` header returns `HTTP 200 OK` with real product names, prices, images, and stock status — identical to what an ordinary guest visitor sees on `www.keellssuper.com`.
- No user credentials, private API keys, encrypted tokens, CAPTCHA solving, or Cloudflare bypasses are used.
- If `GuestLogin` or `GetItemDetails` ever returns `HTTP 401` or `403` (e.g., if a datacenter IP is blocked by Cloudflare), `KeellsCollector` immediately halts and reports `Status = "blocked"` without retrying or fabricating data.

---

## 3. Pagination and Filtering Findings

- **Offset + Page Size semantics:** `fromCount` is the 0-based offset and `toCount` is the requested page size (confirmed both in live responses and in the public SPA bundle: `fromCount=${t*e-t}&toCount=${t}`).
- **End-of-pagination detection:** Stops when `totalRawCount == 0`, when `itemDetailsList.Count < pageSize`, when `0` new unique items are discovered on a page, or when `MaxPagesPerTerm` (`3` pages = up to 60 items per search term) is reached.
- **Keyword search (`itemDescription`):** Verified live with `itemDescription=milk` and `isPromotionOnly=false`, allowing Keells to collect the exact same staple categories (`milk,tea,rice,dhal,sugar,biscuit`) as Cargills and Arpico for cross-supermarket comparison.

---

## 4. Implementation Changes

1. **Updated [KeellsCollector.cs](file:///c:/Users/sheha/Desktop/Keels/src/collector/PriceWise.Collector/Adapters/KeellsCollector.cs):**
   - Added `InitializeGuestSessionAsync` calling `POST /1.0/Login/GuestLogin` with standard cookie handling.
   - Updated `BuildGetItemDetailsUrl` to include `itemDescription` and `isPromotionOnly=false` by default.
   - Updated `ParseResponse` to deserialize both `{ statusCode, result: { itemDetailsList, promotionItemDetailsList } }` and unwrapped payloads.
   - Records `amount` as `Price` without inventing unverified discounted prices, sets `Barcode = null` (never assumes `itemCode` is a barcode), preserves outlet context (`SCDR:{itemID}`), deduplicates across pages/terms, and enforces `1500ms` delays + `MaxPagesPerTerm = 3`.
   - Preserves strict `HTTP 401`/`403` -> `Status = "blocked"` handling.
2. **Updated [Program.cs](file:///c:/Users/sheha/Desktop/Keels/src/collector/PriceWise.Collector/Program.cs):**
   - Configured `KeellsCollector` `HttpClient` with `HttpClientHandler { UseCookies = true, CookieContainer = new CookieContainer() }`.
3. **Updated [KeellsCollectorTests.cs](file:///c:/Users/sheha/Desktop/Keels/src/collector/PriceWise.Collector.Tests/KeellsCollectorTests.cs):**
   - Added 8 unit tests covering `GuestLogin` + `GetItemDetails`, wrapped `result` envelope parsing, deduplication, missing/zero prices, partial tail pagination, `HTTP 401`/`403` block handling on both endpoints, and `HTTP 500` bounded retries.
4. **Updated [ComparisonPage.tsx](file:///c:/Users/sheha/Desktop/Keels/src/frontend/src/pages/ComparisonPage.tsx):**
   - Dynamically renders verified Keells prices, original prices, cheapest badges, and store links alongside Cargills and Arpico whenever Keells offers are present.

---

## 5. Tests Actually Executed

- **`.NET 10 Test Suite (`dotnet test src/collector/PriceWise.sln --verbosity normal`):**
  - **Result:** `35 passed, 0 failed, 0 skipped` (27 existing + 8 `KeellsCollectorTests`).
- **Frontend Production Build (`npm run build` in `src/frontend`):**
  - **Result:** `tsc && vite build` succeeded with `0` errors.

---

## 6. Live Collection Evidence (Verified 2026-11-25)

Executed `dotnet run --project src/collector/PriceWise.Collector -- --supermarket keells --terms milk`:
- `POST https://zebraliveback.keellssuper.com/1.0/Login/GuestLogin` -> `HTTP 200 OK` (1151 ms), `Outlet: SCDR`
- `GET .../2.0/Web/GetItemDetails?fromCount=0&toCount=20&outletCode=SCDR&isPromotionOnly=false&stockStatus=true&itemPricefrom=0&itemPriceTo=50000&subDepartmentCode=ALL&itemDescription=milk` -> `HTTP 200 OK` (676 ms), `raw=22, newUnique=20`
- `fromCount=20` -> `HTTP 200 OK` (685 ms), `raw=22, newUnique=20`
- `fromCount=40` -> `HTTP 200 OK` (640 ms), `raw=23, newUnique=20`
- **Total:** `60 unique products validated out of 67 checked`, persisted cleanly with exit code `0`.
- Sample verified items:
  - `SCDR:67230` | `Ambewela Chocolate Flavoured Milk Uht Tetra 1L` | `Rs. 600.00` | `in_stock`
  - `SCDR:67229` | `Ambewela Flavoured Milk Uht Vanilla Tetra 1L` | `Rs. 600.00` | `in_stock`
  - `SCDR:75534` | `4Rever Body Milk Aloe Lavender 200ml` | `Rs. 750.00` | `in_stock`

---

## 7. Supabase Persistence & Unattended 15-Minute Schedule Assessment

- **Supabase compatibility:** Existing schema (`products`, `store_products`, `price_observations`, `collector_runs`, `product_matches`) supports Keells (`slug = 'keells'`) without schema changes. Note: the `supermarkets` row for `keells` in Supabase should have `integration_status` updated from `'blocked'` to `'active'` if you want the UI status badge to reflect active guest collection.
- **Unattended 15-minute updates (`collect-prices.yml`):**
  - **Supported in principle:** Each run initializes a fresh guest session and queries at most 3 pages per term with `1500ms` delays (~18 lightweight GET requests total).
  - **Cloud risk note:** Because `zebraliveback.keellssuper.com` uses Cloudflare (`__cf_bm`), GitHub Actions shared runner IPs may still occasionally encounter Cloudflare rate limits or `401/403` challenges. `KeellsCollector` handles this safely by halting with `Status = "blocked"` without failing Cargills/Arpico.

---

## 8. Remaining Blockers

- None for local/browser-equivalent guest collection (`HTTP 200 OK` verified live).
- GitHub Actions cloud runner IP acceptance by Cloudflare should be verified once pushed and run in GitHub Actions.

---

## 9. Exact Next Actions

1. **Approve git commit & push:** Confirm if you want these changes (along with the earlier `SupabaseStorageService.cs` fix) committed and pushed to `origin/master`.
2. **Verify in GitHub Actions & Supabase:** Run `Scheduled Supermarket Price Collection` (`--supermarket all`) after pushing to confirm Keells persists to Supabase from GitHub's Ubuntu runner and produces real cross-supermarket comparisons for the CEO demonstration.
