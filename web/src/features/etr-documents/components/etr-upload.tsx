import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Upload } from 'lucide-react';
import i18n from '@/lib/i18n';
import { uploadFile } from '../api/etr-documents-api';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { useToast } from '@/components/ui/toast';

interface EtrUploadProps {
  etrId: number;
  onUploaded: () => void;
}

export function EtrUpload({ etrId, onUploaded }: EtrUploadProps) {
  const { t } = useTranslation('etr-documents');
  const { show: showToast } = useToast();
  const [isDragging, setIsDragging] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);

  // `i18n.t` directly (not the hook's `t`): see iep-upload.tsx's identical
  // comment — a stable callback reference matters more here than reacting
  // to `t`'s identity.
  const handleFile = useCallback(
    async (file: File) => {
      if (!file.name.toLowerCase().endsWith('.pdf')) {
        setError(i18n.t('etr-documents:upload.onlyPdf'));
        return;
      }

      if (file.size > 50 * 1024 * 1024) {
        setError(i18n.t('etr-documents:upload.tooLarge'));
        return;
      }

      setIsUploading(true);
      setError(null);
      setProgress(0);

      try {
        const response = await uploadFile(etrId, file, setProgress);
        if (response.success) {
          showToast({ message: i18n.t('etr-documents:upload.uploadedToast'), variant: 'success' });
          onUploaded();
        } else {
          setError(response.message || i18n.t('etr-documents:upload.uploadFailed'));
        }
      } catch {
        setError(i18n.t('etr-documents:upload.uploadError'));
      } finally {
        setIsUploading(false);
      }
    },
    [etrId, onUploaded, showToast]
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
        <div className="mb-3" data-testid="etr-upload-error">
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
        data-testid="etr-upload-zone"
        className={`block border-2 border-dashed rounded-card p-6 text-center cursor-pointer transition-colors ${
          isDragging
            ? 'border-brand-teal-500 bg-brand-teal-50'
            : 'border-brand-slate-200 hover:border-brand-teal-300'
        } ${isUploading ? 'opacity-80 pointer-events-none' : ''}`}
      >
        <input
          type="file"
          accept=".pdf,application/pdf"
          onChange={handleFileInput}
          className="hidden"
          disabled={isUploading}
          data-testid="etr-file-input"
        />
        {isUploading ? (
          <div className="flex flex-col items-center gap-2">
            <Spinner size="sm" label={t('upload.uploadingLabel')} />
            <p className="text-brand-slate-500 text-sm">{t('upload.uploadingProgress', { progress })}</p>
            <div className="w-full max-w-xs bg-brand-slate-100 rounded-full h-1.5 overflow-hidden">
              <div
                className="bg-brand-teal-500 h-full transition-all"
                style={{ width: `${progress}%` }}
                data-testid="etr-upload-progress"
              />
            </div>
          </div>
        ) : (
          <div className="flex flex-col items-center gap-1">
            <Upload className="w-5 h-5 text-brand-slate-400" strokeWidth={1.8} aria-hidden="true" />
            <p className="text-brand-slate-600 text-sm">{t('upload.attachPdf')}</p>
            <p className="text-brand-slate-500 text-[11px]">
              {t('upload.dropHint')}
            </p>
          </div>
        )}
      </label>
    </div>
  );
}
