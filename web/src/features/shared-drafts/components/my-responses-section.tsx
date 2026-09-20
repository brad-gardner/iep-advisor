import { Badge } from '@/components/ui/badge';
import { Card } from '@/components/ui/card';
import { Markdown } from '@/components/ui/markdown';
import { formatDate } from '@/lib/format-date';
import { DRAFT_RESPONSE_KIND_LABELS, type DraftResponseDto } from '../types';

interface MyResponsesSectionProps {
  responses: DraftResponseDto[];
}

/** Every response this parent has sent on the revision, with the staff reply
 *  once one has resolved it. Renders nothing when the parent hasn't responded. */
export function MyResponsesSection({ responses }: MyResponsesSectionProps) {
  if (responses.length === 0) return null;
  const sorted = [...responses].sort((a, b) => b.createdAt.localeCompare(a.createdAt));

  return (
    <section id="my-responses" data-testid="my-responses-section" className="space-y-3">
      <h2 className="font-serif text-lg text-brand-slate-800">My responses</h2>
      <ul className="space-y-3">
        {sorted.map((response) => (
          <li key={response.id}>
            <Card className="space-y-2" data-testid={`my-response-${response.id}`}>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="flex items-center gap-2">
                  <Badge variant={response.status === 'Resolved' ? 'success' : 'info'}>
                    {DRAFT_RESPONSE_KIND_LABELS[response.kind]}
                  </Badge>
                  {response.targetLabel && (
                    <span className="text-sm text-brand-slate-500">{response.targetLabel}</span>
                  )}
                </div>
                <span className="text-xs text-brand-slate-500">{formatDate(response.createdAt)}</span>
              </div>
              <Markdown content={response.text} data-testid={`my-response-${response.id}-text`} />
              {response.staffReply && (
                <div className="rounded-card bg-brand-slate-50 p-3">
                  <p className="text-xs font-medium text-brand-slate-500">
                    {response.resolvedByName ? `${response.resolvedByName} replied` : 'School reply'}
                  </p>
                  <Markdown
                    content={response.staffReply}
                    className="mt-1"
                    data-testid={`my-response-${response.id}-staff-reply`}
                  />
                </div>
              )}
              {response.status === 'Resolved' && !response.staffReply && (
                <p className="text-xs text-brand-slate-500">Marked resolved in the updated draft.</p>
              )}
            </Card>
          </li>
        ))}
      </ul>
    </section>
  );
}
