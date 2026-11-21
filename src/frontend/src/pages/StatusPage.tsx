import React from 'react';
import type { Supermarket, CollectorRun } from '../types';
import { StatusBadge } from '../components/StatusBadge';
import { Activity, ShieldAlert, RefreshCw, Database } from 'lucide-react';

interface StatusPageProps {
  supermarkets: Supermarket[];
  collectorRuns: CollectorRun[];
  onRefresh: () => void;
}

export const StatusPage: React.FC<StatusPageProps> = ({
  supermarkets,
  collectorRuns,
  onRefresh
}) => {
  return (
    <div className="space-y-8 max-w-5xl mx-auto">
      {/* Status Header */}
      <div className="bg-white rounded-3xl border border-slate-200/80 p-6 sm:p-8 shadow-sm flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-lg bg-emerald-100 text-emerald-700 flex items-center justify-center font-bold">
              <Activity className="w-4 h-4" />
            </div>
            <h2 className="text-xl font-bold text-slate-900">Collector & Integration Audits</h2>
          </div>
          <p className="mt-1 text-xs text-slate-500">
            Real-time status of supermarket data feeds, authorization boundaries, and scheduled runs.
          </p>
        </div>

        <button
          onClick={onRefresh}
          className="px-4 py-2 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-700 font-semibold text-xs flex items-center gap-2 transition self-start sm:self-center"
        >
          <RefreshCw className="w-3.5 h-3.5" />
          <span>Refresh Status</span>
        </button>
      </div>

      {/* Supermarket Adapters Grid */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {supermarkets.map(s => {
          const isBlocked = s.IntegrationStatus === 'blocked';
          const isActive = s.IntegrationStatus === 'active';

          return (
            <div
              key={s.Slug}
              className={`rounded-2xl border p-6 flex flex-col justify-between ${
                isActive 
                  ? 'bg-white border-emerald-200 shadow-xs' 
                  : 'bg-white border-amber-200 shadow-xs'
              }`}
            >
              <div>
                <div className="flex items-center justify-between">
                  <h4 className="font-extrabold text-slate-900 text-base">{s.Name}</h4>
                  <StatusBadge status={s.IntegrationStatus} />
                </div>

                <a
                  href={s.WebsiteUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="mt-1 text-xs text-slate-400 hover:text-slate-600 block truncate"
                >
                  {s.WebsiteUrl}
                </a>

                <div className="mt-4 pt-4 border-t border-slate-100 space-y-2 text-xs">
                  <div>
                    <span className="text-[10px] text-slate-400 uppercase tracking-wider font-semibold block">Source Status</span>
                    <span className="text-slate-700 font-medium">
                      {isActive ? 'Public & Permitted' : 'Blocked (Access Controls)'}
                    </span>
                  </div>

                  <div>
                    <span className="text-[10px] text-slate-400 uppercase tracking-wider font-semibold block">Audit Note</span>
                    <p className="text-slate-600 text-xs mt-0.5 leading-relaxed">
                      {s.StatusReason || 'Standard adapter integration.'}
                    </p>
                  </div>
                </div>
              </div>

              {isBlocked && (
                <div className="mt-4 p-3 bg-amber-50 rounded-xl border border-amber-200 text-[11px] text-amber-800 flex items-start gap-2">
                  <ShieldAlert className="w-4 h-4 text-amber-600 shrink-0 mt-0.5" />
                  <p>
                    Ethical Access Rule: Automated bypass of encrypted sessions or anti-bot challenges is strictly prohibited. Integration awaits authorized API feed.
                  </p>
                </div>
              )}
            </div>
          );
        })}
      </div>

      {/* Collector Execution History */}
      <div className="bg-white rounded-3xl border border-slate-200/80 p-6 sm:p-8 shadow-sm space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-lg font-bold text-slate-900 flex items-center gap-2">
              <Database className="w-5 h-5 text-emerald-600" />
              <span>Recent Collector Runs</span>
            </h3>
            <p className="text-xs text-slate-500">
              Audit logs from GitHub Actions and local diagnostic runs.
            </p>
          </div>
        </div>

        {collectorRuns.length === 0 ? (
          <div className="text-center py-8 text-xs text-slate-500 italic bg-slate-50 rounded-xl border border-slate-100">
            No historical run logs loaded yet. Runs executed via GitHub Actions will appear here.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs border-collapse">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50 text-slate-600 uppercase tracking-wider font-semibold">
                  <th className="py-2.5 px-4">Run Status</th>
                  <th className="py-2.5 px-4">Started At</th>
                  <th className="py-2.5 px-4">Items Checked</th>
                  <th className="py-2.5 px-4">Items Updated</th>
                  <th className="py-2.5 px-4">Diagnostics / Error</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {collectorRuns.map(run => (
                  <tr key={run.Id} className="hover:bg-slate-50/80">
                    <td className="py-2.5 px-4">
                      <StatusBadge status={run.Status} />
                    </td>
                    <td className="py-2.5 px-4 text-slate-600 font-mono">
                      {new Date(run.StartedAt).toLocaleString()}
                    </td>
                    <td className="py-2.5 px-4 font-semibold text-slate-800">
                      {run.ProductsChecked}
                    </td>
                    <td className="py-2.5 px-4 font-bold text-emerald-700">
                      {run.ProductsUpdated}
                    </td>
                    <td className="py-2.5 px-4 text-slate-500 max-w-xs truncate" title={run.ErrorMessage || ''}>
                      {run.ErrorMessage || <span className="text-emerald-600 font-medium">All items validated successfully</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};
