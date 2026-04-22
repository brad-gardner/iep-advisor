import { useMemo, useState } from 'react';
import { ChevronDown, ChevronUp } from 'lucide-react';
import { Card } from '@/components/ui/card';
import type { EtrSection } from '../types';
import { formatSectionTypeLabel } from '../lib/section-type-labels';

interface EtrSectionCardProps {
  section: EtrSection;
  defaultOpen?: boolean;
}

type ParsedShape =
  | string
  | number
  | boolean
  | null
  | ParsedShape[]
  | { [key: string]: ParsedShape };

function tryParseJson(value: string | null): ParsedShape | undefined {
  if (!value) return undefined;
  try {
    return JSON.parse(value) as ParsedShape;
  } catch {
    return undefined;
  }
}

function humanizeKey(key: string): string {
  return key
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/\b\w/g, (c) => c.toUpperCase());
}

export function EtrSectionCard({ section, defaultOpen = false }: EtrSectionCardProps) {
  const [isOpen, setIsOpen] = useState(defaultOpen);
  const parsed = useMemo(() => tryParseJson(section.parsedContent), [section.parsedContent]);
  const hasStructured = parsed !== undefined && parsed !== null;
  const label = formatSectionTypeLabel(section.sectionType);

  return (
    <Card className="p-0" data-testid="etr-section-card">
      <button
        type="button"
        onClick={() => setIsOpen((v) => !v)}
        className="w-full flex items-center justify-between gap-3 px-4 py-3 text-left hover:bg-brand-slate-50 transition-colors rounded-card"
        aria-expanded={isOpen}
        data-testid="etr-section-toggle"
      >
        <div className="min-w-0">
          <p className="text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold">
            {section.sectionType}
          </p>
          <p className="font-serif text-[17px] font-semibold text-brand-slate-800 truncate">
            {label}
          </p>
        </div>
        {isOpen ? (
          <ChevronUp className="w-4 h-4 text-brand-slate-400 shrink-0" strokeWidth={1.8} aria-hidden="true" />
        ) : (
          <ChevronDown className="w-4 h-4 text-brand-slate-400 shrink-0" strokeWidth={1.8} aria-hidden="true" />
        )}
      </button>

      {isOpen && (
        <div className="px-4 pb-4 border-t border-brand-slate-100">
          {hasStructured ? (
            <div className="mt-3" data-testid="etr-section-parsed">
              <ParsedContentView value={parsed} />
            </div>
          ) : section.rawText ? (
            <pre
              className="mt-3 text-sm text-brand-slate-600 whitespace-pre-wrap font-sans leading-relaxed"
              data-testid="etr-section-raw"
            >
              {section.rawText}
            </pre>
          ) : (
            <p className="mt-3 text-sm text-brand-slate-400 italic">
              No content captured for this section.
            </p>
          )}
        </div>
      )}
    </Card>
  );
}

function ParsedContentView({ value }: { value: ParsedShape }) {
  if (value === null || value === undefined) return null;

  if (typeof value === 'string') {
    return (
      <p className="text-sm text-brand-slate-600 whitespace-pre-wrap leading-relaxed">{value}</p>
    );
  }

  if (typeof value === 'number' || typeof value === 'boolean') {
    return <p className="text-sm text-brand-slate-600">{String(value)}</p>;
  }

  if (Array.isArray(value)) {
    if (value.length === 0) {
      return <p className="text-sm text-brand-slate-400 italic">None</p>;
    }
    return (
      <ul className="space-y-2">
        {value.map((item, idx) => (
          <li
            key={idx}
            className="text-sm text-brand-slate-600 bg-brand-slate-50 rounded-card p-3 border-[0.5px] border-brand-slate-200"
          >
            <ParsedContentView value={item} />
          </li>
        ))}
      </ul>
    );
  }

  // object
  const entries = Object.entries(value);
  if (entries.length === 0) {
    return <p className="text-sm text-brand-slate-400 italic">No details</p>;
  }
  return (
    <dl className="space-y-3">
      {entries.map(([k, v]) => (
        <div key={k}>
          <dt className="text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold">
            {humanizeKey(k)}
          </dt>
          <dd className="mt-1">
            <ParsedContentView value={v} />
          </dd>
        </div>
      ))}
    </dl>
  );
}
