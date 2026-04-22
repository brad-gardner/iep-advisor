import { useCallback, useState } from 'react';
import { Upload } from 'lucide-react';
import { uploadFile } from '../api/etr-documents-api';
import { Notice } from '@/components/ui/notice';

interface EtrUploadProps {
  etrId: number;
  onUploaded: () => void;
}

export function EtrUpload({ etrId, onUploaded }: EtrUploadProps) {
  const [isDragging, setIsDragging] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);

  const handleFile = useCallback(
    async (file: File) => {
      if (!file.name.toLowerCase().endsWith('.pdf')) {
        setError('Only PDF files are supported');
        return;
      }

      if (file.size > 50 * 1024 * 1024) {
        setError('File is too large. Maximum size is 50MB.');
        return;
      }

      setIsUploading(true);
      setError(null);
      setProgress(0);

      try {
        const response = await uploadFile(etrId, file, setProgress);
        if (response.success) {
          onUploaded();
        } else {
          setError(response.message || 'Upload failed');
        }
      } catch {
        setError('An error occurred during upload');
      } finally {
        setIsUploading(false);
      }
    },
    [etrId, onUploaded]
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
            <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-brand-teal-500" />
            <p className="text-brand-slate-500 text-sm">Uploading... {progress}%</p>
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
            <p className="text-brand-slate-600 text-sm">Attach PDF</p>
            <p className="text-brand-slate-400 text-[11px]">
              Drop a PDF here or click to browse (max 50MB)
            </p>
          </div>
        )}
      </label>
    </div>
  );
}
