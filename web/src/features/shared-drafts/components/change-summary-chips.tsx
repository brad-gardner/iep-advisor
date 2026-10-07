import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import type { ChangeSummaryDto } from '../types';

interface ChangeSummaryChipsProps {
  summary: ChangeSummaryDto;
  'data-testid'?: string;
}

/**
 * Compact "what changed" chips (added/changed/removed row + field counts)
 * plus the server's plain-language summary. Shared by the staff shared-draft
 * banner/converge panel and the parent review page — same `ChangeSummaryDto`
 * either way (a semantic diff keyed by `_rowId`).
 */
export function ChangeSummaryChips({ summary, 'data-testid': testId }: ChangeSummaryChipsProps) {
  const { t } = useTranslation('shared-drafts');
  const { addedRows, changedRows, removedRows, changedFields } = summary;
  const hasAny =
    addedRows.length > 0 || changedRows.length > 0 || removedRows.length > 0 || changedFields.length > 0;

  if (!hasAny) {
    return (
      <p className="text-sm text-brand-slate-500" data-testid={testId}>
        {summary.summaryText || t('changeSummary.noChanges')}
      </p>
    );
  }

  return (
    <div className="space-y-2" data-testid={testId}>
      <div className="flex flex-wrap gap-2">
        {addedRows.length > 0 && (
          <Badge variant="success" data-testid={testId ? `${testId}-added` : undefined}>
            {t('changeSummary.added', { count: addedRows.length })}
          </Badge>
        )}
        {changedRows.length > 0 && (
          <Badge variant="warning" data-testid={testId ? `${testId}-changed` : undefined}>
            {t('changeSummary.changed', { count: changedRows.length })}
          </Badge>
        )}
        {removedRows.length > 0 && (
          <Badge variant="error" data-testid={testId ? `${testId}-removed` : undefined}>
            {t('changeSummary.removed', { count: removedRows.length })}
          </Badge>
        )}
        {changedFields.length > 0 && (
          <Badge variant="info" data-testid={testId ? `${testId}-fields` : undefined}>
            {t('changeSummary.fieldsUpdated', { count: changedFields.length })}
          </Badge>
        )}
      </div>
      {summary.summaryText && <p className="text-sm text-brand-slate-600">{summary.summaryText}</p>}
    </div>
  );
}
