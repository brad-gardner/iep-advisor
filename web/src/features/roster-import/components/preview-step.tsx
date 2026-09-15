import { useMemo, useState } from 'react';
import { Download } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Notice } from '@/components/ui/notice';
import { Pagination } from '@/components/ui/pagination';
import { Table, type TableColumn } from '@/components/ui/table';
import { apiErrorMessage } from '@/lib/api-error';
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
  // The page focuses the step heading on each transition.
  headingRef?: React.Ref<HTMLHeadingElement>;
}

// A workbook can carry up to 5,000 rows — far above the Table's client-sort
// ceiling — so the preview is paged client-side in file order (no sort) and
// defaults to the rows that need attention.
const PREVIEW_PAGE_SIZE = 100;

const OUTCOME_BADGE: Record<ImportRowOutcome, 'success' | 'info' | 'neutral' | 'error'> = {
  New: 'success',
  Updated: 'info',
  Unchanged: 'neutral',
  Error: 'error',
};

const columns: TableColumn<ImportRow>[] = [
  { key: 'row', header: 'Row', align: 'right', cell: (r) => r.rowNumber, className: 'w-16' },
  {
    key: 'outcome',
    header: 'Outcome',
    cell: (r) => <Badge variant={OUTCOME_BADGE[r.outcome]}>{r.outcome}</Badge>,
  },
  { key: 'key', header: 'Key', cell: (r) => r.key },
  { key: 'name', header: 'Name', cell: (r) => r.displayName || '—' },
  { key: 'message', header: 'Message', cell: (r) => r.message || '—' },
  {
    key: 'changes',
    header: 'Changes',
    hideBelow: 'md',
    cell: (r) => (r.changes.length > 0 ? r.changes.join('; ') : '—'),
  },
];

// Step 3: what the commit would do, row by row. Commit is confirmed in a
// dialog; with error rows present only the valid rows are imported.
export function PreviewStep({
  preview,
  onCommitted,
  onStartOver,
  onConfirmingChange,
  headingRef,
}: PreviewStepProps) {
  const { counts } = preview;
  const hasErrors = counts.error > 0;
  const validCount = counts.total - counts.error;
  const nothingToImport = validCount === 0;

  const [isConfirming, setIsConfirming] = useState(false);
  const [isCommitting, setIsCommitting] = useState(false);
  const [commitError, setCommitError] = useState<string | null>(null);
  const [isDownloadingErrors, setIsDownloadingErrors] = useState(false);
  const [downloadError, setDownloadError] = useState<string | null>(null);
  const [errorsOnly, setErrorsOnly] = useState(hasErrors);
  const [page, setPage] = useState(1);

  const visibleRows = useMemo(
    () => (errorsOnly ? preview.rows.filter((r) => r.outcome === 'Error') : preview.rows),
    [preview.rows, errorsOnly]
  );
  const pageRows = useMemo(
    () => visibleRows.slice((page - 1) * PREVIEW_PAGE_SIZE, page * PREVIEW_PAGE_SIZE),
    [visibleRows, page]
  );

  const toggleErrorsOnly = (next: boolean) => {
    setErrorsOnly(next);
    setPage(1);
  };

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
    } catch (err) {
      // e.g. "Fix the errors or choose to import valid rows only." (400).
      setCommitError(apiErrorMessage(err, 'The import could not be committed'));
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
            <h2
              ref={headingRef}
              tabIndex={-1}
              className="font-serif text-lg text-brand-slate-800 focus:outline-none"
            >
              Preview
            </h2>
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
          <div className="mt-4" role="alert">
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

      <div className="flex flex-wrap items-center justify-between gap-3">
        <label className="inline-flex items-center gap-2 text-sm text-brand-slate-600">
          <input
            type="checkbox"
            checked={errorsOnly}
            onChange={(e) => toggleErrorsOnly(e.target.checked)}
            className="h-4 w-4 rounded border-brand-slate-300 text-brand-teal-500 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-400"
            data-testid="import-preview-errors-only"
          />
          Errors only
        </label>
        <p className="text-xs text-brand-slate-500" data-testid="import-preview-row-count">
          {errorsOnly
            ? `${visibleRows.length} error ${visibleRows.length === 1 ? 'row' : 'rows'}`
            : `${visibleRows.length} ${visibleRows.length === 1 ? 'row' : 'rows'}`}
        </p>
      </div>

      <Table
        label="Preview rows"
        data-testid="import-preview-rows"
        columns={columns}
        rows={pageRows}
        rowKey={(r) => r.rowNumber}
        empty={
          <p className="text-center text-sm text-brand-slate-400" data-testid="import-preview-no-rows">
            {errorsOnly ? 'No rows have errors.' : 'The workbook has no data rows.'}
          </p>
        }
      />

      <Pagination
        label="Preview rows pagination"
        page={page}
        pageSize={PREVIEW_PAGE_SIZE}
        total={visibleRows.length}
        onPageChange={setPage}
        data-testid="import-preview-pagination"
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
