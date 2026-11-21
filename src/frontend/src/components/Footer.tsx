import React from 'react';
import { ShieldCheck, Info, CheckCircle2, AlertTriangle } from 'lucide-react';

export const Footer: React.FC = () => {
  return (
    <footer className="bg-white border-t border-slate-200 mt-20">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-12">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
          <div>
            <div className="flex items-center gap-2">
              <span className="font-bold text-lg text-slate-900">PriceWise <span className="text-emerald-600">LK</span></span>
              <span className="text-xs bg-emerald-100 text-emerald-800 px-2 py-0.5 rounded font-medium">Verified Live Data</span>
            </div>
            <p className="mt-3 text-sm text-slate-600 leading-relaxed">
              Real-world supermarket price intelligence for Sri Lankan consumers. Built on strict data integrity principles with zero fabricated prices or simulated products.
            </p>
          </div>

          <div>
            <h4 className="font-semibold text-sm text-slate-900 uppercase tracking-wider">Supermarket Integrations</h4>
            <ul className="mt-3 space-y-2 text-sm text-slate-600">
              <li className="flex items-center gap-2">
                <CheckCircle2 className="w-4 h-4 text-emerald-600" />
                <span>Cargills Food City (Live Public JSON API)</span>
              </li>
              <li className="flex items-center gap-2">
                <CheckCircle2 className="w-4 h-4 text-emerald-600" />
                <span>Arpico Supercentre (Live OpenCart Catalog)</span>
              </li>
              <li className="flex items-center gap-2">
                <AlertTriangle className="w-4 h-4 text-amber-500" />
                <span>Keells Super (Blocked: Session Encryption Required)</span>
              </li>
            </ul>
          </div>

          <div>
            <h4 className="font-semibold text-sm text-slate-900 uppercase tracking-wider">Data Transparency</h4>
            <div className="mt-3 bg-slate-50 p-4 rounded-xl border border-slate-200 text-xs text-slate-600 space-y-2">
              <div className="flex items-start gap-2">
                <ShieldCheck className="w-4 h-4 text-emerald-600 shrink-0 mt-0.5" />
                <p>
                  Prices are collected directly from authorized, publicly accessible supermarket storefronts in Sri Lanka.
                </p>
              </div>
              <div className="flex items-start gap-2">
                <Info className="w-4 h-4 text-blue-600 shrink-0 mt-0.5" />
                <p>
                  Observed prices represent recorded point-in-time shelf amounts. In-store prices or localized promotions may differ.
                </p>
              </div>
            </div>
          </div>
        </div>

        <div className="mt-8 pt-8 border-t border-slate-100 flex flex-col sm:flex-row items-center justify-between text-xs text-slate-500">
          <p>© {new Date().getFullYear()} PriceWise LK. All product trademarks, logos, and brand names belong to their respective owners.</p>
          <p className="mt-2 sm:mt-0">Colombo, Sri Lanka</p>
        </div>
      </div>
    </footer>
  );
};
