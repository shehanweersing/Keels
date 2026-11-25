import React from 'react';
import { CheckCircle2, AlertCircle, XCircle, Clock } from 'lucide-react';

interface StatusBadgeProps {
  status: 'active' | 'blocked' | 'maintenance' | 'pending' | 'success' | 'failed' | 'running';
  label?: string;
  className?: string;
}

export const StatusBadge: React.FC<StatusBadgeProps> = ({ status, label, className = '' }) => {
  switch (status) {
    case 'active':
    case 'success':
      return (
        <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200 ${className}`}>
          <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600" />
          <span>{label || 'Active & Verified'}</span>
        </span>
      );

    case 'blocked':
      return (
        <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-amber-50 text-amber-800 border border-amber-200 ${className}`}>
          <AlertCircle className="w-3.5 h-3.5 text-amber-600" />
          <span>{label || 'Blocked (Auth Required)'}</span>
        </span>
      );

    case 'failed':
      return (
        <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-rose-50 text-rose-700 border border-rose-200 ${className}`}>
          <XCircle className="w-3.5 h-3.5 text-rose-600" />
          <span>{label || 'Failed'}</span>
        </span>
      );

    case 'maintenance':
      return (
        <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-amber-50 text-amber-800 border border-amber-200 ${className}`}>
          <Clock className="w-3.5 h-3.5 text-amber-600" />
          <span>{label || 'Degraded / Stale'}</span>
        </span>
      );

    case 'running':
    case 'pending':
    default:
      return (
        <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-slate-100 text-slate-700 border border-slate-200 ${className}`}>
          <Clock className="w-3.5 h-3.5 text-slate-500" />
          <span>{label || 'Pending'}</span>
        </span>
      );
  }
};
