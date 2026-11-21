import React from 'react';
import { PackageSearch, DatabaseZap, AlertCircle, RefreshCw } from 'lucide-react';

interface EmptyStateProps {
  title?: string;
  description?: string;
  onRefresh?: () => void;
  isSearching?: boolean;
}

export const EmptyState: React.FC<EmptyStateProps> = ({
  title = 'No Verified Price Information Available',
  description = 'PriceWise LK strictly displays only genuinely verified supermarket data. If the catalog is empty or an active collection is in progress, check back shortly or visit the Status & Audits page to monitor adapter runs.',
  onRefresh,
  isSearching = false
}) => {
  return (
    <div className="bg-white rounded-2xl border border-slate-200/80 p-8 sm:p-12 text-center max-w-lg mx-auto shadow-sm my-8">
      <div className="w-16 h-16 rounded-2xl bg-slate-100 border border-slate-200 text-slate-500 mx-auto flex items-center justify-center mb-5">
        {isSearching ? <PackageSearch className="w-8 h-8 text-slate-600" /> : <DatabaseZap className="w-8 h-8 text-emerald-600" />}
      </div>
      <h3 className="text-lg font-bold text-slate-900 tracking-tight">{title}</h3>
      <p className="mt-2 text-sm text-slate-600 leading-relaxed max-w-md mx-auto">
        {description}
      </p>

      <div className="mt-6 flex flex-col sm:flex-row items-center justify-center gap-3">
        {onRefresh && (
          <button
            onClick={onRefresh}
            className="w-full sm:w-auto px-4 py-2 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-medium text-sm transition shadow-sm flex items-center justify-center gap-2"
          >
            <RefreshCw className="w-4 h-4" />
            <span>Check for Updates</span>
          </button>
        )}
      </div>

      <div className="mt-8 pt-6 border-t border-slate-100 flex items-center justify-center gap-2 text-xs text-slate-500">
        <AlertCircle className="w-4 h-4 text-emerald-600" />
        <span>Strict Real Data Mode: No synthetic placeholder records.</span>
      </div>
    </div>
  );
};
