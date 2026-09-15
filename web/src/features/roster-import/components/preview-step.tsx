import { useState } from 'react';
import { Download } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Notice } from '@/components/ui/notice';
import { Table, type TableColumn } from '@/components/ui/table';
import { downloadBlob } from '@/lib/download-file';
import { commitImport, downloadImportErrors } from '../api/import-api';
import type { ImportPreview, ImportResult, ImportRow, ImportRowOutcome } from '../types';
import { ImportCountsBadges } from './import-counts-badges';

interface PreviewStepProps {
  preview: ImportPreview;
  onCommitted: (result: ImportResult) => void;
  onStartOver: () => void;
  // Lets the page move the progress indicator to "Commit" while confirming.
  onConfirmingChange: (confirming: boolean) => void;
}

const OUTCOME_BADGE: Record<ImportRowOutcome, 'success' | 'info' | 'neutral' | 'error'> = {
  New: 'success',
  Updated: 'info',
  Unchanged: 'neutral',
  Error: 'error',
};

const columns: TableColumn<ImportRow>[] = [
  {
    key: 'row',
    header: 'Row',
    align: 'right',
    cell: (r) => r.rowNumber,
    sortValue: (r) => r.rowNumber,
    className: 'w-16',
  },
  {
    key: 'outcome',
    header: 'Outcome',
    cell: (r) => <Badge variant={OUTCOME_BADGE[r.outcome]}>{r.outcome}</Badge>,
    sortValue: (r) => r.outcome,
  },
  { key: 'key', header: 'Key', cell: (r) => r.key, sortValue: (r) => r.key },
  {
    key: 'name',
    header: 'Name',
    cell: (r) => r.displayName || '—',
    sortValue: (r) => r.displayName,
  },
  {
    key: 'message',
    header: 'Message',
    cell: (r) => r.message || '—',
  },
  {
    key: 'changes',
    header: 'Changes',
    hideBelow: 'md',
    cell: (r) => (r.changes.length > 0 ? r.changes.join('; ') : '—'),
  },
];

// Step 3: what the commit would do, row by row. Commit is confirmed in a
// dialog; with error rows present only the valid rows are imported.
export function PreviewStep({ preview, onCommitted, onStartOver, onConfirmingChange }: PreviewStepProps) {
  const [isConfirming, setIsConfirming] = useState(false);
  const [isCommitting, setIsCommitting] = useState(false);
  const [commitError, setCommitError] = useState<string | null>(null);
  const [isDownloadingErrors, setIsDownloadingErrors] = useState(false);
  const [downloadError, setDownloadError] = useState<string | null>(null);

  const { counts } = preview;
  const hasErrors = counts.error > 0;
  const validCount = counts.total - counts.error;
  const nothingToImport = validCount === 0;

  const openConfirm = () => {
    setCommitError(null);
    setIsConfirming(true);
    onConfirmingChange(true);
  };

  const closeConfirm = () => {
    setIsConfirming(false);
    onConfirmingChange(false);
  };

  const handleCommit = async () => {
    setIsCommitting(true);
    setCommitError(null);
    try {
      const response = await commitImport(preview.batchId, { commitValid: hasErrors });
      if (response.success && response.data) {
        closeConfirm();
        onCommitted(response.data);
      } else {
        setCommitError(response.message || 'The import could not be committed');
      }
    } catch {
      setCommitError('An error occurred committing the import');
    } finally {
      setIsCommitting(false);
    }
  };

  const handleDownloadErrors = async () => {
    setIsDownloadingErrors(true);
    setDownloadError(null);
    try {
      const { blob, fileName } = await downloadImportErrors(preview.batchId);
      downloadBlob(blob, fileName);
    } catch {
      setDownloadError('Could not download the error report');
    } finally {
      setIsDownloadingErrors(false);
    }
  };

  return (
    <div className="space-y-4" data-testid="import-preview-step">
      <Card>
        <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="space-y-2">
            <h2 className="font-serif text-lg text-brand-slate-800">Preview</h2>
            <p className="text-sm text-brand-slate-600" data-testid="import-preview-file">
              {preview.fileName} · {counts.total} {counts.total === 1 ? 'row' : 'rows'}
            </p>
            <ImportCountsBadges counts={counts} />
          </div>
          {hasErrors && (
            <Button
              variant="secondary"
              size="sm"
              loading={isDownloadingErrors}
              onClick={handleDownloadErrors}
              data-testid="import-download-errors"
            >
              <Download className="mr-1.5 h-4 w-4" strokeWidth={1.8} aria-hidden="true" />
              Download error report
            </Button>
          )}
        </div>

        {downloadError && (
          <div className="mt-4">
            <Notice variant="error" title={downloadError} />
          </div>
        )}

        {hasErrors && (
          <div className="mt-4">
            <Notice
              variant="warning"
              title={`${counts.error} ${counts.error === 1 ? 'row has' : 'rows have'} errors`}
            >
              Error rows are skipped. Fix them in the workbook and upload again, or import the{' '}
              {validCount} valid {validCount === 1 ? 'row' : 'rows'} now.
            </Notice>
          </div>
        )}

        <div className="mt-4 flex flex-wrap gap-2">
          <Button variant="ghost" onClick={onStartOver} data-testid="import-preview-start-over">
            Start over
          </Button>
          <Button onClick={openConfirm} disabled={nothingToImport} data-testid="import-preview-commit">
            {hasErrors
              ? `Import ${validCount} valid ${validCount === 1 ? 'row' : 'rows'} only`
              : `Import ${validCount} ${validCount === 1 ? 'row' : 'rows'}`}
          </Button>
        </div>
      </Card>

      <Table
        label="Preview rows"
        data-testid="import-preview-rows"
        columns={columns}
        rows={preview.rows}
        rowKey={(r) => r.rowNumber}
        defaultSort={{ key: 'row', direction: 'asc' }}
      />

      <ConfirmDialog
        open={isConfirming}
        title="Commit import"
        message={
          hasErrors
            ? `Import ${validCount} valid ${validCount === 1 ? 'row' : 'rows'} and skip ${counts.error} with errors? This writes ${counts.new} new and updates ${counts.updated}.`
            : `Import ${validCount} ${validCount === 1 ? 'row' : 'rows'}? This writes ${counts.new} new and updates ${counts.updated}.`
        }
        confirmLabel={hasErrors ? 'Import valid rows only' : 'Import rows'}
        confirmVariant="primary"
        loading={isCommitting}
        error={commitError}
        onConfirm={handleCommit}
        onCancel={closeConfirm}
        data-testid="import-commit-dialog"
      />
    </div>
  );
}
