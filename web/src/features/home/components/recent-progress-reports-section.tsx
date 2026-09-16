import { formatDate } from '@/lib/format-date';
import { EmptyHint } from './empty-hint';
import { HomeSection } from './home-section';
import { WorkItemRow } from './work-item-row';
import type { ParentProgressReportDto } from '../types';

/**
 * Parent "Recent progress reports". The contract's `ParentProgressReportDto`
 * doesn't carry the IEP id the direct viewer route needs
 * (`/children/:childId/ieps/:id/progress-reports/:prId`), so this links to the
 * child's IEPs tab (where progress reports are listed per IEP) rather than the
 * exact viewer — a deliberate deviation given the fixed contract shape.
 */
export function RecentProgressReportsSection({ items }: { items: ParentProgressReportDto[] }) {
  return (
    <HomeSection title="Recent progress reports" data-testid="home-progress-reports">
      {items.length === 0 ? (
        <EmptyHint data-testid="home-progress-reports-empty">
          No progress reports yet.
        </EmptyHint>
      ) : (
        <ul className="divide-y divide-brand-slate-100">
          {items.map((report) => (
            <WorkItemRow
              key={report.id}
              title={report.title}
              subtitle={`${report.childName} · ${formatDate(report.createdAt)}`}
              href={`/children/${report.childId}/ieps`}
              data-testid={`home-progress-reports-${report.id}`}
            />
          ))}
        </ul>
      )}
    </HomeSection>
  );
}
