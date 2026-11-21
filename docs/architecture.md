# PriceWise LK — System Architecture & Design Specification

## 1. System Overview

PriceWise LK is a real-world supermarket price comparison engine for Sri Lanka. The system is designed for high data integrity, auditable collection pipelines, reliable cross-store product matching, and zero tolerance for fabricated data.

```mermaid
flowchart TD
    subgraph DataSources["Live Sri Lankan Supermarkets"]
        Cargills["Cargills Food City (Public JSON API)"]
        Arpico["Arpico Supercentre (OpenCart HTML)"]
        Keells["Keells Super (Blocked: Auth/Encryption)"]
    end

    subgraph Collection["Data Collector (.NET 8/10 CLI)"]
        Engine["Collector Orchestrator"]
        CargillsAdapter["CargillsCollector"]
        ArpicoAdapter["ArpicoCollector"]
        KeellsAdapter["KeellsCollector (Blocked Reporter)"]
        Validator["Price & Data Validator"]
        Matcher["Product Matching Engine"]
    end

    subgraph Storage["Database & Persistence (Supabase / PostgreSQL)"]
        Schema["Normalized Relational Schema\n(supermarkets, products, store_products, price_observations, collector_runs, product_matches)"]
        RLS["Row Level Security (RLS)\nPublic Read-Only / Service Role Write"]
    end

    subgraph Automation["Scheduled Automation"]
        GHA["GitHub Actions Cron (Every 15 mins / Manual Dispatch)"]
    end

    subgraph Frontend["User Interface (React + TypeScript + Vite + Tailwind)"]
        Home["Home Page (Search, Freshness, Supermarkets)"]
        Search["Search & Filters"]
        Comparison["Side-by-Side Supermarket Comparison"]
        Details["Product Details & Price History"]
        Status["Collector & Integration Status"]
    end

    GHA -->|Executes on Schedule| Engine
    Engine --> CargillsAdapter & ArpicoAdapter & KeellsAdapter
    CargillsAdapter -->|HTTP POST| Cargills
    ArpicoAdapter -->|HTTP GET| Arpico
    KeellsAdapter -.->|Logged as Blocked| Keells

    CargillsAdapter & ArpicoAdapter --> Validator
    Validator --> Matcher
    Matcher -->|Privileged Writes (Service Key)| Schema

    Frontend -->|Public Read-Only API (Anon Key)| Storage
```

---

## 2. Core Entities & Relational Schema

### 2.1 Supermarkets (`supermarkets`)
Tracks supermarket chains and their integration status.
- `id` (UUID, PK)
- `name` (TEXT, NOT NULL)
- `slug` (TEXT, UNIQUE, NOT NULL)
- `website_url` (TEXT, NOT NULL)
- `integration_status` (TEXT: `active`, `blocked`, `maintenance`)
- `status_reason` (TEXT, NULLABLE)
- `created_at` (TIMESTAMPTZ, DEFAULT NOW())

### 2.2 Store Locations (`store_locations`)
Real, confirmed physical or delivery hub locations.
- `id` (UUID, PK)
- `supermarket_id` (UUID, FK -> supermarkets.id)
- `location_name` (TEXT, NOT NULL)
- `location_type` (TEXT: `physical_store`, `delivery_hub`, `online_fulfillment`)
- `delivery_area` (TEXT, NOT NULL)
- `external_store_id` (TEXT, NULLABLE)
- `created_at` (TIMESTAMPTZ, DEFAULT NOW())

### 2.3 Canonical Products (`products`)
Unified, deduplicated catalog representation of a supermarket item.
- `id` (UUID, PK)
- `canonical_name` (TEXT, NOT NULL)
- `brand` (TEXT, NOT NULL)
- `barcode` (TEXT, NULLABLE, UNIQUE WHERE NOT NULL)
- `quantity` (NUMERIC(10,2), NOT NULL)
- `unit` (TEXT, NOT NULL: `g`, `kg`, `ml`, `l`, `pcs`, `packs`)
- `category` (TEXT, NOT NULL)
- `created_at` (TIMESTAMPTZ, DEFAULT NOW())

