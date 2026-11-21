# PriceWise LK — Sri Lankan Supermarket Price Intelligence

> **Real-World Sri Lankan Supermarket Price Comparison Application**  
> Built with C# / .NET 8, React, TypeScript, Vite, Tailwind CSS, PostgreSQL / Supabase, and GitHub Actions.

---

## 1. Absolute Requirement: Real Data Only

PriceWise LK adheres to a strict data integrity policy:
- **No fake products, simulated prices, or fictional records.**
- **No mock API responses presented as real data.**
- **No randomly generated discounts, ratings, images, or price histories.**
- **All supermarket prices are verified directly from authorized public endpoints.**
- If a supermarket cannot be accessed using authorized methods (such as Keells requiring encrypted sessions and anti-bot challenge cookies), it is marked as **Blocked** with the exact blocker disclosed. No fake data is used as a fallback.

---

## 2. Live Supermarket Integrations

| Supermarket | Platform | Integration Status | Permitted Method | Verified Data Format |
| :--- | :--- | :--- | :--- | :--- |
| **Cargills Food City** | `cargillsonline.com/Web/` | **Active & Verified** | Public ASP.NET POST search endpoint (`/Web/GetSearchBarProductsV1`) | Clean JSON: SKU, Item Name, Category, Unit, Unit Size, Exact LKR Price, Image URL, EnId. |
| **Arpico Supercentre** | `myarpico.com/shop/` | **Active & Verified** | Public OpenCart Search & Catalog (`index.php?route=product/search`) | HTML (OpenCart): Product Thumbnails, Current Price, Original Price, Image URL, Product URL. |
| **Keells Super** | `keellssuper.com` | **Blocked (Auth & Session Encryption Required)** | None publicly permitted without session encryption | Backend APIs (`zebraliveback.keellssuper.com`) require custom encrypted `usersessionid` headers and Cloudflare challenge tokens. Direct unauthenticated requests return `HTTP 401 Unauthorized`. Automated bypass is strictly prohibited. |

