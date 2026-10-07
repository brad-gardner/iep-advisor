import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Markdown } from '@/components/ui/markdown';
import { formatDate } from '@/lib/format-date';
import { draftResponseKindLabel } from '@/features/shared-drafts/lib/draft-response-kind-label';
import type { DraftResponseDto } from '../types';

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
  const { t } = useTranslation('draft-sharing');
  return (
    <Card className="space-y-2" data-testid={`response-card-${response.id}`}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <Badge variant={response.status === 'Resolved' ? 'success' : 'warning'}>
            {draftResponseKindLabel(response.kind)}
          </Badge>
          <span className="text-sm font-medium text-brand-slate-800">{response.parentName}</span>
        </div>
        <span className="text-xs text-brand-slate-500">{formatDate(response.createdAt)}</span>
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

      <Markdown content={response.text} data-testid={`response-card-${response.id}-text`} />

      {response.staffReply && (
        <div className="rounded-card bg-brand-slate-50 p-3">
          <p className="text-xs font-medium text-brand-slate-500">
            {response.resolvedByName ? t('responseCard.repliedBy', { name: response.resolvedByName }) : t('responseCard.replyHeading')}
          </p>
          <Markdown
            content={response.staffReply}
            className="mt-1"
            data-testid={`response-card-${response.id}-staff-reply`}
          />
        </div>
      )}

      {onResolve && (
        <div>
          <Button size="sm" variant="secondary" onClick={onResolve} data-testid={`response-resolve-open-${response.id}`}>
            {t('responseCard.replyResolveButton')}
          </Button>
        </div>
      )}
    </Card>
  );
}
