import React, { useState } from 'react';
import type { VerifiedProduct, Supermarket } from '../types';
import { EmptyState } from '../components/EmptyState';
import { Scale, ExternalLink, ArrowRight, Tag } from 'lucide-react';

interface ComparisonPageProps {
  products: VerifiedProduct[];
  supermarkets: Supermarket[];
  onSelectProduct: (product: VerifiedProduct) => void;
}

export const ComparisonPage: React.FC<ComparisonPageProps> = ({
  products,
  supermarkets,
  onSelectProduct
}) => {
  const [filterQuery, setFilterQuery] = useState('');

  // Find products that have multiple offers or allow comparing all products across active supermarkets
  const multiOfferProducts = products.filter(p => p.OffersCount > 1);
  const displayProducts = (multiOfferProducts.length > 0 ? multiOfferProducts : products)
    .filter(p => {
      if (!filterQuery.trim()) return true;
      const q = filterQuery.toLowerCase();
      return p.CanonicalName.toLowerCase().includes(q) || p.Brand.toLowerCase().includes(q);
    });

  const formatPrice = (val?: number | null) => {
    if (val === undefined || val === null || val <= 0) return null;
    return `Rs. ${val.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };

  return (
    <div className="space-y-8">
      {/* Page Header */}
      <div className="bg-white rounded-2xl border border-slate-200/80 p-6 shadow-sm">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <div className="flex items-center gap-2">
              <div className="w-8 h-8 rounded-lg bg-emerald-100 text-emerald-700 flex items-center justify-center font-bold">
                <Scale className="w-4 h-4" />
              </div>
              <h2 className="text-xl font-bold text-slate-900">Cross-Supermarket Price Comparison</h2>
            </div>
            <p className="mt-1 text-xs text-slate-500">
              Side-by-side comparison of verified offers. The cheapest verified price is automatically highlighted.
            </p>
          </div>

          <div className="w-full md:w-72">
            <input
              type="text"
              value={filterQuery}
              onChange={(e) => setFilterQuery(e.target.value)}
              placeholder="Filter comparisons..."
              className="w-full px-3.5 py-2 text-xs rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition"
            />
          </div>
        </div>

        {/* Supermarket Legend Bar */}
        <div className="mt-6 pt-4 border-t border-slate-100 grid grid-cols-1 sm:grid-cols-3 gap-3 text-xs">
          {supermarkets.map(s => (
            <div key={s.Slug} className="flex items-center gap-2 p-2.5 rounded-xl bg-slate-50 border border-slate-200/70">
              <span className={`w-2.5 h-2.5 rounded-full ${s.IntegrationStatus === 'active' ? 'bg-emerald-500' : 'bg-amber-500'}`} />
              <div className="min-w-0 flex-1">
                <span className="font-bold text-slate-800 block truncate">{s.Name}</span>
                <span className="text-[10px] text-slate-500 capitalize">{s.IntegrationStatus === 'active' ? 'Live Price Feed' : 'Blocked (Auth Required)'}</span>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Comparison Table / Cards */}
      {displayProducts.length === 0 ? (
        <EmptyState
          title="No Comparative Matches Found"
          description="We could not find multiple supermarket offers matching your query. As additional products are cataloged from permitted supermarkets, cross-store comparisons will appear here."
          onRefresh={() => setFilterQuery('')}
        />
      ) : (
        <div className="space-y-4">
          {displayProducts.map(product => {
            const cargillsOffer = product.Offers.find(o => o.SupermarketSlug === 'cargills');
            const arpicoOffer = product.Offers.find(o => o.SupermarketSlug === 'arpico');
            const hasSavings = product.PriceDifference > 0;

            return (
              <div
                key={product.Id}
                className="bg-white rounded-2xl border border-slate-200/90 hover:border-emerald-300 p-5 shadow-sm transition flex flex-col lg:flex-row items-start lg:items-center justify-between gap-6"
              >
                {/* Product Summary Column */}
                <div className="flex items-start gap-4 min-w-[280px] max-w-sm">
                  <div className="w-16 h-16 rounded-xl bg-slate-50 border border-slate-100 p-2 flex items-center justify-center shrink-0">
                    {product.Offers[0]?.ImageUrl ? (
                      <img
                        src={product.Offers[0].ImageUrl}
                        alt={product.CanonicalName}
                        className="max-h-full max-w-full object-contain mix-blend-multiply"
                      />
                    ) : (
                      <Tag className="w-6 h-6 text-slate-400" />
                    )}
                  </div>

                  <div className="min-w-0">
                    <span className="text-[10px] uppercase font-bold text-slate-500 bg-slate-100 px-2 py-0.5 rounded">
                      {product.Brand} • {product.Quantity > 0 ? `${product.Quantity} ${product.Unit}` : product.Unit}
                    </span>
                    <h4 
                      onClick={() => onSelectProduct(product)}
                      className="font-bold text-slate-900 text-sm hover:text-emerald-700 cursor-pointer mt-1 line-clamp-2"
                    >
                      {product.CanonicalName}
                    </h4>

                    {hasSavings && (
                      <span className="mt-1.5 inline-flex items-center gap-1 text-xs font-bold text-emerald-700 bg-emerald-50 border border-emerald-200 px-2 py-0.5 rounded-md">
                        <span>Save {formatPrice(product.PriceDifference)}</span>
                      </span>
                    )}
                  </div>
                </div>

                {/* Supermarket Offers Grid */}
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 w-full lg:w-auto lg:flex-1 lg:max-w-2xl">
                  {/* Cargills Column */}
                  <div className={`p-3.5 rounded-xl border ${
                    cargillsOffer && cargillsOffer.CurrentPrice === product.MinPrice
                      ? 'bg-emerald-50/60 border-emerald-300 ring-1 ring-emerald-300'
                      : 'bg-slate-50 border-slate-200'
                  }`}>
                    <div className="flex items-center justify-between text-xs mb-1">
                      <span className="font-bold text-slate-700">Cargills</span>
                      {cargillsOffer && cargillsOffer.CurrentPrice === product.MinPrice && (
                        <span className="bg-emerald-600 text-white text-[10px] font-bold px-1.5 py-0.2 rounded">Cheapest</span>
                      )}
                    </div>
                    {cargillsOffer ? (
                      <div>
                        <span className="text-base font-extrabold text-slate-900">
                          {formatPrice(cargillsOffer.CurrentPrice)}
                        </span>
                        <a
                          href={cargillsOffer.ProductUrl}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="mt-2 text-[11px] text-emerald-600 hover:text-emerald-800 font-medium flex items-center gap-1"
                        >
                          <span>Store Link</span>
                          <ExternalLink className="w-3 h-3" />
                        </a>
                      </div>
                    ) : (
                      <span className="text-xs text-slate-400 italic block mt-2">Not listed</span>
                    )}
                  </div>

                  {/* Arpico Column */}
                  <div className={`p-3.5 rounded-xl border ${
                    arpicoOffer && arpicoOffer.CurrentPrice === product.MinPrice
                      ? 'bg-emerald-50/60 border-emerald-300 ring-1 ring-emerald-300'
                      : 'bg-slate-50 border-slate-200'
                  }`}>
                    <div className="flex items-center justify-between text-xs mb-1">
                      <span className="font-bold text-slate-700">Arpico</span>
                      {arpicoOffer && arpicoOffer.CurrentPrice === product.MinPrice && (
                        <span className="bg-emerald-600 text-white text-[10px] font-bold px-1.5 py-0.2 rounded">Cheapest</span>
                      )}
                    </div>
                    {arpicoOffer ? (
                      <div>
                        <span className="text-base font-extrabold text-slate-900">
                          {formatPrice(arpicoOffer.CurrentPrice)}
                        </span>
                        {arpicoOffer.OriginalPrice && arpicoOffer.OriginalPrice > arpicoOffer.CurrentPrice && (
                          <span className="text-[11px] text-slate-400 line-through block">
                            {formatPrice(arpicoOffer.OriginalPrice)}
                          </span>
                        )}
                        <a
                          href={arpicoOffer.ProductUrl}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="mt-2 text-[11px] text-emerald-600 hover:text-emerald-800 font-medium flex items-center gap-1"
                        >
                          <span>Store Link</span>
                          <ExternalLink className="w-3 h-3" />
                        </a>
                      </div>
                    ) : (
                      <span className="text-xs text-slate-400 italic block mt-2">Not listed</span>
                    )}
                  </div>

                  {/* Keells Column */}
                  <div className="p-3.5 rounded-xl border bg-amber-50/40 border-amber-200/80">
                    <div className="flex items-center justify-between text-xs mb-1">
                      <span className="font-bold text-slate-700">Keells</span>
                      <span className="text-amber-700 bg-amber-100 text-[10px] font-semibold px-1.5 py-0.2 rounded">Blocked</span>
                    </div>
                    <span className="text-xs text-slate-500 italic block mt-2">
                      Feed pending authorization
                    </span>
                  </div>
                </div>

                {/* Details Button */}
                <div className="shrink-0 self-end lg:self-center">
                  <button
                    onClick={() => onSelectProduct(product)}
                    className="px-4 py-2 rounded-xl bg-slate-100 hover:bg-emerald-50 hover:text-emerald-700 text-slate-700 font-semibold text-xs transition flex items-center gap-1"
                  >
                    <span>Price History</span>
                    <ArrowRight className="w-3.5 h-3.5" />
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
