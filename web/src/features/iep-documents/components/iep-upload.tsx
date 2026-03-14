import { useCallback, useState } from 'react';
import { Upload } from 'lucide-react';
import { attachFile } from '../api/iep-documents-api';
import { Notice } from '@/components/ui/notice';

interface IepUploadProps {
  iepId: number;
  onUploaded: () => void;
}

export function IepUpload({ iepId, onUploaded }: IepUploadProps) {
  const [isDragging, setIsDragging] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleFile = useCallback(
    async (file: File) => {
      if (!file.name.toLowerCase().endsWith('.pdf')) {
        setError('Only PDF files are supported');
        return;
      }

      setIsUploading(true);
      setError(null);

      try {
        const response = await attachFile(iepId, file);
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
    [iepId, onUploaded]
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
        <div className="mb-3">
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
        />
        {isUploading ? (
          <div className="flex flex-col items-center gap-2">
            <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-brand-teal-500" />
            <p className="text-brand-slate-400 text-sm">Uploading...</p>
          </div>
        ) : (
          <div className="flex flex-col items-center gap-1">
            <Upload className="w-5 h-5 text-brand-slate-400" strokeWidth={1.8} aria-hidden="true" />
            <p className="text-brand-slate-600 text-sm">Attach PDF</p>
            <p className="text-brand-slate-400 text-[11px]">Drop a PDF here or click to browse</p>
          </div>
        )}
      </label>
    </div>
  );
}
