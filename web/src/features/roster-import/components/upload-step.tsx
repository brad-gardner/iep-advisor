import { useId, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { previewImport } from '../api/import-api';
import type { ImportKind, ImportPreview } from '../types';
import { formatFileSize, validateImportFile } from '../lib/import-file';

interface UploadStepProps {
  kind: ImportKind;
  onPreviewed: (preview: ImportPreview) => void;
  onBack: () => void;
}

// Step 2: pick a workbook and send it for preview. Nothing is written until
// the commit step.
export function UploadStep({ kind, onPreviewed, onBack }: UploadStepProps) {
  const inputId = useId();
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isPreviewing, setIsPreviewing] = useState(false);

  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const next = event.target.files?.[0] ?? null;
    setFile(next);
    setError(next ? validateImportFile(next) : null);
  };

  const handlePreview = async () => {
    if (!file) {
      setError('Choose a workbook to upload');
      return;
    }
    const validation = validateImportFile(file);
    if (validation) {
      setError(validation);
      return;
    }
    setIsPreviewing(true);
    setError(null);
    try {
      const response = await previewImport(kind, file);
      if (response.success && response.data) {
        onPreviewed(response.data);
      } else {
        setError(response.message || 'The workbook could not be read');
      }
    } catch {
      setError('An error occurred uploading the workbook');
    } finally {
      setIsPreviewing(false);
    }
  };

  return (
    <Card data-testid="import-upload-step">
      <div className="space-y-4">
        <div>
          <h2 className="font-serif text-lg text-brand-slate-800">Upload your workbook</h2>
          <p className="mt-1 text-sm text-brand-slate-600">
            .xlsx only, up to 5 MB and 5,000 rows. Formulas and macros are never run.
          </p>
        </div>

        {error && <Notice variant="error" title={error} data-testid="import-upload-error" />}

        <div>
          <label
            htmlFor={inputId}
            className="mb-1 block text-[13px] font-medium text-brand-slate-600"
          >
            Workbook (.xlsx)
          </label>
          <input
            id={inputId}
            type="file"
            accept=".xlsx"
            onChange={handleFileChange}
            data-testid="import-file-input"
            className="block w-full rounded-input border border-brand-slate-200 bg-white px-3 py-2 text-sm text-brand-slate-800 file:mr-3 file:rounded-button file:border-0 file:bg-brand-teal-50 file:px-3 file:py-1 file:text-xs file:font-medium file:text-brand-teal-600 focus:border-brand-teal-400 focus:outline-none focus:ring-[3px] focus:ring-brand-teal-50"
          />
          {file && (
            <p className="mt-1 text-xs text-brand-slate-500" data-testid="import-file-summary">
              {file.name} · {formatFileSize(file.size)}
            </p>
          )}
        </div>

        <div className="flex flex-wrap gap-2">
          <Button variant="ghost" onClick={onBack} disabled={isPreviewing} data-testid="import-upload-back">
            Back
          </Button>
          <Button
            onClick={handlePreview}
            loading={isPreviewing}
            disabled={!file || error !== null}
            data-testid="import-upload-preview"
          >
            Preview changes
          </Button>
        </div>
      </div>
    </Card>
  );
}
