import React, { useState } from 'react';
import type { VerifiedProduct, Supermarket } from '../types';
import { ProductCard } from '../components/ProductCard';
import { EmptyState } from '../components/EmptyState';
import { StatusBadge } from '../components/StatusBadge';
import { Search, Sparkles, ShieldCheck, ArrowRight, Store, Scale } from 'lucide-react';

interface HomePageProps {
  products: VerifiedProduct[];
  supermarkets: Supermarket[];
  onSearch: (query: string) => void;
  onSelectProduct: (product: VerifiedProduct) => void;
  onNavigate: (page: string, params?: any) => void;
}

export const HomePage: React.FC<HomePageProps> = ({
  products,
  supermarkets,
  onSearch,
  onSelectProduct,
  onNavigate
}) => {
  const [inputQuery, setInputQuery] = useState('');

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (inputQuery.trim()) {
      onSearch(inputQuery.trim());
    }
  };

  const multiOffers = products.filter(p => new Set(p.Offers.map(o => o.SupermarketSlug)).size >= 2);
  const recentProducts = products.slice(0, 8);

  const categories = Array.from(new Set(products.map(p => p.Category))).slice(0, 6);

  return (
    <div className="space-y-12 pb-12">
      {/* Hero Section */}
      <section className="relative overflow-hidden rounded-3xl bg-gradient-to-b from-emerald-900 via-emerald-800 to-slate-900 text-white p-8 sm:p-14 shadow-xl">
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_right,rgba(16,185,129,0.15),transparent_50%)]"></div>
        <div className="relative max-w-3xl mx-auto text-center space-y-6">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-emerald-500/20 border border-emerald-400/30 text-emerald-300 text-xs font-semibold uppercase tracking-wider backdrop-blur">
            <ShieldCheck className="w-4 h-4 text-emerald-400" />
            <span>100% Real Sri Lankan Supermarket Prices</span>
          </div>

          <h1 className="text-3xl sm:text-5xl font-black tracking-tight leading-tight">
            Stop Overpaying at <br className="hidden sm:inline" />
            <span className="text-emerald-400">Sri Lankan Supermarkets</span>
          </h1>

          <p className="text-sm sm:text-base text-slate-300 max-w-xl mx-auto">
            Compare verified shelf prices across Cargills Food City, Arpico Supercentre, and Keells Super. Audited data with zero simulated or fabricated prices.
          </p>

          {/* Search Bar */}
          <form onSubmit={handleSearchSubmit} className="relative max-w-xl mx-auto pt-2">
            <div className="relative flex items-center">
              <input
                type="text"
                value={inputQuery}
                onChange={(e) => setInputQuery(e.target.value)}
                placeholder="Search real products (e.g. Milkmaid, Basmati, Dilmah, Ambewela)..."
                className="w-full pl-12 pr-32 py-4 rounded-2xl bg-white text-slate-900 placeholder-slate-400 font-medium text-sm sm:text-base shadow-2xl focus:outline-none focus:ring-4 focus:ring-emerald-500/30"
              />
              <Search className="w-5 h-5 text-slate-400 absolute left-4 pointer-events-none" />
              <button
                type="submit"
                className="absolute right-2 px-5 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-sm transition shadow-sm"
              >
                Search
              </button>
            </div>
          </form>

          {/* Quick Search Chips */}
          <div className="flex flex-wrap items-center justify-center gap-2 pt-2">
            <span className="text-xs text-slate-400">Popular:</span>
            {['Milk', 'Tea', 'Rice', 'Biscuits', 'Dairy'].map(tag => (
              <button
                key={tag}
                onClick={() => onSearch(tag)}
                className="text-xs bg-white/10 hover:bg-white/20 text-slate-200 px-3 py-1 rounded-full border border-white/10 transition"
              >
                {tag}
              </button>
            ))}
          </div>
        </div>
      </section>

      {/* Supermarket Integration Status Strip */}
      <section className="bg-white rounded-2xl border border-slate-200/80 p-6 shadow-sm">
        <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 mb-4 pb-4 border-b border-slate-100">
          <div>
            <h3 className="text-base font-bold text-slate-900 flex items-center gap-2">
              <Store className="w-5 h-5 text-emerald-600" />
              <span>Supermarket Source Verification Status</span>
            </h3>
            <p className="text-xs text-slate-500">
              Only authorized and permitted access methods are used. Blocked sources are honestly disclosed.
            </p>
          </div>
          <button
            onClick={() => onNavigate('status')}
            className="text-xs font-semibold text-emerald-600 hover:text-emerald-700 flex items-center gap-1"
          >
            <span>View Full Audit Logs</span>
            <ArrowRight className="w-3.5 h-3.5" />
          </button>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {supermarkets.map(s => (
            <div key={s.Slug} className="p-4 rounded-xl bg-slate-50 border border-slate-200/80 flex flex-col justify-between">
              <div className="flex items-start justify-between gap-2">
                <span className="font-bold text-slate-900 text-sm">{s.Name}</span>
                <StatusBadge status={s.IntegrationStatus} />
              </div>
              <p className="mt-2 text-xs text-slate-600 line-clamp-2">
                {s.StatusReason || 'Integration active and monitored.'}
              </p>
            </div>
          ))}
        </div>
      </section>

      {/* Cross-Store Multi-Offer Comparisons */}
      {multiOffers.length > 0 && (
        <section className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <div className="flex items-center gap-2">
                <Scale className="w-5 h-5 text-emerald-600" />
                <h3 className="text-xl font-bold text-slate-900">Verified Cross-Store Price Differences</h3>
              </div>
              <p className="text-xs text-slate-500">
                Exact matching products verified across multiple supermarkets.
              </p>
            </div>
            <button
              onClick={() => onNavigate('compare')}
              className="text-sm font-semibold text-emerald-600 hover:text-emerald-700 flex items-center gap-1"
            >
              <span>See All Comparisons ({multiOffers.length})</span>
              <ArrowRight className="w-4 h-4" />
            </button>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
            {multiOffers.slice(0, 4).map(product => (
              <ProductCard
                key={product.Id}
                product={product}
                onSelect={onSelectProduct}
              />
            ))}
          </div>
        </section>
      )}

      {/* Verified Recently Updated Products */}
      <section className="space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <div className="flex items-center gap-2">
              <Sparkles className="w-5 h-5 text-emerald-600" />
              <h3 className="text-xl font-bold text-slate-900">Verified Shelf Prices</h3>
            </div>
            <p className="text-xs text-slate-500">
              Showing {products.length} genuinely verified items collected directly from supermarket platforms.
            </p>
          </div>
          <button
            onClick={() => onNavigate('search')}
            className="text-sm font-semibold text-emerald-600 hover:text-emerald-700 flex items-center gap-1"
          >
            <span>Browse Full Catalog</span>
            <ArrowRight className="w-4 h-4" />
          </button>
        </div>

        {products.length === 0 ? (
          <EmptyState />
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
            {recentProducts.map(product => (
              <ProductCard
                key={product.Id}
                product={product}
                onSelect={onSelectProduct}
              />
            ))}
          </div>
        )}
      </section>
    </div>
  );
};
