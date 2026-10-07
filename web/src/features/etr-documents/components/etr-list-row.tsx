import { Link } from 'react-router-dom';
import { ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { formatDate } from '@/lib/format-date';
import { documentStatusLabel } from '@/lib/document-status-label';
import { evaluationTypeLabel, documentStateLabel } from '../lib/document-labels';
import type { EtrDocumentListItem } from '../types';

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

export function EtrListRow({ etr }: EtrListRowProps) {
  const { t } = useTranslation(['etr-documents', 'iep-documents']);
  const evalLabel = etr.evaluationType ? evaluationTypeLabel(etr.evaluationType) : t('listRow.evaluationFallback');
  const evalDate = etr.evaluationDate
    ? formatDate(etr.evaluationDate)
    : t('listRow.uploaded', { date: formatDate(etr.uploadDate) });

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
              {documentStateLabel(etr.documentState)}
            </Badge>
          )}
          <Badge variant={STATUS_VARIANTS[etr.status] || 'neutral'}>{documentStatusLabel(etr.status)}</Badge>
        </div>
        {etr.fileName && (
          <p className="mt-0.5 text-[11px] text-brand-slate-500 truncate">{etr.fileName}</p>
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
