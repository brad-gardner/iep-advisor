import { useState } from 'react';
import { MessageCircleQuestion, Reply } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { CellValue } from '@/features/document-authoring/components/authored-version-snapshot';
import type { TableColumn } from '@/features/admin/templates/template-config';
import { AskQuestionDrawer } from './ask-question-drawer';
import { ExplainPanel } from './explain-panel';
import { RespondDialog } from './respond-dialog';
import { useDraftReviewContext } from '../hooks/draft-review-context';

interface DraftItemCardProps {
  revisionId: number;
  canRespond: boolean;
  fieldKey: string;
  rowId: string | null;
  label: string;
  otherColumns: { column: TableColumn; value: unknown }[];
  changeState: 'added' | 'changed' | null;
}

/** One goal/service/accommodation row: read-only frozen values plus the three
 *  parent actions (Explain, Ask a question, Respond). Non-primary columns
 *  render generically via the same per-column renderer as the frozen snapshot. */
export function DraftItemCard({
  revisionId,
  canRespond,
  fieldKey,
  rowId,
  label,
  otherColumns,
  changeState,
}: DraftItemCardProps) {
  const ctx = useDraftReviewContext();
  const [askOpen, setAskOpen] = useState(false);
  const [respondOpen, setRespondOpen] = useState(false);

  const myResponseCount =
    ctx?.responses.filter((r) => (r.targetFieldKey ?? null) === fieldKey && (r.targetRowId ?? null) === rowId)
      .length ?? 0;

  return (
    <Card className="space-y-3" data-testid={`draft-item-card-${fieldKey}-${rowId ?? 'field'}`}>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <h3 className="font-serif text-base text-brand-slate-800">{label}</h3>
        {changeState && (
          <Badge variant={changeState === 'added' ? 'success' : 'warning'}>
            {changeState === 'added' ? 'Added' : 'Changed'}
          </Badge>
        )}
      </div>

      {otherColumns.length > 0 && (
        <dl className="grid grid-cols-1 gap-2 text-sm sm:grid-cols-2">
          {otherColumns.map(({ column, value }) => (
            <div key={column.columnKey}>
              <dt className="text-[13px] font-medium text-brand-slate-500">{column.label || 'Detail'}</dt>
              <dd className="text-brand-slate-700"><CellValue column={column} value={value} /></dd>
            </div>
          ))}
        </dl>
      )}

      {myResponseCount > 0 && (
        <p className="text-xs text-brand-slate-400">
          You already responded to this — see "My responses" below.
        </p>
      )}

      <div className="flex flex-wrap items-center gap-2 border-t border-brand-slate-100 pt-3">
        <ExplainPanel target={{ kind: 'item', fieldKey, rowId }} data-testid={`explain-${fieldKey}-${rowId ?? 'field'}`} />
        <Button
          size="sm"
          variant="ghost"
          onClick={() => setAskOpen(true)}
          data-testid={`ask-question-open-${fieldKey}-${rowId ?? 'field'}`}
        >
          <MessageCircleQuestion className="mr-1 h-3.5 w-3.5" aria-hidden="true" />
          Ask a question
        </Button>
        {canRespond && (
          <Button
            size="sm"
            variant="ghost"
            onClick={() => setRespondOpen(true)}
            data-testid={`respond-open-${fieldKey}-${rowId ?? 'field'}`}
          >
            <Reply className="mr-1 h-3.5 w-3.5" aria-hidden="true" />
            Respond
          </Button>
        )}
      </div>

      <AskQuestionDrawer
        open={askOpen}
        onClose={() => setAskOpen(false)}
        revisionId={revisionId}
        targetFieldKey={fieldKey}
        targetRowId={rowId ?? undefined}
        targetLabel={label}
        data-testid={`ask-question-drawer-${fieldKey}-${rowId ?? 'field'}`}
      />
      {canRespond && (
        <RespondDialog
          open={respondOpen}
          onClose={() => setRespondOpen(false)}
          revisionId={revisionId}
          data-testid={`respond-dialog-${fieldKey}-${rowId ?? 'field'}`}
          targetFieldKey={fieldKey}
          targetRowId={rowId ?? undefined}
          targetLabel={label}
        />
      )}
    </Card>
  );
}
