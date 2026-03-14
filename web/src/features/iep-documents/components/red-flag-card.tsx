import { AlertTriangle, AlertOctagon } from 'lucide-react';
import type { RedFlag } from '@/types/api';

interface RedFlagCardProps {
  redFlag: RedFlag;
}

export function RedFlagCard({ redFlag }: RedFlagCardProps) {
  const isRed = redFlag.severity === 'red';

  return (
    <div
      className={`rounded-card border p-4 ${
        isRed
          ? 'bg-red-50 border-red-200'
          : 'bg-brand-amber-50 border-brand-amber-100'
      }`}
    >
      <div className="flex items-start gap-2">
        {isRed ? (
          <AlertOctagon className="w-5 h-5 text-brand-red shrink-0 mt-0.5" strokeWidth={1.8} aria-hidden="true" />
        ) : (
          <AlertTriangle className="w-5 h-5 text-brand-amber-500 shrink-0 mt-0.5" strokeWidth={1.8} aria-hidden="true" />
        )}
        <div className="flex-1">
          <h4 className={`text-[13px] font-medium ${isRed ? 'text-brand-red' : 'text-brand-amber-500'}`}>
            {redFlag.title}
          </h4>
          <p className="text-sm text-brand-slate-600 mt-1">{redFlag.description}</p>
          {redFlag.legalBasis && (
            <p className="text-[11px] text-brand-slate-400 mt-2 italic">
              Legal basis: {redFlag.legalBasis}
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
