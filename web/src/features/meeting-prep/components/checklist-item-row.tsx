import { useState } from 'react';
import { ChevronDown, ChevronUp, Scale } from 'lucide-react';
import type { ChecklistItem } from '@/types/api';

interface ChecklistItemRowProps {
  item: ChecklistItem;
  index: number;
  onCheck: (index: number, isChecked: boolean) => void;
}

export function ChecklistItemRow({ item, index, onCheck }: ChecklistItemRowProps) {
  const [expanded, setExpanded] = useState(false);
  const hasDetails = item.context || item.legalBasis;

  return (
    <div className="border-[0.5px] border-brand-slate-200 rounded-card">
      <div className="flex items-start gap-3 p-3">
        <button
          onClick={() => onCheck(index, !item.isChecked)}
          className={`mt-0.5 flex-shrink-0 w-5 h-5 rounded border-[1.5px] flex items-center justify-center transition-colors ${
            item.isChecked
              ? 'bg-brand-teal-500 border-brand-teal-500'
              : 'border-brand-slate-300 hover:border-brand-teal-400'
          }`}
          aria-label={item.isChecked ? 'Uncheck item' : 'Check item'}
        >
          {item.isChecked && (
            <svg className="w-3 h-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
            </svg>
          )}
        </button>

        <div className="flex-1 min-w-0">
          <p
            className={`text-sm leading-relaxed ${
              item.isChecked
                ? 'line-through text-brand-slate-400'
                : 'text-brand-slate-700'
            }`}
          >
            {item.text}
          </p>
        </div>

        {hasDetails && (
          <button
            onClick={() => setExpanded(!expanded)}
            className="flex-shrink-0 p-1 rounded hover:bg-brand-slate-100 text-brand-slate-400 hover:text-brand-slate-600 transition-colors"
            aria-label={expanded ? 'Collapse details' : 'Expand details'}
          >
            {expanded ? (
              <ChevronUp className="w-4 h-4" strokeWidth={1.8} aria-hidden="true" />
            ) : (
              <ChevronDown className="w-4 h-4" strokeWidth={1.8} aria-hidden="true" />
            )}
          </button>
        )}
      </div>

      {expanded && hasDetails && (
        <div className="px-3 pb-3 pt-0 ml-8 space-y-2">
          {item.context && (
            <p className="text-[13px] text-brand-slate-500 leading-relaxed">
              {item.context}
            </p>
          )}
          {item.legalBasis && (
            <div className="flex items-start gap-1.5">
              <Scale className="w-3.5 h-3.5 mt-0.5 text-brand-amber-500 flex-shrink-0" strokeWidth={1.8} aria-hidden="true" />
              <p className="text-[13px] text-brand-amber-600 leading-relaxed">
                {item.legalBasis}
              </p>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
