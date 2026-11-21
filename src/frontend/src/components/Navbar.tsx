import React from 'react';
import { ShoppingBag, Search, Scale, Activity } from 'lucide-react';

interface NavbarProps {
  currentPage: string;
  onNavigate: (page: string, params?: any) => void;
  freshnessTimestamp?: string | null;
}

export const Navbar: React.FC<NavbarProps> = ({ currentPage, onNavigate, freshnessTimestamp }) => {
  return (
    <header className="sticky top-0 z-50 bg-white/95 backdrop-blur border-b border-slate-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16">
          {/* Logo & Brand */}
          <div 
            onClick={() => onNavigate('home')} 
            className="flex items-center gap-3 cursor-pointer group"
          >
            <div className="w-10 h-10 rounded-xl bg-emerald-600 text-white flex items-center justify-center shadow-md shadow-emerald-600/20 group-hover:bg-emerald-700 transition">
              <ShoppingBag className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="font-bold text-xl tracking-tight text-slate-900">PriceWise <span className="text-emerald-600">LK</span></span>
                <span className="text-[10px] font-semibold uppercase px-1.5 py-0.5 rounded bg-emerald-100 text-emerald-800 tracking-wider">Real Data</span>
              </div>
              <p className="text-xs text-slate-500 hidden sm:block">Sri Lankan Supermarket Price Intelligence</p>
            </div>
          </div>

          {/* Navigation Links */}
          <nav className="flex items-center gap-1 sm:gap-2">
            <button
              onClick={() => onNavigate('home')}
              className={`px-3 py-2 rounded-lg text-sm font-medium transition ${
                currentPage === 'home' 
                  ? 'bg-emerald-50 text-emerald-700 font-semibold' 
                  : 'text-slate-600 hover:text-slate-900 hover:bg-slate-100'
              }`}
            >
              Home
            </button>

            <button
              onClick={() => onNavigate('search')}
              className={`px-3 py-2 rounded-lg text-sm font-medium flex items-center gap-1.5 transition ${
                currentPage === 'search' 
                  ? 'bg-emerald-50 text-emerald-700 font-semibold' 
                  : 'text-slate-600 hover:text-slate-900 hover:bg-slate-100'
              }`}
            >
              <Search className="w-4 h-4" />
              <span>Search</span>
            </button>

            <button
              onClick={() => onNavigate('compare')}
              className={`px-3 py-2 rounded-lg text-sm font-medium flex items-center gap-1.5 transition ${
                currentPage === 'compare' 
                  ? 'bg-emerald-50 text-emerald-700 font-semibold' 
                  : 'text-slate-600 hover:text-slate-900 hover:bg-slate-100'
              }`}
            >
              <Scale className="w-4 h-4" />
              <span>Compare</span>
            </button>

            <button
              onClick={() => onNavigate('status')}
              className={`px-3 py-2 rounded-lg text-sm font-medium flex items-center gap-1.5 transition ${
                currentPage === 'status' 
                  ? 'bg-emerald-50 text-emerald-700 font-semibold' 
                  : 'text-slate-600 hover:text-slate-900 hover:bg-slate-100'
              }`}
            >
              <Activity className="w-4 h-4" />
              <span className="hidden sm:inline">Status & Audits</span>
              <span className="sm:hidden">Status</span>
            </button>
          </nav>

          {/* Freshness Badge */}
          <div className="hidden lg:flex items-center gap-2 pl-4 border-l border-slate-200">
            <div className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></div>
            <span className="text-xs text-slate-500">
              {freshnessTimestamp 
                ? `Updated: ${new Date(freshnessTimestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}` 
                : 'Live Verified'}
            </span>
          </div>
        </div>
      </div>
    </header>
  );
};
