import { useTranslation } from 'react-i18next';
import { formatDate } from '@/lib/format-date';
import { ListSection } from './list-section';
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
  const { t } = useTranslation('home');
  return (
    <ListSection
      title={t('progressReports.title')}
      data-testid="home-progress-reports"
      items={items}
      emptyHint={t('progressReports.emptyHint')}
      itemKey={(report) => report.id}
      renderRow={(report) => (
        <WorkItemRow
          title={report.title ?? t('progressReports.untitled')}
          subtitle={`${report.childName} · ${formatDate(report.createdAt)}`}
          href={`/children/${report.childId}/ieps`}
          data-testid={`home-progress-reports-${report.id}`}
        />
      )}
    />
  );
}
