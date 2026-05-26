import { Link } from 'react-router-dom';
import { ChevronRight } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import {
  DOCUMENT_STATE_LABELS,
  EVALUATION_TYPE_LABELS,
  type EtrDocumentListItem,
} from '../types';

interface EtrListRowProps {
  etr: EtrDocumentListItem;
}

const STATUS_VARIANTS: Record<string, 'neutral' | 'warning' | 'success' | 'error'> = {
  created: 'neutral',
  uploaded: 'neutral',
  processing: 'warning',
  parsed: 'success',
  error: 'error',
};

function formatDate(value: string | null): string {
  if (!value) return '';
  return new Date(value).toLocaleDateString();
}

export function EtrListRow({ etr }: EtrListRowProps) {
  const evalLabel = etr.evaluationType
    ? EVALUATION_TYPE_LABELS[etr.evaluationType] || etr.evaluationType
    : 'Evaluation';
  const evalDate = etr.evaluationDate
    ? formatDate(etr.evaluationDate)
    : `Uploaded ${formatDate(etr.uploadDate)}`;

  return (
    <Link
      to={`/children/${etr.childProfileId}/etrs/${etr.id}`}
      data-testid="etr-list-row"
      className="flex items-center justify-between gap-3 px-3 py-2.5 rounded-button hover:bg-brand-slate-50 transition-colors"
    >
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-[13px] font-medium text-brand-slate-800 truncate">
            {evalDate}
          </span>
          <Badge variant="neutral">{evalLabel}</Badge>
          {etr.documentState && (
            <Badge variant={etr.documentState === 'final' ? 'success' : 'neutral'}>
              {DOCUMENT_STATE_LABELS[etr.documentState] || etr.documentState}
            </Badge>
          )}
          <Badge variant={STATUS_VARIANTS[etr.status] || 'neutral'}>{etr.status}</Badge>
        </div>
        {etr.fileName && (
          <p className="mt-0.5 text-[11px] text-brand-slate-400 truncate">{etr.fileName}</p>
        )}
      </div>
      <ChevronRight
        className="w-4 h-4 text-brand-slate-300 shrink-0"
        strokeWidth={1.8}
        aria-hidden="true"
      />
    </Link>
  );
}
