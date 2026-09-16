import { ObligationStatusChip } from '@/features/obligations/components/obligation-status-chip';
import { OBLIGATION_KIND_LABELS } from '@/features/obligations/types';
import type { ObligationDto } from '@/features/obligations/types';
import { formatDate } from '@/lib/format-date';
import { EmptyHint } from './empty-hint';
import { HomeSection } from './home-section';
import { WorkItemRow } from './work-item-row';

/** Due-soon/overdue procedural deadlines where the viewer is lead (staff
 * variants only — admins get the board instead). Unknown dates render via the
 * shared `ObligationStatusChip`'s "Unknown" state, never as healthy. */
export function DueSoonSection({ obligations }: { obligations: ObligationDto[] }) {
  return (
    <HomeSection title="Due soon / overdue" data-testid="home-due-soon">
      {obligations.length === 0 ? (
        <EmptyHint data-testid="home-due-soon-empty">
          Nothing due soon or overdue on your caseload.
        </EmptyHint>
      ) : (
        <ul className="divide-y divide-brand-slate-100">
          {obligations.map((o) => (
            <WorkItemRow
              key={`${o.schoolStudentId}-${o.kind}`}
              title={o.studentName}
              subtitle={`${OBLIGATION_KIND_LABELS[o.kind]} · ${formatDate(o.dueDate)}`}
              href={`/educator/students/${o.schoolStudentId}`}
              data-testid={`home-due-soon-${o.schoolStudentId}-${o.kind}`}
              meta={<ObligationStatusChip status={o.status} />}
            />
          ))}
        </ul>
      )}
    </HomeSection>
  );
}
