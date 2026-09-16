import { Badge } from '@/components/ui/badge';
import { formatDate } from '@/lib/format-date';
import { EmptyHint } from './empty-hint';
import { HomeSection } from './home-section';
import { WorkItemRow } from './work-item-row';
import type { HomeDraftDto } from '../types';

/** Draft document instances the viewer edited or is on the team for, newest
 * edit first (server-ordered, max 20). Completeness is shown as text so it is
 * never a colour-only signal. */
export function DraftsSection({ drafts }: { drafts: HomeDraftDto[] }) {
  return (
    <HomeSection title="Drafts in progress" data-testid="home-drafts">
      {drafts.length === 0 ? (
        <EmptyHint data-testid="home-drafts-empty">No drafts in progress.</EmptyHint>
      ) : (
        <ul className="divide-y divide-brand-slate-100">
          {drafts.map((d) => (
            <WorkItemRow
              key={d.instanceId}
              title={d.studentName}
              subtitle={`${d.documentTypeDisplayName} · edited ${formatDate(d.lastEditedAt)}`}
              href={`/educator/documents/${d.instanceId}`}
              data-testid={`home-drafts-${d.instanceId}`}
              meta={
                <Badge variant={d.requiredMissing > 0 ? 'warning' : 'success'}>
                  {d.completenessPercent}% complete
                </Badge>
              }
            />
          ))}
        </ul>
      )}
    </HomeSection>
  );
}
