import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Upload } from 'lucide-react';
import i18n from '@/lib/i18n';
import { attachFile } from '../api/iep-documents-api';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { useToast } from '@/components/ui/toast';

interface IepUploadProps {
  iepId: number;
  onUploaded: () => void;
}

export function IepUpload({ iepId, onUploaded }: IepUploadProps) {
  const { t } = useTranslation('iep-documents');
  const { show: showToast } = useToast();
  const [isDragging, setIsDragging] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // `i18n.t` directly (not the hook's `t`): this callback is a `useCallback`
  // dependency of nothing re-run on language change, but it IS invoked from
  // drag/drop and file-input handlers outside render, so a stable function
  // reference matters more than reacting to `t`'s identity — same reasoning
  // as the plain-function label helpers (see docs/i18n/README.md).
  const handleFile = useCallback(
    async (file: File) => {
      if (!file.name.toLowerCase().endsWith('.pdf')) {
        setError(i18n.t('iep-documents:upload.onlyPdf'));
        return;
      }

      if (file.size > 50 * 1024 * 1024) {
        setError(i18n.t('iep-documents:upload.tooLarge'));
        return;
      }

      setIsUploading(true);
      setError(null);

      try {
        const response = await attachFile(iepId, file);
        if (response.success) {
          showToast({ message: i18n.t('iep-documents:upload.uploadedToast'), variant: 'success' });
          onUploaded();
        } else {
          setError(response.message || i18n.t('iep-documents:upload.uploadFailed'));
        }
      } catch {
        setError(i18n.t('iep-documents:upload.uploadError'));
      } finally {
        setIsUploading(false);
      }
    },
    [iepId, onUploaded, showToast]
  );

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setIsDragging(false);
      const file = e.dataTransfer.files[0];
      if (file) handleFile(file);
    },
    [handleFile]
  );

  const handleFileInput = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (file) handleFile(file);
      e.target.value = '';
    },
    [handleFile]
  );

  return (
    <div>
      {error && (
        <div className="mb-3" data-testid="iep-upload-error">
          <Notice variant="error" title={error} />
        </div>
      )}

      <label
        onDragOver={(e) => {
          e.preventDefault();
          setIsDragging(true);
        }}
        onDragLeave={() => setIsDragging(false)}
        onDrop={handleDrop}
        data-testid="iep-upload-zone"
        className={`block border-2 border-dashed rounded-card p-6 text-center cursor-pointer transition-colors ${
          isDragging
            ? 'border-brand-teal-500 bg-brand-teal-50'
            : 'border-brand-slate-200 hover:border-brand-teal-300'
        } ${isUploading ? 'opacity-50 pointer-events-none' : ''}`}
      >
        <input
          type="file"
          accept=".pdf,application/pdf"
          onChange={handleFileInput}
          className="hidden"
          disabled={isUploading}
          data-testid="iep-file-input"
        />
        {isUploading ? (
          <div className="flex flex-col items-center gap-2">
            <Spinner size="sm" label={t('upload.uploadingLabel')} />
            <p className="text-brand-slate-500 text-sm">{t('upload.uploadingEllipsis')}</p>
          </div>
        ) : (
          <div className="flex flex-col items-center gap-1">
            <Upload className="w-5 h-5 text-brand-slate-400" strokeWidth={1.8} aria-hidden="true" />
            <p className="text-brand-slate-600 text-sm">{t('upload.attachPdf')}</p>
            <p className="text-brand-slate-500 text-[11px]">{t('upload.dropHint')}</p>
          </div>
        )}
      </label>
    </div>
  );
}
