import { useCallback, useState } from 'react';
import { uploadIepDocument } from '../api/iep-documents-api';

interface IepUploadProps {
  childId: number;
  onUploaded: () => void;
}

export function IepUpload({ childId, onUploaded }: IepUploadProps) {
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
        const response = await uploadIepDocument(childId, file);
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
    [childId, onUploaded]
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
        <div className="p-3 rounded text-sm bg-red-50 text-red-600 mb-3">{error}</div>
      )}

      <label
        onDragOver={(e) => {
          e.preventDefault();
          setIsDragging(true);
        }}
        onDragLeave={() => setIsDragging(false)}
        onDrop={handleDrop}
        className={`block border-2 border-dashed rounded-lg p-8 text-center cursor-pointer transition-colors ${
          isDragging
            ? 'border-blue-500 bg-blue-50'
            : 'border-gray-300 hover:border-gray-400'
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
            <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-blue-500" />
            <p className="text-gray-500 text-sm">Uploading...</p>
          </div>
        ) : (
          <div>
            <p className="text-gray-600">Drop a PDF here or click to browse</p>
            <p className="text-gray-400 text-sm mt-1">PDF files only, up to 50MB</p>
          </div>
        )}
      </label>
    </div>
  );
}
