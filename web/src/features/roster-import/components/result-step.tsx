import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import type { ImportKind, ImportResult } from '../types';

interface ResultStepProps {
  kind: ImportKind;
  result: ImportResult;
  onImportAnother: () => void;
  // The page focuses the step heading on each transition.
  headingRef?: React.Ref<HTMLHeadingElement>;
}

// Step 5: what was written. Skipped = error rows left out of the commit.
export function ResultStep({ kind, result, onImportAnother, headingRef }: ResultStepProps) {
  const { t } = useTranslation('roster-import');
  const { committed, skipped } = result;
  const written = committed.new + committed.updated;
  return (
    <Card data-testid="import-result-step">
      <div className="space-y-4">
        <h2
          ref={headingRef}
          tabIndex={-1}
          className="font-serif text-lg text-brand-slate-800 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-500"
        >
          {t('resultStep.heading')}
        </h2>
        <Notice
          variant="success"
          title={t('resultStep.rowsWritten', { count: written })}
          data-testid="import-result-notice"
        >
          {skipped > 0
            ? t('resultStep.summarySkipped', {
                newCount: committed.new,
                updatedCount: committed.updated,
                unchangedCount: committed.unchanged,
                skippedCount: skipped,
              })
            : t('resultStep.summary', {
                newCount: committed.new,
                updatedCount: committed.updated,
                unchangedCount: committed.unchanged,
              })}
        </Notice>
        <div className="flex flex-wrap gap-2">
          <Button variant="secondary" onClick={onImportAnother} data-testid="import-result-another">
            {t('resultStep.importAnother')}
          </Button>
          <Link to={kind === 'Staff' ? '/educator/admin/staff' : '/educator/students'}>
            <Button data-testid="import-result-view">
              {kind === 'Staff' ? t('resultStep.viewStaff') : t('resultStep.viewStudents')}
            </Button>
          </Link>
        </div>
      </div>
    </Card>
  );
}
