import { ObligationStatusChip } from '@/features/obligations/components/obligation-status-chip';
import { OBLIGATION_KIND_LABELS } from '@/features/obligations/types';
import type { ObligationDto } from '@/features/obligations/types';
import { formatDate } from '@/lib/format-date';
import { ListSection } from './list-section';
import { WorkItemRow } from './work-item-row';

/** Due-soon/overdue procedural deadlines where the viewer is lead (staff
 * variants only — admins get the board instead). Unknown dates render via the
 * shared `ObligationStatusChip`'s "Unknown" state, never as healthy. */
export function DueSoonSection({ obligations }: { obligations: ObligationDto[] }) {
  return (
    <ListSection
      title="Due soon / overdue"
      data-testid="home-due-soon"
      items={obligations}
      emptyHint="Nothing due soon or overdue on your caseload."
      itemKey={(o) => `${o.schoolStudentId}-${o.kind}`}
      renderRow={(o) => (
        <WorkItemRow
          title={o.studentName}
          subtitle={`${OBLIGATION_KIND_LABELS[o.kind]} · ${formatDate(o.dueDate)}`}
          href={`/educator/students/${o.schoolStudentId}`}
          data-testid={`home-due-soon-${o.schoolStudentId}-${o.kind}`}
          meta={<ObligationStatusChip status={o.status} />}
        />
      )}
    />
  );
}
