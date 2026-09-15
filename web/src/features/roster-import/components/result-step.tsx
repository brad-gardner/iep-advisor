import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import type { ImportKind, ImportResult } from '../types';

interface ResultStepProps {
  kind: ImportKind;
  result: ImportResult;
  onImportAnother: () => void;
}

// Step 5: what was written. Skipped = error rows left out of the commit.
export function ResultStep({ kind, result, onImportAnother }: ResultStepProps) {
  const { committed, skipped } = result;
  const written = committed.new + committed.updated;
  return (
    <Card data-testid="import-result-step">
      <div className="space-y-4">
        <Notice variant="success" title="Import complete" data-testid="import-result-notice">
          {written} {written === 1 ? 'row' : 'rows'} written — {committed.new} new,{' '}
          {committed.updated} updated, {committed.unchanged} unchanged
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
