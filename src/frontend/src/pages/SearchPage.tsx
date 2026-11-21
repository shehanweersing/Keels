import React, { useState, useMemo } from 'react';
import type { VerifiedProduct, Supermarket } from '../types';
import { ProductCard } from '../components/ProductCard';
import { EmptyState } from '../components/EmptyState';
import { Search, Filter, ArrowUpDown, X } from 'lucide-react';

interface SearchPageProps {
  products: VerifiedProduct[];
  supermarkets: Supermarket[];
  initialQuery?: string;
  onSelectProduct: (product: VerifiedProduct) => void;
}

export const SearchPage: React.FC<SearchPageProps> = ({
  products,
  supermarkets,
  initialQuery = '',
  onSelectProduct
}) => {
  const [searchQuery, setSearchQuery] = useState(initialQuery);
  const [selectedSupermarket, setSelectedSupermarket] = useState<string>('all');
  const [selectedCategory, setSelectedCategory] = useState<string>('all');
  const [sortBy, setSortBy] = useState<'price_asc' | 'price_desc' | 'name' | 'difference'>('price_asc');

  // Categories list
  const categories = useMemo(() => {
    return Array.from(new Set(products.map(p => p.Category))).sort();
  }, [products]);

  // Filtered and sorted products
  const filteredProducts = useMemo(() => {
    return products.filter(p => {
      // Query filter
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase();
        const matchesName = p.CanonicalName.toLowerCase().includes(q);
        const matchesBrand = p.Brand.toLowerCase().includes(q);
        const matchesCategory = p.Category.toLowerCase().includes(q);
        if (!matchesName && !matchesBrand && !matchesCategory) return false;
      }

      // Supermarket filter
      if (selectedSupermarket !== 'all') {
        const hasOffer = p.Offers.some(o => o.SupermarketSlug === selectedSupermarket);
        if (!hasOffer) return false;
      }

      // Category filter
      if (selectedCategory !== 'all') {
        if (p.Category !== selectedCategory) return false;
      }

      return true;
    }).sort((a, b) => {
      switch (sortBy) {
        case 'price_asc':
          return a.MinPrice - b.MinPrice;
        case 'price_desc':
          return b.MinPrice - a.MinPrice;
        case 'difference':
          return b.PriceDifference - a.PriceDifference;
        case 'name':
        default:
          return a.CanonicalName.localeCompare(b.CanonicalName);
      }
    });
  }, [products, searchQuery, selectedSupermarket, selectedCategory, sortBy]);

  return (
    <div className="space-y-6">
      {/* Search Header */}
      <div className="bg-white rounded-2xl border border-slate-200/80 p-6 shadow-sm">
        <h2 className="text-xl font-bold text-slate-900 mb-4">Search Verified Supermarket Catalog</h2>
        
        {/* Search input */}
        <div className="relative">
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Search by product name, brand, or category (e.g. Milk, Rice, Dilmah)..."
            className="w-full pl-11 pr-10 py-3 rounded-xl border border-slate-200 bg-slate-50 text-slate-900 placeholder-slate-400 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 focus:bg-white transition"
          />
          <Search className="w-5 h-5 text-slate-400 absolute left-3.5 top-3.5" />
          {searchQuery && (
            <button
              onClick={() => setSearchQuery('')}
              className="absolute right-3 top-3 text-slate-400 hover:text-slate-600"
            >
              <X className="w-5 h-5" />
            </button>
          )}
        </div>

        {/* Filter controls */}
        <div className="mt-4 pt-4 border-t border-slate-100 flex flex-wrap items-center justify-between gap-4">
          <div className="flex flex-wrap items-center gap-3">
            {/* Supermarket select */}
            <div className="flex items-center gap-1.5 text-xs text-slate-600">
              <Filter className="w-3.5 h-3.5 text-slate-400" />
              <span>Supermarket:</span>
              <select
                value={selectedSupermarket}
                onChange={(e) => setSelectedSupermarket(e.target.value)}
                className="bg-slate-50 border border-slate-200 rounded-lg px-2.5 py-1 text-xs font-semibold text-slate-800 focus:outline-none focus:ring-1 focus:ring-emerald-500"
              >
                <option value="all">All Supermarkets</option>
                {supermarkets.map(s => (
                  <option key={s.Slug} value={s.Slug} disabled={s.IntegrationStatus === 'blocked'}>
                    {s.Name} {s.IntegrationStatus === 'blocked' ? '(Blocked)' : ''}
                  </option>
                ))}
              </select>
            </div>

            {/* Category select */}
            <div className="flex items-center gap-1.5 text-xs text-slate-600">
              <span>Category:</span>
              <select
                value={selectedCategory}
                onChange={(e) => setSelectedCategory(e.target.value)}
                className="bg-slate-50 border border-slate-200 rounded-lg px-2.5 py-1 text-xs font-semibold text-slate-800 focus:outline-none focus:ring-1 focus:ring-emerald-500"
              >
                <option value="all">All Categories</option>
                {categories.map(c => (
                  <option key={c} value={c}>{c}</option>
                ))}
              </select>
            </div>
          </div>

          {/* Sort control */}
          <div className="flex items-center gap-1.5 text-xs text-slate-600">
            <ArrowUpDown className="w-3.5 h-3.5 text-slate-400" />
            <span>Sort by:</span>
            <select
              value={sortBy}
              onChange={(e) => setSortBy(e.target.value as any)}
              className="bg-slate-50 border border-slate-200 rounded-lg px-2.5 py-1 text-xs font-semibold text-slate-800 focus:outline-none focus:ring-1 focus:ring-emerald-500"
            >
              <option value="price_asc">Price: Low to High</option>
              <option value="price_desc">Price: High to Low</option>
              <option value="difference">Biggest Price Difference</option>
              <option value="name">Product Name (A-Z)</option>
            </select>
          </div>
        </div>
      </div>

      {/* Results Header */}
      <div className="flex items-center justify-between text-xs text-slate-500 px-1">
        <span>Found <strong className="text-slate-800 font-bold">{filteredProducts.length}</strong> verified products</span>
        <span>Showing real prices in Sri Lankan Rupees (LKR)</span>
      </div>

      {/* Grid or Empty State */}
      {filteredProducts.length === 0 ? (
        <EmptyState
          isSearching={true}
          title="No Products Match Your Search"
          description={`No verified supermarket items were found matching "${searchQuery}". Try a different keyword like "Milk", "Rice", "Tea", or clear your supermarket and category filters.`}
          onRefresh={() => {
            setSearchQuery('');
            setSelectedSupermarket('all');
            setSelectedCategory('all');
          }}
        />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
          {filteredProducts.map(product => (
            <ProductCard
              key={product.Id}
              product={product}
              onSelect={onSelectProduct}
            />
          ))}
        </div>
      )}
    </div>
  );
};