For detailed network analysis, headers, and robots.txt reviews, see [docs/source-audit.md](file:///docs/source-audit.md).

---

## 3. Technology Stack

- **Frontend**: React 18, TypeScript, Vite, Tailwind CSS, Lucide Icons.
- **Data Collection**: C# / .NET 8 CLI (`PriceWise.Collector`), `HttpClient`, `HtmlAgilityPack`, Structured Console Logging, Microsoft Dependency Injection.
- **Database & Storage**: PostgreSQL hosted by Supabase, SQL Migrations, Row Level Security (RLS) policies (read-only public `anon` access, privileged `service_role` writes).
- **Automation**: GitHub Actions scheduled workflows (15-minute cron interval, per-source scheduling, manual diagnostic triggers).
- **Hosting**: Cloudflare Pages (Frontend) + Supabase (Database) + GitHub Actions (Collector).

---

## 4. Project Structure

```
PriceWise-LK/
├── .agents/
│   └── rules/
│       └── project-rules.md         # Permanent always-on agent engineering rules
├── docs/
│   ├── source-audit.md              # Live feasibility audit of Sri Lankan supermarkets
│   └── architecture.md             # System architecture & matching engine design
├── database/
│   ├── migrations/
│   │   ├── 001_initial_schema.sql   # Relational schema (7 tables, exact money types)
│   │   └── 002_row_level_security.sql # RLS policies: read-only public, write service_role
│   └── seed_initial_metadata.sql    # Verified supermarkets and store locations ONLY
├── src/
│   ├── collector/                   # .NET 8 C# Data Collection Engine
│   │   ├── PriceWise.Collector/
│   │   │   ├── Adapters/            # Supermarket adapters (Cargills, Arpico, Keells)
│   │   │   ├── Matching/            # Cross-store canonical product matching engine
│   │   │   ├── Models/              # Relational models & raw scraped DTOs
│   │   │   ├── Storage/             # Supabase & Local JSON persistence services
│   │   │   ├── Validation/          # Price, quantity, URL & hygiene validation
│   │   │   └── Program.cs           # CLI runner with structured logging
│   │   └── PriceWise.Collector.Tests/
│   │       ├── MatchingTests.cs     # Barcode, quantity, and unit normalization tests
│   │       └── ValidationTests.cs   # Price safeguard limits and regex unit parsing tests
│   └── frontend/                    # React + Vite + Tailwind Web Application
│       ├── public/data/             # Verified live catalog JSON snapshots
│       ├── src/
│       │   ├── components/          # Navbar, Footer, StatusBadge, ProductCard, EmptyState
│       │   ├── lib/supabase.ts      # Read-only Supabase & offline-resilient data loader
│       │   ├── pages/               # Home, Search, Comparison, Details, Status
│       │   └── types/               # TypeScript interfaces
│       └── package.json
├── .github/
│   └── workflows/
│       ├── collect-prices.yml       # 15-minute scheduled collector workflow
│       └── deploy-frontend.yml      # Cloudflare Pages deployment workflow
├── .env.example                     # Environment variables template
└── README.md
```

---

## 5. Getting Started Locally

### Prerequisites
- **.NET SDK 8.0+**
- **Node.js 20+** & **npm**
- (Optional) **Supabase Project** (a local JSON fallback is provided automatically for offline testing)

### Step 1: Clone and Configure Environment
```bash
cp .env.example .env
```
Populate `.env` with your Supabase credentials if using a cloud database:
```env
VITE_SUPABASE_URL=https://your-project.supabase.co
VITE_SUPABASE_ANON_KEY=eyJhbGciOi...
SUPABASE_URL=https://your-project.supabase.co
SUPABASE_SERVICE_ROLE_KEY=eyJhbGciOi...
```

### Step 2: Database Setup (PostgreSQL / Supabase)
Execute the migrations in order:
1. `database/migrations/001_initial_schema.sql`
2. `database/migrations/002_row_level_security.sql`
3. `database/seed_initial_metadata.sql`

### Step 3: Run Collector Unit Tests
```bash
dotnet test src/collector/PriceWise.sln
```

### Step 4: Run Live Price Collection
Execute the collector to fetch real, point-in-time shelf prices from Cargills and Arpico:
```bash
dotnet run --project src/collector/PriceWise.Collector -- --supermarket all --terms "milk,tea,rice"
```
The collector will:
1. Query Cargills Food City live API (e.g. 180+ real products).
2. Query Arpico Supercentre live OpenCart search (e.g. 45+ real products).
3. Audit Keells Super endpoint and record status `blocked` without fictional data.
4. Normalize and cross-match items by brand, title tokens, quantity, and units.
5. Export verified data to `src/frontend/public/data/verified_catalog.json` and Supabase.

### Step 5: Start the Frontend Application
```bash
cd src/frontend
npm install
npm run dev
```
Open [http://localhost:3000](http://localhost:3000) to view the live responsive comparison web app!

---

## 6. Frontend Features

- **Home Page**: Hero search, supermarket status summary, verified recently observed deals, and data freshness badge.
- **Search Page**: Search actual indexed products with filters by supermarket, category, and sorting by lowest price, highest price, or biggest savings.
- **Comparison Page**: Side-by-side supermarket offer comparison highlighting the cheapest verified offer in green, calculating exact savings in LKR, and linking directly to store pages.
- **Product Details**: Complete canonical identity, verified store offerings, price history observation logs, and matching verification method.
- **Status & Audits**: Live integration health for Cargills, Arpico, and Keells, last observation timestamps, error logs, and honest architectural blockers.

---

## 7. Security & Compliance

- **No Secrets in Bundles**: Frontend strictly uses `VITE_SUPABASE_ANON_KEY` restricted by PostgreSQL Row Level Security (RLS) policies to `SELECT` operations only.
- **Service Role Lockdown**: `SUPABASE_SERVICE_ROLE_KEY` is exclusively used by trusted GitHub Actions runners or local collector processes for database writes.
- **Zero Bypass Policy**: No anti-bot bypasses, CAPTCHA workarounds, or undocumented private API hacks.
