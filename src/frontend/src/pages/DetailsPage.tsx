import React from 'react';
import type { VerifiedProduct } from '../types';
import { ArrowLeft, ExternalLink, Calendar, Tag, TrendingUp, Store } from 'lucide-react';

interface DetailsPageProps {
  product: VerifiedProduct;
  onBack: () => void;
}

export const DetailsPage: React.FC<DetailsPageProps> = ({ product, onBack }) => {
  const formatPrice = (val?: number | null) => {
    if (val === undefined || val === null) return 'N/A';
    return `Rs. ${val.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };

  const primaryImage = product.Offers.find(o => o.ImageUrl)?.ImageUrl;

  return (
    <div className="space-y-8 max-w-4xl mx-auto">
      {/* Back button */}
      <button
        onClick={onBack}
        className="inline-flex items-center gap-2 text-xs font-semibold text-slate-600 hover:text-emerald-700 transition"
      >
        <ArrowLeft className="w-4 h-4" />
        <span>Back to Catalog</span>
      </button>

      {/* Main Product Header Card */}
      <div className="bg-white rounded-3xl border border-slate-200/80 p-6 sm:p-8 shadow-sm">
        <div className="flex flex-col md:flex-row items-start gap-8">
          {/* Image */}
          <div className="w-full md:w-64 h-64 rounded-2xl bg-slate-50 border border-slate-100 p-6 flex items-center justify-center shrink-0">
            {primaryImage ? (
              <img
                src={primaryImage}
                alt={product.CanonicalName}
                className="max-h-full max-w-full object-contain mix-blend-multiply"
              />
            ) : (
              <Tag className="w-12 h-12 text-slate-300" />
            )}
          </div>

          {/* Metadata */}
          <div className="flex-1 space-y-4">
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-xs font-bold uppercase tracking-wider text-emerald-800 bg-emerald-50 border border-emerald-200 px-2.5 py-0.5 rounded-full">
                {product.Brand}
              </span>
              <span className="text-xs font-semibold text-slate-600 bg-slate-100 px-2.5 py-0.5 rounded-full">
                {product.Category}
              </span>
              <span className="text-xs font-semibold text-slate-600 bg-slate-100 px-2.5 py-0.5 rounded-full">
                {product.Quantity > 0 ? `${product.Quantity} ${product.Unit}` : product.Unit}
              </span>
            </div>

            <h1 className="text-2xl sm:text-3xl font-black text-slate-900 tracking-tight">
              {product.CanonicalName}
            </h1>

            {product.Barcode && (
              <p className="text-xs text-slate-500 font-mono">
                Barcode / GTIN: <strong>{product.Barcode}</strong>
              </p>
            )}

            <div className="pt-4 border-t border-slate-100 flex items-baseline gap-4">
              <div>
                <span className="text-xs text-slate-500 block">Lowest Verified Price</span>
                <span className="text-3xl font-black text-slate-900">
                  {formatPrice(product.MinPrice)}
                </span>
              </div>

              {product.CheapestSupermarket && (
                <div className="bg-emerald-50 border border-emerald-200 px-3 py-1.5 rounded-xl">
                  <span className="text-[10px] text-emerald-700 uppercase tracking-wider font-semibold block">Best Offer At</span>
                  <span className="text-sm font-bold text-emerald-900">{product.CheapestSupermarket}</span>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Verified Supermarket Store Listings */}
      <div className="bg-white rounded-3xl border border-slate-200/80 p-6 sm:p-8 shadow-sm space-y-6">
        <h3 className="text-lg font-bold text-slate-900 flex items-center gap-2">
          <Store className="w-5 h-5 text-emerald-600" />
          <span>Verified Supermarket Offers</span>
        </h3>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {product.Offers.map(offer => (
            <div
              key={offer.StoreProductId}
              className={`p-5 rounded-2xl border transition flex flex-col justify-between ${
                offer.CurrentPrice === product.MinPrice
                  ? 'bg-emerald-50/50 border-emerald-300 shadow-xs'
                  : 'bg-slate-50 border-slate-200'
              }`}
            >
              <div>
                <div className="flex items-center justify-between">
                  <span className="font-extrabold text-slate-900 text-base">{offer.SupermarketName}</span>
                  {offer.CurrentPrice === product.MinPrice && (
                    <span className="text-xs font-bold text-emerald-700 bg-emerald-100 px-2 py-0.5 rounded-full">
                      Cheapest Offer
                    </span>
                  )}
                </div>

                <div className="mt-4 flex items-baseline gap-2">
                  <span className="text-2xl font-black text-slate-900">{formatPrice(offer.CurrentPrice)}</span>
                  {offer.OriginalPrice && offer.OriginalPrice > offer.CurrentPrice && (
                    <span className="text-xs text-slate-400 line-through">
                      {formatPrice(offer.OriginalPrice)}
                    </span>
                  )}
                </div>

                <p className="mt-1 text-xs text-slate-500">
                  SKU / Source Code: <code className="bg-white px-1 py-0.5 rounded border border-slate-200">{offer.SourceProductId}</code>
                </p>

                <div className="mt-3 flex items-center gap-1.5 text-xs text-slate-500">
                  <Calendar className="w-3.5 h-3.5" />
                  <span>Last Observed: {new Date(offer.LastObservedAt).toLocaleString()}</span>
                </div>
              </div>

              <div className="mt-5 pt-4 border-t border-slate-200/60">
                <a
                  href={offer.ProductUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="w-full py-2.5 px-4 rounded-xl bg-white border border-slate-200 hover:border-emerald-500 hover:text-emerald-700 font-semibold text-xs text-slate-700 flex items-center justify-center gap-1.5 transition shadow-2xs"
                >
                  <span>View Original Listing on {offer.SupermarketName}</span>
                  <ExternalLink className="w-3.5 h-3.5" />
                </a>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Observation History Timeline */}
      <div className="bg-white rounded-3xl border border-slate-200/80 p-6 sm:p-8 shadow-sm space-y-4">
        <h3 className="text-lg font-bold text-slate-900 flex items-center gap-2">
          <TrendingUp className="w-5 h-5 text-emerald-600" />
          <span>Price Observation History</span>
        </h3>
        <p className="text-xs text-slate-500">
          Auditable point-in-time shelf price observations recorded by the collector.
        </p>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs border-collapse">
            <thead>
              <tr className="border-b border-slate-200 bg-slate-50 text-slate-600 uppercase tracking-wider font-semibold">
                <th className="py-2.5 px-4">Supermarket</th>
                <th className="py-2.5 px-4">Observed Price</th>
                <th className="py-2.5 px-4">Original Price</th>
                <th className="py-2.5 px-4">Observed Timestamp</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {product.Offers.flatMap(offer =>
                offer.PriceHistory.map((ph, idx) => (
                  <tr key={`${offer.StoreProductId}-${idx}`} className="hover:bg-slate-50/80">
                    <td className="py-2.5 px-4 font-semibold text-slate-800">{offer.SupermarketName}</td>
                    <td className="py-2.5 px-4 font-bold text-emerald-700">{formatPrice(ph.Price)}</td>
                    <td className="py-2.5 px-4 text-slate-400">{ph.OriginalPrice ? formatPrice(ph.OriginalPrice) : '—'}</td>
                    <td className="py-2.5 px-4 text-slate-500">{new Date(ph.ObservedAt).toLocaleString()}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
