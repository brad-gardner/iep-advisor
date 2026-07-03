import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Download, Trash2, Eye, FileText } from 'lucide-react';
import type { IepDocument } from '@/types/api';
import { deleteIepDocument, getDownloadUrl } from '../api/iep-documents-api';
import { setCurrentIep } from '@/features/children/api/children-api';
import { IepUpload } from './iep-upload';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { EmptyState } from '@/components/ui/empty-state';
import { useToast } from '@/components/ui/toast';

interface IepDocumentListProps {
  documents: IepDocument[];
  isLoading: boolean;
  onDeleted: () => void;
  currentIepId?: number | null;
  canSetCurrent?: boolean;
  onCurrentChanged?: () => void;
}

function formatFileSize(bytes: number): string {
  if (bytes === 0) return '';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

const STATUS_VARIANTS: Record<string, 'neutral' | 'warning' | 'success' | 'error'> = {
  created: 'neutral',
  uploaded: 'neutral',
  processing: 'warning',
  parsed: 'success',
  error: 'error',
};

const MEETING_TYPE_LABELS: Record<string, string> = {
  initial: 'Initial IEP',
  annual_review: 'Annual Review',
  amendment: 'Amendment',
  reevaluation: 'Reevaluation',
};

function formatMeetingDate(dateStr: string | null): string {
  if (!dateStr) return '';
  return new Date(dateStr).toLocaleDateString();
}

export function IepDocumentList({
  documents,
  isLoading,
  onDeleted,
  currentIepId,
  canSetCurrent,
  onCurrentChanged,
}: IepDocumentListProps) {
  const { show: showToast } = useToast();
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [settingCurrentId, setSettingCurrentId] = useState<number | null>(null);

  const handleSetCurrent = async (doc: IepDocument) => {
    setSettingCurrentId(doc.id);
    try {
      const response = await setCurrentIep(doc.childProfileId, doc.id);
      if (response.success) onCurrentChanged?.();
    } catch {
      // handled by interceptor
    } finally {
      setSettingCurrentId(null);
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-4">
        <Spinner size="sm" label="Loading IEP documents…" />
      </div>
    );
  }

  if (documents.length === 0) {
    return <EmptyState icon={FileText} title="No IEP documents yet." />;
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
      if (response.success) {
        showToast({ message: 'IEP deleted', variant: 'success' });
        onDeleted();
      }
    } catch {
      // handled by interceptor
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <div className="space-y-2">
      {documents.map((doc) => (
        <Card key={doc.id} className="p-3" data-testid="iep-document-card">
          <div className="flex items-center justify-between">
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2 flex-wrap">
                <Link
                  to={`/children/${doc.childProfileId}/ieps/${doc.id}`}
                  className="text-[13px] font-medium truncate text-brand-slate-800 hover:text-brand-teal-500 transition-colors"
                >
                  {doc.fileName || (doc.meetingType ? MEETING_TYPE_LABELS[doc.meetingType] || doc.meetingType : `IEP #${doc.id}`)}
                </Link>
                {doc.meetingType && (
                  <Badge variant="neutral">
                    {MEETING_TYPE_LABELS[doc.meetingType] || doc.meetingType}
                  </Badge>
                )}
                <Badge variant={STATUS_VARIANTS[doc.status] || 'neutral'}>
                  {doc.status}
                </Badge>
                {currentIepId === doc.id && (
                  <Badge variant="success" data-testid="current-iep-badge">
                    Current
                  </Badge>
                )}
              </div>
              <div className="flex gap-3 text-[11px] text-brand-slate-400 mt-1">
                {doc.iepDate && <span>Meeting: {formatMeetingDate(doc.iepDate)}</span>}
                {doc.fileSizeBytes > 0 && <span>{formatFileSize(doc.fileSizeBytes)}</span>}
                <span>Created {new Date(doc.createdAt).toLocaleDateString()}</span>
              </div>
            </div>
            <div className="flex gap-2 ml-3 shrink-0">
              {canSetCurrent && currentIepId !== doc.id && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => handleSetCurrent(doc)}
                  loading={settingCurrentId === doc.id}
                  data-testid="set-current-iep-button"
                >
                  Set as current
                </Button>
              )}
              {doc.status === 'parsed' && (
                <Link
                  to={`/children/${doc.childProfileId}/ieps/${doc.id}`}
                  className="inline-flex items-center gap-1 text-[13px] font-medium text-brand-teal-500 hover:text-brand-teal-600 transition-colors"
                >
                  <Eye className="w-3.5 h-3.5" strokeWidth={1.8} aria-hidden="true" />
                  View
                </Link>
              )}
              {doc.fileSizeBytes > 0 && (
                <Button variant="ghost" size="sm" onClick={() => handleDownload(doc.id)}>
                  <Download className="w-3.5 h-3.5 mr-1" strokeWidth={1.8} aria-hidden="true" />
                  Download
                </Button>
              )}
              <Button
                variant="danger"
                size="sm"
                onClick={() => handleDelete(doc.id)}
                loading={deletingId === doc.id}
              >
                <Trash2 className="w-3.5 h-3.5 mr-1" strokeWidth={1.8} aria-hidden="true" />
                Delete
              </Button>
            </div>
          </div>

          {doc.status === 'created' && (
            <div className="mt-3">
              <IepUpload iepId={doc.id} onUploaded={onDeleted} />
            </div>
          )}
        </Card>
      ))}
    </div>
  );
}
