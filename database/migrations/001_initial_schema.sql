-- PriceWise LK: Core Relational Database Schema
-- Migration: 001_initial_schema.sql

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. Supermarkets Table
CREATE TABLE IF NOT EXISTS supermarkets (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(100) NOT NULL,
    slug VARCHAR(50) UNIQUE NOT NULL,
    website_url VARCHAR(255) NOT NULL,
    integration_status VARCHAR(50) NOT NULL DEFAULT 'active' CHECK (integration_status IN ('active', 'blocked', 'maintenance', 'pending')),
    status_reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 2. Store Locations Table (Physical or Delivery Hubs)
CREATE TABLE IF NOT EXISTS store_locations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    supermarket_id UUID NOT NULL REFERENCES supermarkets(id) ON DELETE CASCADE,
    location_name VARCHAR(150) NOT NULL,
    location_type VARCHAR(50) NOT NULL DEFAULT 'delivery_hub' CHECK (location_type IN ('physical_store', 'delivery_hub', 'online_fulfillment')),
    delivery_area VARCHAR(150) NOT NULL,
    external_store_id VARCHAR(100),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_supermarket_external_store UNIQUE (supermarket_id, external_store_id)
);

-- 3. Canonical Products Table
CREATE TABLE IF NOT EXISTS products (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    canonical_name VARCHAR(255) NOT NULL,
    brand VARCHAR(150) NOT NULL,
    barcode VARCHAR(100),
    quantity NUMERIC(10,2) NOT NULL,
    unit VARCHAR(20) NOT NULL CHECK (unit IN ('g', 'kg', 'ml', 'l', 'pcs', 'packs', 'm', 'cm')),
    category VARCHAR(100) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Index for barcode lookup
CREATE UNIQUE INDEX IF NOT EXISTS idx_products_barcode ON products (barcode) WHERE barcode IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_products_canonical_name ON products (canonical_name);
CREATE INDEX IF NOT EXISTS idx_products_brand ON products (brand);
CREATE INDEX IF NOT EXISTS idx_products_category ON products (category);

-- 4. Store Products Table (Per-Supermarket Listings)
CREATE TABLE IF NOT EXISTS store_products (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    supermarket_id UUID NOT NULL REFERENCES supermarkets(id) ON DELETE CASCADE,
    store_location_id UUID REFERENCES store_locations(id) ON DELETE SET NULL,
    product_id UUID REFERENCES products(id) ON DELETE SET NULL,
    source_product_id VARCHAR(150) NOT NULL,
    source_product_name VARCHAR(255) NOT NULL,
    product_url TEXT NOT NULL,
    image_url TEXT,
    availability_status VARCHAR(50) NOT NULL DEFAULT 'in_stock' CHECK (availability_status IN ('in_stock', 'out_of_stock', 'unknown')),
    last_observed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_store_product_source UNIQUE (supermarket_id, source_product_id)
);

CREATE INDEX IF NOT EXISTS idx_store_products_product_id ON store_products (product_id);
CREATE INDEX IF NOT EXISTS idx_store_products_supermarket ON store_products (supermarket_id);
CREATE INDEX IF NOT EXISTS idx_store_products_last_observed ON store_products (last_observed_at);

-- 5. Price Observations Table
CREATE TABLE IF NOT EXISTS price_observations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_product_id UUID NOT NULL REFERENCES store_products(id) ON DELETE CASCADE,
    price NUMERIC(12,2) NOT NULL CHECK (price >= 0),
    original_price NUMERIC(12,2) CHECK (original_price >= 0),
    currency VARCHAR(3) NOT NULL DEFAULT 'LKR',
    observed_at TIMESTAMPTZ NOT NULL,
    source_url TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_price_observations_store_product ON price_observations (store_product_id);
CREATE INDEX IF NOT EXISTS idx_price_observations_observed_at ON price_observations (observed_at DESC);

-- 6. Collector Runs Table (Audit and Operational Logs)
CREATE TABLE IF NOT EXISTS collector_runs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    supermarket_id UUID NOT NULL REFERENCES supermarkets(id) ON DELETE CASCADE,
    started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    finished_at TIMESTAMPTZ,
    status VARCHAR(50) NOT NULL CHECK (status IN ('running', 'success', 'failed', 'blocked')),
    products_checked INT NOT NULL DEFAULT 0,
    products_updated INT NOT NULL DEFAULT 0,
    error_message TEXT
);

CREATE INDEX IF NOT EXISTS idx_collector_runs_supermarket ON collector_runs (supermarket_id, started_at DESC);

-- 7. Product Matches Table
CREATE TABLE IF NOT EXISTS product_matches (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    product_id UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    store_product_id UUID NOT NULL REFERENCES store_products(id) ON DELETE CASCADE,
    match_method VARCHAR(50) NOT NULL CHECK (match_method IN ('barcode_exact', 'sku_exact', 'brand_name_unit_exact', 'high_confidence_heuristic')),
    confidence NUMERIC(4,3) NOT NULL CHECK (confidence >= 0 AND confidence <= 1.0),
    verification_status VARCHAR(50) NOT NULL DEFAULT 'verified' CHECK (verification_status IN ('verified', 'pending_review', 'rejected')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_product_store_product_match UNIQUE (product_id, store_product_id)
);

CREATE INDEX IF NOT EXISTS idx_product_matches_verification ON product_matches (verification_status);
