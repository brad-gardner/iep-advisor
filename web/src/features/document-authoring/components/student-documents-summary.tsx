import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Badge } from '@/components/ui/badge';
import { EmptyState } from '@/components/ui/empty-state';
import { Skeleton } from '@/components/ui/skeleton';
import { relativeTime } from '@/lib/relative-time';
import { useDocumentList } from '../hooks/use-document-list';
import { useAuthoredVersions } from '../hooks/use-authored-versions';
import { documentStatusLabel } from '../lib/document-status-label';
import type { DocumentInstanceStatus } from '../types';

const statusVariant: Record<DocumentInstanceStatus, 'neutral' | 'warning' | 'success'> = {
  Draft: 'neutral',
  Finalizing: 'warning',
  Finalized: 'success',
};

interface StudentDocumentsSummaryProps {
  studentId: number;
}

/**
 * Compact "what documents does this student have" list for the student page:
 * in-progress drafts (link to the editor) and finalized versions (link to the
 * snapshot). The full management surface lives at /documents.
 */
export function StudentDocumentsSummary({ studentId }: StudentDocumentsSummaryProps) {
  const { t } = useTranslation('document-authoring');
  const drafts = useDocumentList(studentId);
  const versions = useAuthoredVersions(studentId);
  const isLoading = drafts.isLoading || versions.isLoading;

  if (isLoading) {
    return (
      <div className="space-y-2" role="status" aria-label={t('studentDocumentsSummary.loadingAriaLabel')}>
        <Skeleton className="h-5 w-3/4" />
        <Skeleton className="h-5 w-1/2" />
        <span className="sr-only">{t('studentDocumentsSummary.loading')}</span>
      </div>
    );
  }

  const openDrafts = drafts.documents.filter((d) => d.status !== 'Finalized');
  if (openDrafts.length === 0 && versions.versions.length === 0) {
    return (
      <EmptyState
        title={t('studentDocumentsSummary.noDocumentsTitle')}
        description={t('studentDocumentsSummary.noDocumentsDescription')}
      />
    );
  }

  return (
    <div className="space-y-4" data-testid="student-documents-summary">
      {openDrafts.length > 0 && (
        <div>
          <h3 className="mb-1 text-[13px] font-medium uppercase tracking-wide text-brand-slate-500">
            {t('studentDocumentsSummary.inProgressHeading')}
          </h3>
          <ul className="divide-y divide-brand-slate-100">
            {openDrafts.map((d) => (
              <li key={d.id} className="flex items-center justify-between gap-3 py-2">
                <Link
                  to={`/educator/documents/${d.id}`}
                  className="text-sm font-medium text-brand-teal-600 hover:underline"
                  data-testid={`draft-link-${d.id}`}
                >
                  {d.documentTypeDisplayName}
                </Link>
                <span className="flex items-center gap-2 text-xs text-brand-slate-500">
                  {d.lastEditedAt
                    ? t('studentDocumentsSummary.editedAgo', { time: relativeTime(d.lastEditedAt) })
                    : t('studentDocumentsSummary.notStarted')}
                  <Badge variant={statusVariant[d.status]}>{documentStatusLabel(d.status)}</Badge>
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
      {versions.versions.length > 0 && (
        <div>
          <h3 className="mb-1 text-[13px] font-medium uppercase tracking-wide text-brand-slate-500">
            {t('studentDocumentsSummary.finalizedHeading')}
          </h3>
          <ul className="divide-y divide-brand-slate-100">
            {versions.versions.map((v) => (
              <li key={v.id} className="flex items-center justify-between gap-3 py-2">
                <Link
                  to={`/educator/students/${studentId}/authored-versions/${v.id}`}
                  className="text-sm font-medium text-brand-teal-600 hover:underline"
                  data-testid={`version-link-${v.id}`}
                >
                  {`${v.documentTypeDisplayName} v${v.versionNumber}`}
                </Link>
                <span className="text-xs text-brand-slate-500">
                  {t('studentDocumentsSummary.finalizedAgo', { time: relativeTime(v.finalizedAt) })}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