### 2.4 Store Products (`store_products`)
A specific supermarket's representation and listing of a product.
- `id` (UUID, PK)
- `supermarket_id` (UUID, FK -> supermarkets.id)
- `store_location_id` (UUID, FK -> store_locations.id)
- `product_id` (UUID, FK -> products.id, NULLABLE)
- `source_product_id` (TEXT, NOT NULL)
- `source_product_name` (TEXT, NOT NULL)
- `product_url` (TEXT, NOT NULL)
- `image_url` (TEXT, NULLABLE)
- `availability_status` (TEXT: `in_stock`, `out_of_stock`, `unknown`)
- `last_observed_at` (TIMESTAMPTZ, NULLABLE)
- `created_at` (TIMESTAMPTZ, DEFAULT NOW())
- UNIQUE(`supermarket_id`, `source_product_id`)

### 2.5 Price Observations (`price_observations`)
Point-in-time recorded prices. Exact decimal precision (`NUMERIC(12,2)`).
- `id` (UUID, PK)
- `store_product_id` (UUID, FK -> store_products.id)
- `price` (NUMERIC(12,2), NOT NULL)
- `original_price` (NUMERIC(12,2), NULLABLE)
- `currency` (VARCHAR(3), NOT NULL DEFAULT 'LKR')
- `observed_at` (TIMESTAMPTZ, NOT NULL)
- `source_url` (TEXT, NOT NULL)
- `created_at` (TIMESTAMPTZ, DEFAULT NOW())

### 2.6 Collector Runs (`collector_runs`)
Auditable execution logs for each adapter run.
- `id` (UUID, PK)
- `supermarket_id` (UUID, FK -> supermarkets.id)
- `started_at` (TIMESTAMPTZ, NOT NULL)
- `finished_at` (TIMESTAMPTZ, NULLABLE)
- `status` (TEXT: `running`, `success`, `failed`, `blocked`)
- `products_checked` (INT, DEFAULT 0)
- `products_updated` (INT, DEFAULT 0)
- `error_message` (TEXT, NULLABLE)

### 2.7 Product Matches (`product_matches`)
Explicit audit log of cross-store product pairings.
- `id` (UUID, PK)
- `product_id` (UUID, FK -> products.id)
- `store_product_id` (UUID, FK -> store_products.id)
- `match_method` (TEXT: `barcode_exact`, `sku_exact`, `brand_name_unit_exact`, `high_confidence_heuristic`)
- `confidence` (NUMERIC(4,3) BETWEEN 0 AND 1)
- `verification_status` (TEXT: `verified`, `pending_review`, `rejected`)
- `created_at` (TIMESTAMPTZ, DEFAULT NOW())
- UNIQUE(`product_id`, `store_product_id`)

---

## 3. Product Matching Engine

The matching engine enforces strict rules to prevent false product comparisons:

1. **Tier 1: Barcode / GTIN Match (Confidence: 1.0)**
   - When both store items have verified EAN-13 / UPC / GTIN barcodes that match exactly.
2. **Tier 2: Brand + Normalized Title + Exact Quantity & Unit (Confidence: 0.95)**
   - Normalized text removes marketing adjectives (e.g. "Special Promo", "New Look").
   - Brand names match canonical brand dictionary (e.g., "Ambewela", "Dilmah", "Munchee", "Milkmaid").
   - Unit numbers and scales match exactly (e.g., `510g == 510g`, `1kg == 1000g`).
3. **Tier 3: Heuristic Token Jaccard + Levenshtein with Exact Unit Match (Confidence: 0.85 - 0.90)**
   - If unit and quantity match exactly, and title token overlap > 80%, flag as `pending_review` or `verified` if confidence > 0.88.
4. **Different Quantities / Variants**:
   - `500g` and `1kg` of the same brand are NEVER matched as identical products.
   - Normalized unit prices (`Rs / 100g` or `Rs / 1L`) are computed purely for informational display, never confusing the user that the packs are identical.

---

## 4. Security & Access Boundaries

- **Database RLS Policies**:
  - `anon` role (frontend): `SELECT` permissions ONLY on `supermarkets`, `store_locations`, `products`, `store_products`, `price_observations`, `product_matches`, and `collector_runs`.
  - `service_role` (collector process): Full `INSERT`, `UPDATE` permissions.
- **Collector Secrets**:
  - `SUPABASE_URL` and `SUPABASE_SERVICE_ROLE_KEY` stored exclusively in GitHub Actions Secrets or local `.env` (never checked into Git).
- **Public Frontend**:
  - Only needs `VITE_SUPABASE_URL` and `VITE_SUPABASE_ANON_KEY`.
  - Cannot alter data or trigger unauthorized runs.
