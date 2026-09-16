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
  const { addedRows, changedRows, removedRows, changedFields } = summary;
  const hasAny =
    addedRows.length > 0 || changedRows.length > 0 || removedRows.length > 0 || changedFields.length > 0;

  if (!hasAny) {
    return (
      <p className="text-sm text-brand-slate-500" data-testid={testId}>
        {summary.summaryText || 'No changes since the last share.'}
      </p>
    );
  }

  return (
    <div className="space-y-2" data-testid={testId}>
      <div className="flex flex-wrap gap-2">
        {addedRows.length > 0 && (
          <Badge variant="success" data-testid={testId ? `${testId}-added` : undefined}>
            {addedRows.length} added
          </Badge>
        )}
        {changedRows.length > 0 && (
          <Badge variant="warning" data-testid={testId ? `${testId}-changed` : undefined}>
            {changedRows.length} changed
          </Badge>
        )}
        {removedRows.length > 0 && (
          <Badge variant="error" data-testid={testId ? `${testId}-removed` : undefined}>
            {removedRows.length} removed
          </Badge>
        )}
        {changedFields.length > 0 && (
          <Badge variant="info" data-testid={testId ? `${testId}-fields` : undefined}>
            {changedFields.length} {changedFields.length === 1 ? 'field' : 'fields'} updated
          </Badge>
        )}
      </div>
      {summary.summaryText && <p className="text-sm text-brand-slate-600">{summary.summaryText}</p>}
    </div>
  );
}
