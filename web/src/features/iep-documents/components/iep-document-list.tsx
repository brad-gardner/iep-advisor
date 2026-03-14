import { useState } from 'react';
import { Link } from 'react-router-dom';
import type { IepDocument } from '@/types/api';
import { deleteIepDocument, getDownloadUrl } from '../api/iep-documents-api';

interface IepDocumentListProps {
  documents: IepDocument[];
  isLoading: boolean;
  onDeleted: () => void;
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function statusBadge(status: string) {
  const colors: Record<string, string> = {
    uploaded: 'bg-gray-200 text-gray-700',
    processing: 'bg-yellow-100 text-yellow-700',
    parsed: 'bg-green-100 text-green-700',
    error: 'bg-red-100 text-red-700',
  };
  return (
    <span className={`text-xs px-2 py-0.5 rounded ${colors[status] || colors.uploaded}`}>
      {status}
    </span>
  );
}

export function IepDocumentList({ documents, isLoading, onDeleted }: IepDocumentListProps) {
  const [deletingId, setDeletingId] = useState<number | null>(null);

  if (isLoading) {
    return (
      <div className="flex justify-center py-4">
        <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-gray-900" />
      </div>
    );
  }

  if (documents.length === 0) {
    return <p className="text-gray-400 text-sm">No IEP documents uploaded yet.</p>;
  }

  const handleDownload = async (id: number) => {
    const response = await getDownloadUrl(id);
    if (response.success && response.data) {
      window.open(response.data.url, '_blank');
    }
  };

  const handleDelete = async (id: number) => {
    if (!confirm('Delete this IEP document?')) return;
    setDeletingId(id);
    try {
      const response = await deleteIepDocument(id);
      if (response.success) onDeleted();
    } catch {
      // handled by interceptor
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <div className="space-y-2">
      {documents.map((doc) => (
        <div key={doc.id} className="bg-gray-50 rounded p-3 flex items-center justify-between border border-gray-200">
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2">
              <Link to={`/ieps/${doc.id}`} className="font-medium truncate text-gray-900 hover:text-blue-600 transition-colors">
                {doc.fileName}
              </Link>
              {statusBadge(doc.status)}
            </div>
            <div className="flex gap-3 text-xs text-gray-500 mt-1">
              <span>{formatFileSize(doc.fileSizeBytes)}</span>
              <span>Uploaded {new Date(doc.uploadDate).toLocaleDateString()}</span>
            </div>
          </div>
          <div className="flex gap-2 ml-3 shrink-0">
            {doc.status === 'parsed' && (
              <Link to={`/ieps/${doc.id}`} className="text-sm text-green-600 hover:text-green-700">
                View
              </Link>
            )}
            <button
              onClick={() => handleDownload(doc.id)}
              className="text-sm text-blue-600 hover:text-blue-700"
            >
              Download
            </button>
            <button
              onClick={() => handleDelete(doc.id)}
              disabled={deletingId === doc.id}
              className="text-sm text-red-600 hover:text-red-700 disabled:opacity-50"
            >
              {deletingId === doc.id ? '...' : 'Delete'}
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}
