import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { formatDate } from '@/lib/format-date';
import { DRAFT_RESPONSE_KIND_LABELS, type DraftResponseDto } from '../types';

interface ResponseCardProps {
  response: DraftResponseDto;
  /** Present when the response targets a live field on this document. */
  onJump?: () => void;
  /** Present for an Open response — opens the reply/resolve dialog. */
  onResolve?: () => void;
}

/** One family response in the Converge view: who/what/when, a jump-to-field
 *  link when it targets a specific item, the staff reply once resolved. */
export function ResponseCard({ response, onJump, onResolve }: ResponseCardProps) {
  return (
    <Card className="space-y-2" data-testid={`response-card-${response.id}`}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <Badge variant={response.status === 'Resolved' ? 'success' : 'warning'}>
            {DRAFT_RESPONSE_KIND_LABELS[response.kind]}
          </Badge>
          <span className="text-sm font-medium text-brand-slate-800">{response.parentName}</span>
        </div>
        <span className="text-xs text-brand-slate-400">{formatDate(response.createdAt)}</span>
      </div>

      {response.targetLabel && onJump ? (
        <button
          type="button"
          onClick={onJump}
          className="text-left text-sm text-brand-teal-600 underline"
          data-testid={`response-jump-${response.id}`}
        >
          {response.targetLabel}
        </button>
      ) : (
        response.targetLabel && <p className="text-sm text-brand-slate-500">{response.targetLabel}</p>
      )}

      <p className="text-sm text-brand-slate-700">{response.text}</p>

      {response.staffReply && (
        <div className="rounded-card bg-brand-slate-50 p-3">
          <p className="text-xs font-medium text-brand-slate-500">
            {response.resolvedByName ? `${response.resolvedByName} replied` : 'Reply'}
          </p>
          <p className="mt-1 whitespace-pre-wrap text-sm text-brand-slate-700">{response.staffReply}</p>
        </div>
      )}

      {onResolve && (
        <div>
          <Button size="sm" variant="secondary" onClick={onResolve} data-testid={`response-resolve-open-${response.id}`}>
            Reply / resolve
          </Button>
        </div>
      )}
    </Card>
  );
}
