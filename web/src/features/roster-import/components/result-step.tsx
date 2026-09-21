import { Link } from 'react-router-dom';
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
          Import complete
        </h2>
        <Notice
          variant="success"
          title={`${written} ${written === 1 ? 'row' : 'rows'} written`}
          data-testid="import-result-notice"
        >
          {committed.new} new, {committed.updated} updated, {committed.unchanged} unchanged
          {skipped > 0 ? `, ${skipped} skipped` : ''}.
        </Notice>
        <div className="flex flex-wrap gap-2">
          <Button variant="secondary" onClick={onImportAnother} data-testid="import-result-another">
            Import another file
          </Button>
          <Link to={kind === 'Staff' ? '/educator/admin/staff' : '/educator/students'}>
            <Button data-testid="import-result-view">
              {kind === 'Staff' ? 'View staff' : 'View students'}
            </Button>
          </Link>
        </div>
      </div>
    </Card>
  );
}
