import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Download, Trash2, Eye } from 'lucide-react';
import type { IepDocument } from '@/types/api';
import { deleteIepDocument, getDownloadUrl } from '../api/iep-documents-api';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';

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

const STATUS_VARIANTS: Record<string, 'neutral' | 'warning' | 'success' | 'error'> = {
  uploaded: 'neutral',
  processing: 'warning',
  parsed: 'success',
  error: 'error',
};

export function IepDocumentList({ documents, isLoading, onDeleted }: IepDocumentListProps) {
  const [deletingId, setDeletingId] = useState<number | null>(null);

  if (isLoading) {
    return (
      <div className="flex justify-center py-4">
        <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (documents.length === 0) {
    return <p className="text-brand-slate-400 text-sm">No IEP documents uploaded yet.</p>;
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
        <Card key={doc.id} className="p-3 flex items-center justify-between">
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2">
              <Link
                to={`/ieps/${doc.id}`}
                className="text-[13px] font-medium truncate text-brand-slate-800 hover:text-brand-teal-500 transition-colors"
              >
                {doc.fileName}
              </Link>
              <Badge variant={STATUS_VARIANTS[doc.status] || 'neutral'}>
                {doc.status}
              </Badge>
            </div>
            <div className="flex gap-3 text-[11px] text-brand-slate-400 mt-1">
              <span>{formatFileSize(doc.fileSizeBytes)}</span>
              <span>Uploaded {new Date(doc.uploadDate).toLocaleDateString()}</span>
            </div>
          </div>
          <div className="flex gap-2 ml-3 shrink-0">
            {doc.status === 'parsed' && (
              <Link
                to={`/ieps/${doc.id}`}
                className="inline-flex items-center gap-1 text-[13px] font-medium text-brand-teal-500 hover:text-brand-teal-600 transition-colors"
              >
                <Eye className="w-3.5 h-3.5" strokeWidth={1.8} aria-hidden="true" />
                View
              </Link>
            )}
            <button
              onClick={() => handleDownload(doc.id)}
              className="inline-flex items-center gap-1 text-[13px] font-medium text-brand-slate-400 hover:text-brand-teal-500 transition-colors"
            >
              <Download className="w-3.5 h-3.5" strokeWidth={1.8} aria-hidden="true" />
              Download
            </button>
            <button
              onClick={() => handleDelete(doc.id)}
              disabled={deletingId === doc.id}
              className="inline-flex items-center gap-1 text-[13px] font-medium text-brand-red hover:text-red-800 disabled:opacity-50 transition-colors"
            >
              <Trash2 className="w-3.5 h-3.5" strokeWidth={1.8} aria-hidden="true" />
              {deletingId === doc.id ? '...' : 'Delete'}
            </button>
          </div>
        </Card>
      ))}
    </div>
  );
}
