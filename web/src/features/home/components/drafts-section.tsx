import { Badge } from '@/components/ui/badge';
import { formatDate } from '@/lib/format-date';
import { ListSection } from './list-section';
import { WorkItemRow } from './work-item-row';
import type { HomeDraftDto } from '../types';

/** Draft document instances the viewer edited or is on the team for, newest
 * edit first (server-ordered, max 20). Completeness is shown as text so it is
 * never a colour-only signal. */
export function DraftsSection({ drafts }: { drafts: HomeDraftDto[] }) {
  return (
    <ListSection
      title="Drafts in progress"
      data-testid="home-drafts"
      items={drafts}
      emptyHint="No drafts in progress."
      itemKey={(d) => d.instanceId}
      renderRow={(d) => (
        <WorkItemRow
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
      )}
    />
  );
}
