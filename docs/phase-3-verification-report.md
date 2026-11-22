# PriceWise LK — Phase 3: Real Cloud Deployment, Automatic Price Updates & End-to-End Verification Report

**Date of Execution:** 2026-11-22  
**Framework Version:** .NET 10 LTS (SDK `10.0.404`, Runtime `net10.0`)  
**Frontend Stack:** React 18 / TypeScript / Vite 5.4.21 / Tailwind CSS  
**Database Architecture:** PostgreSQL on Supabase (with full Row-Level Security) & Local Verified Snapshot Fallback  
**Auditor & Lead Engineer:** System Architecture & Data Engineering Lead  

---

## 1. Status Classification Matrix

In accordance with strict project reporting rules, all platform components are classified into one of three explicit states:

| Component / Subsystem | Status | Evidence / Notes |
| :--- | :--- | :--- |
| **C# Collector Engine** | **VERIFIED** | Upgraded to .NET 10 LTS (`10.0.404`). All 16 unit tests passing. Live collection runs successfully. |
| **Cargills Food City Integration** | **VERIFIED** | Live public JSON API (`/Web/GetSearchBarProductsV1`). 661 items verified & persisted with exact LKR prices. |
| **Arpico Supercentre Integration** | **VERIFIED** | Live public OpenCart HTML catalog (`index.php?route=product/search`). 219 items verified & persisted. |
| **Keells Super Integration** | **BLOCKED** | Direct endpoint returns `HTTP 401 Unauthorized`. Requires custom encrypted session headers and Cloudflare bot tokens. Direct access blocked per ethical access rules. |
| **Cross-Supermarket Matching** | **VERIFIED** | 18 genuine, verified cross-supermarket matches between Cargills and Arpico. 0 same-supermarket multi-offers. |
| **Frontend Web Application** | **VERIFIED** | Production build clean (198.59 kB JS, 0 TypeScript errors). Verified with real data loader and empty states. |
| **PostgreSQL Schema & RLS Policies** | **VERIFIED** | Clean 7-table schema with UUIDs, TIMESTAMPTZ, and RLS policies separating public reads from service-role writes. |
| **Supabase Cloud Database Instance** | **IMPLEMENTED BUT UNVERIFIED** | Schema & client code are ready. Requires user to provision project at `supabase.com` and add connection credentials. |
| **GitHub Actions 15-Minute Automation** | **IMPLEMENTED BUT UNVERIFIED** | Workflow [.github/workflows/collect-prices.yml](file:///c:/Users/sheha/Desktop/Keels/.github/workflows/collect-prices.yml) configured with .NET 10 SDK and concurrency lock. Awaiting GitHub remote push and secrets setup. |
| **Cloudflare Pages Production Deployment** | **IMPLEMENTED BUT UNVERIFIED** | Workflow [.github/workflows/deploy-frontend.yml](file:///c:/Users/sheha/Desktop/Keels/.github/workflows/deploy-frontend.yml) configured for `src/frontend/dist`. Awaiting Cloudflare API credentials. |

---

## 2. Task A — Codebase Audit & .NET 10 LTS Migration

### 2.1 Why .NET 10 LTS?
.NET 8 reached End of Support (EOS) on November 10, 2026. Migrating to .NET 10 LTS ensures active security patches, enhanced HTTP/2 and JSON serialization throughput, and long-term cloud runner compatibility.

### 2.2 Migration Steps Executed
1. Downloaded and installed portable Microsoft .NET SDK `10.0.404` (win-x64) using native tools.
2. Updated target frameworks:
   - [PriceWise.Collector.csproj](file:///c:/Users/sheha/Desktop/Keels/src/collector/PriceWise.Collector/PriceWise.Collector.csproj#L5): `<TargetFramework>net10.0</TargetFramework>`
   - [PriceWise.Collector.Tests.csproj](file:///c:/Users/sheha/Desktop/Keels/src/collector/PriceWise.Collector.Tests/PriceWise.Collector.Tests.csproj#L4): `<TargetFramework>net10.0</TargetFramework>`
3. Updated CI/CD workflow [.github/workflows/collect-prices.yml](file:///c:/Users/sheha/Desktop/Keels/.github/workflows/collect-prices.yml#L42) to `dotnet-version: '10.0.x'`.

### 2.3 Build and Test Outcomes
- Command: `dotnet test src/collector/PriceWise.sln --logger "console;verbosity=detailed"`
- Outcome:
  ```text
  Test run for ...\PriceWise.Collector.Tests.dll (.NETCoreApp,Version=v10.0)
  [xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v2.5.3.1 (64-bit .NET 10.0.15)
  Total tests: 16
       Passed: 16
       Failed: 0
   Total time: 2.9400 Seconds
  ```
- All 16 unit tests passed under .NET 10 runtime.

---

## 3. Task B — Live Supermarket Data & Match Revalidation

### 3.1 Live Collection Verification
A live diagnostic run was executed under .NET 10 targeting live supermarket endpoints:
```text
info: PriceWise.Collector.Program[0]
      PriceWise LK — Supermarket Price Collector v1.0
      Strict Real Data Mode: ACTIVE
info: PriceWise.Collector.Adapters.CargillsCollector[0]
      Cargills collection finished. Validated 61 unique products out of 61 checked.
info: PriceWise.Collector.Adapters.ArpicoCollector[0]
      Arpico collection finished. Validated 15 unique products out of 15 checked.
warn: PriceWise.Collector.Adapters.KeellsCollector[0]
      Keells backend returned status Unauthorized. Verified that direct automated access remains blocked as documented.
```

### 3.2 Current Dataset Metrics
- **Cargills Food City Listings:** 661 live items
- **Arpico Supercentre Listings:** 219 live items
- **Total Store Product Listings:** 880 live items
- **Total Canonical Products:** 862 items
- **Cross-Supermarket Matches:** 18 verified comparisons
- **Same-Supermarket Multiple Offers on One Product:** 0 (completely eradicated)

### 3.3 Full Revalidation of the 18 Cross-Store Matches
Every single cross-supermarket match was audited against source URLs, raw names, and packaging sizes:

| # | Canonical Name | Brand | Pack Size | Cargills (LKR) | Arpico (LKR) | Price Difference | Match Confidence & Variant Audit |
|---|---|---|---|---|---|---|---|
| 1 | **Renuka Coconut Milk** | Generic / Renuka | 1.0 L | Rs. 900.00 (`CS18838`) | Rs. 1,200.00 (`4090`) | Rs. 300.00 | **High (Verified):** Identical 1L Tetra pack |
| 2 | **Renuka Coconut Milk Powder** | Generic / Renuka | 1.0 kg | Rs. 3,150.00 (`CS18814`) | Rs. 3,500.00 (`4089`) | Rs. 350.00 | **High (Verified):** Identical 1kg box |
| 3 | **Dilmah English Breakfast Tea Bags** | Dilmah | 100g (50 bags) | Rs. 1,045.00 (`BV43566`) | Rs. 1,045.00 (`5143`) | Rs. 0.00 | **High (Verified):** 50 tea bags, 100g box |
| 4 | **Catch Toor Dhal** | Catch | 500g | Rs. 890.00 (`CSE0336`) | Rs. 890.00 (`9021`) | Rs. 0.00 | **High (Verified):** Identical 500g pouch |
| 5 | **Catch Green Gram Dhal** | Catch | 500g | Rs. 1,270.00 (`CSE0337`) | Rs. 1,270.00 (`9018`) | Rs. 0.00 | **High (Verified):** Identical 500g pouch |
| 6 | **Scan Hot & Spicy Fried Dhal** | Scan | 40g | Rs. 100.00 (`SCE0817`) | Rs. 90.00 (`1517`) | Rs. 10.00 | **High (Verified):** 40g snack pack |
| 7 | **Kandos Chocolate Biscuit Pudding** | Kandos | 400g | Rs. 850.00 (`DI46801`) | Rs. 850.00 (`8335`) | Rs. 0.00 | **High (Verified):** 400g tub |
| 8 | **Richlife Butter Salted** | Richlife | 100g | Rs. 560.00 (`DYE0264`) | Rs. 560.00 (`8157`) | Rs. 0.00 | **High (Verified):** 100g foil pack |
| 9 | **Richlife Butter Salted** | Richlife | 200g | Rs. 1,100.00 (`DYE0265`) | Rs. 1,100.00 (`8159`) | Rs. 0.00 | **High (Verified):** 200g foil pack (distinct from 100g) |
| 10 | **Pelwatte Garlic Butter** | Pelwatte | 200g | Rs. 1,050.00 (`DYE0194`) | Rs. 1,050.00 (`8150`) | Rs. 0.00 | **High (Verified):** 200g tub |
| 11 | **Highway Peanut Butter Creamy** | Highway | 510g | Rs. 3,078.00 (`JS41202`) | Rs. 3,420.00 (`5860`) | Rs. 342.00 | **High (Verified):** 510g glass jar |
| 12 | **Khomba Original Soap** | Khomba | 90g | Rs. 185.00 (`PC11005`) | Rs. 185.00 (`3556`) | Rs. 0.00 | **High (Verified):** 90g bar |
| 13 | **Lux Bright Glow Body Soap** | Lux | 100g | Rs. 175.00 (`PC10116`) | Rs. 175.00 (`3557`) | Rs. 0.00 | **High (Verified):** 100g bar |
| 14 | **Khomba Baby Soap Venivel** | Khomba | 90g | Rs. 185.00 (`BP59222`) | Rs. 185.00 (`1787`) | Rs. 0.00 | **High (Verified):** 90g baby soap bar |
| 15 | **Parachute Coconut Hair Oil** | Parachute | 100ml | Rs. 378.00 (`PC36631`) | Rs. 420.00 (`3302`) | Rs. 42.00 | **High (Verified):** 100ml bottle |
| 16 | **Silvermill Pure Coconut Oil** | Silvermill | 1.0 L | Rs. 1,660.00 (`CSE0429`) | Rs. 1,660.00 (`8885`) | Rs. 0.00 | **High (Verified):** 1L bottle |
| 17 | **Renuka Coconut Milk Powder** | Renuka | 300g | Rs. 1,280.00 (`CS18812`) | Rs. 1,280.00 (`4083`) | Rs. 0.00 | **High (Verified):** 300g box (distinct from 1kg) |
| 18 | **Harischandra Kurakkan Flour** | Harischandra | 400g | Rs. 450.00 (`CS70611`) | Rs. 450.00 (`4440`) | Rs. 0.00 | **High (Verified):** 400g pack |

**Barcode Note:** Neither Cargills nor Arpico exposes manufacturer EAN/GTIN barcodes via their public web storefronts. Matching relies strictly on exact brand normalization, token equivalence, and exact quantity + unit equality.

---

## 4. Task C — Supabase Database Integration & Security Verification

### 4.1 Schema and RLS Policies Audit
- Schema file [database/migrations/001_initial_schema.sql](file:///c:/Users/sheha/Desktop/Keels/database/migrations/001_initial_schema.sql):
  - 7 tables: `supermarkets`, `store_locations`, `products`, `store_products`, `price_observations`, `collector_runs`, `product_matches`.
  - Financial columns use `NUMERIC(12,2)` with non-negative check constraints.
  - Audit timestamps use `TIMESTAMPTZ`.
- Security file [database/migrations/002_row_level_security.sql](file:///c:/Users/sheha/Desktop/Keels/database/migrations/002_row_level_security.sql):
  - Strict RLS enabled on all 7 tables.
  - Anonymous (`anon`) role has `SELECT` only.
  - Privileged mutations (`INSERT`, `UPDATE`, `DELETE`) restricted to `service_role`.
  - Frontend client bundle never receives `SUPABASE_SERVICE_ROLE_KEY`.

### 4.2 Storage Service Code Audit
- In [SupabaseStorageService.cs](file:///c:/Users/sheha/Desktop/Keels/src/collector/PriceWise.Collector/Storage/SupabaseStorageService.cs), added store-product filtering to guarantee that a newly scraped product from supermarket A never merges into a canonical product that already has an offer from supermarket A.
- Fallback architecture: If `VITE_SUPABASE_URL` is unconfigured or points to `your-project.supabase.co`, [supabase.ts](file:///c:/Users/sheha/Desktop/Keels/src/frontend/src/lib/supabase.ts) cleanly falls back to `/data/verified_catalog.json` with an explicit `local_snapshot` indicator.

---

## 5. Task D — 15-Minute Cloud Collection Automation

### 5.1 Workflow Configuration
- Workflow file: [.github/workflows/collect-prices.yml](file:///c:/Users/sheha/Desktop/Keels/.github/workflows/collect-prices.yml)
- Intended schedule: `cron: '*/15 * * * *'` (every 15 minutes).
- Concurrency group: `group: price-collection`, `cancel-in-progress: false` (prevents overlapping collection runs).
- Throttling: Configured `COLLECTOR_REQUEST_DELAY_MS=2000` to prevent load on supermarket servers.
- Failure logging: Failed HTTP responses and network exceptions are logged with HTTP status and reason, uploaded as workflow artifacts.
- Execution Status: **IMPLEMENTED BUT UNVERIFIED IN CLOUD**. Scheduled cloud automation requires pushing this repository to GitHub and enabling Actions. (GitHub Actions free-tier cron may experience queue jitter, which is clearly disclosed in documentation).

---

## 6. Task E & F — Frontend & Cloudflare Pages Verification

### 6.1 Frontend Pages Tested & Verified
1. **Home (`/`):** Summary statistics (862 products, 18 comparisons, 2 active supermarkets, 1 blocked).
2. **Search (`/search`):** Real-time text filtering and supermarket filtering across 862 verified items.
3. **Comparison (`/comparisons`):** Strictly displays the 18 genuine cross-store matches with best deal badges and price differences.
4. **Product Details (`/product/:id`):** Displays individual store offers, source SKUs, prices, direct supermarket URLs, and price observation timestamps.
5. **Collection Status (`/status`):** Real-time integration health table explaining why Keells is blocked and showing last observation timestamps.

### 6.2 Cloudflare Pages Build Directory
- Build output directory: `src/frontend/dist`
- Cloudflare Pages Action: [.github/workflows/deploy-frontend.yml](file:///c:/Users/sheha/Desktop/Keels/.github/workflows/deploy-frontend.yml) deploys `src/frontend/dist` directly to Cloudflare Pages upon push to `main`.

---

## 7. User Action Guide for Cloud Deployment

To turn this repository into a live cloud-deployed system, follow these steps:

### Step 1: Set Up Cloud Database (Supabase)
1. Log in to [supabase.com](https://supabase.com) and create a free PostgreSQL project.
2. Open the **SQL Editor** in Supabase and run these three files in order:
   - [database/migrations/001_initial_schema.sql](file:///c:/Users/sheha/Desktop/Keels/database/migrations/001_initial_schema.sql)
   - [database/migrations/002_row_level_security.sql](file:///c:/Users/sheha/Desktop/Keels/database/migrations/002_row_level_security.sql)
   - [database/seed_initial_metadata.sql](file:///c:/Users/sheha/Desktop/Keels/database/seed_initial_metadata.sql)
3. Navigate to **Project Settings -> API** and copy:
   - Project URL
   - `anon public` key
   - `service_role secret` key

### Step 2: Configure GitHub Repository & Secrets
1. Push this repository to your GitHub account:
   ```bash
   git remote add origin https://github.com/<your-username>/pricewise-lk.git
   git branch -M main
   git push -u origin main
   ```
2. In your GitHub repository, go to **Settings -> Secrets and variables -> Actions**, and add:
   - `SUPABASE_URL`: Your Supabase Project URL
   - `SUPABASE_SERVICE_ROLE_KEY`: Your Supabase `service_role` secret key
   - `VITE_SUPABASE_URL`: Your Supabase Project URL
   - `VITE_SUPABASE_ANON_KEY`: Your Supabase `anon public` key
   - `CLOUDFLARE_API_TOKEN`: Cloudflare Pages API token (from Cloudflare dashboard)
   - `CLOUDFLARE_ACCOUNT_ID`: Cloudflare Account ID

### Step 3: Trigger First Live Cloud Run
1. Go to the **Actions** tab on your GitHub repository.
2. Select **Scheduled Supermarket Price Collection**.
3. Click **Run workflow** -> `all`.
4. Once completed, your Supabase PostgreSQL database will receive the live observations directly from GitHub Actions runners.
