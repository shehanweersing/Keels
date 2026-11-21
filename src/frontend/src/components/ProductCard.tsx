import React from 'react';
import type { VerifiedProduct } from '../types';
import { ShoppingCart, ChevronRight, Scale } from 'lucide-react';

interface ProductCardProps {
  product: VerifiedProduct;
  onSelect: (product: VerifiedProduct) => void;
}

export const ProductCard: React.FC<ProductCardProps> = ({ product, onSelect }) => {
  const primaryOffer = product.Offers[0];
  const hasMultipleOffers = product.OffersCount > 1;

  // Format currency
  const formatPrice = (val?: number) => {
    if (val === undefined || val === null) return 'N/A';
    return `Rs. ${val.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };

  return (
    <div className="bg-white rounded-2xl border border-slate-200/80 hover:border-emerald-300 hover:shadow-lg transition-all duration-200 flex flex-col overflow-hidden group">
      {/* Top Image & Badge */}
      <div className="relative h-48 bg-slate-50 flex items-center justify-center p-4 overflow-hidden border-b border-slate-100">
        {primaryOffer?.ImageUrl ? (
          <img
            src={primaryOffer.ImageUrl}
            alt={product.CanonicalName}
            className="max-h-full max-w-full object-contain mix-blend-multiply group-hover:scale-105 transition-transform duration-300"
            loading="lazy"
            onError={(e) => {
              // Hide broken image and fallback to icon
              (e.target as HTMLElement).style.display = 'none';
            }}
          />
        ) : (
          <div className="w-16 h-16 rounded-xl bg-slate-200 text-slate-400 flex items-center justify-center">
            <ShoppingCart className="w-8 h-8" />
          </div>
        )}

        {/* Brand Tag */}
        <span className="absolute top-3 left-3 bg-white/90 backdrop-blur text-slate-700 text-[11px] font-semibold px-2 py-0.5 rounded-md border border-slate-200/80 shadow-xs">
          {product.Brand}
        </span>

        {/* Multiple Offers / Price Difference Indicator */}
        {hasMultipleOffers && product.PriceDifference > 0 && (
          <span className="absolute top-3 right-3 bg-emerald-600 text-white text-[11px] font-bold px-2 py-0.5 rounded-md shadow-xs flex items-center gap-1">
            <Scale className="w-3 h-3" />
            <span>Save up to {formatPrice(product.PriceDifference)}</span>
          </span>
        )}
      </div>

      {/* Content */}
      <div className="p-4 flex-1 flex flex-col">
        {/* Category & Quantity */}
        <div className="flex items-center justify-between text-xs text-slate-500 mb-1">
          <span className="uppercase tracking-wider font-medium text-[10px] bg-slate-100 px-2 py-0.5 rounded">
            {product.Category}
          </span>
          <span className="font-semibold text-slate-600">
            {product.Quantity > 0 ? `${product.Quantity} ${product.Unit}` : product.Unit}
          </span>
        </div>

        {/* Title */}
        <h4 
          onClick={() => onSelect(product)}
          className="font-bold text-slate-900 text-base leading-snug line-clamp-2 hover:text-emerald-700 cursor-pointer transition mb-2"
        >
          {product.CanonicalName}
        </h4>

        {/* Price Section */}
        <div className="mt-auto pt-3 border-t border-slate-100">
          <div className="flex items-baseline justify-between">
            <div>
              <span className="text-xs text-slate-500 block">
                {hasMultipleOffers ? 'Best Verified Price' : 'Verified Shelf Price'}
              </span>
              <div className="flex items-baseline gap-2">
                <span className="text-xl font-extrabold text-slate-900">
                  {formatPrice(product.MinPrice)}
                </span>
                {primaryOffer?.OriginalPrice && primaryOffer.OriginalPrice > product.MinPrice && (
                  <span className="text-xs text-slate-400 line-through">
                    {formatPrice(primaryOffer.OriginalPrice)}
                  </span>
                )}
              </div>
            </div>

            {product.CheapestSupermarket && (
              <div className="text-right">
                <span className="text-[10px] text-slate-400 uppercase tracking-wider block">Found at</span>
                <span className="text-xs font-semibold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded-md border border-emerald-100 inline-block">
                  {product.CheapestSupermarket}
                </span>
              </div>
            )}
          </div>

          {/* Supermarket availability breakdown */}
          <div className="mt-3 flex items-center justify-between gap-2 pt-2 text-xs">
            <div className="flex items-center gap-1.5 text-slate-500">
              <span className="inline-block w-2 h-2 rounded-full bg-emerald-500"></span>
              <span>
                {product.OffersCount === 1 
                  ? `Verified at 1 store` 
                  : `Comparing ${product.OffersCount} stores`}
              </span>
            </div>

            <button
              onClick={() => onSelect(product)}
              className="text-emerald-600 hover:text-emerald-700 font-semibold flex items-center gap-0.5 hover:underline"
            >
              <span>Details</span>
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
