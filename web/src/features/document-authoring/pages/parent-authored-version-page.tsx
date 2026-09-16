import { useParams } from 'react-router-dom';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Spinner } from '@/components/ui/spinner';
import { AuthoredPdfDownload } from '../components/authored-pdf-download';
import { AuthoredVersionSnapshot } from '../components/authored-version-snapshot';
import { useAuthoredVersion } from '../hooks/use-authored-version';
import { usePageTitle } from '@/hooks/use-page-title';

/**
 * Parent read-only view of a finalized document the school shared for their
 * child. Same frozen snapshot the educator sees; the server enforces the child
 * link, so an unrelated or revoked parent gets the error state.
 */
export function ParentAuthoredVersionPage() {
  const { childId: childIdParam, versionId: versionIdParam } = useParams<{
    childId: string;
    versionId: string;
  }>();
  const childId = Number(childIdParam);
  const versionId = Number(versionIdParam);
  const { version, isLoading, error } = useAuthoredVersion(versionId);
  usePageTitle(version ? `${version.documentTypeDisplayName} v${version.versionNumber}` : 'Document version');

  const backTo = `/children/${childId}/overview`;

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label="Loading document…" />
      </div>
    );
  }

  if (error || !version) {
    return (
      <PageLayout title="Document unavailable" breadcrumb={[{ label: 'Overview', to: backTo }]}>
        <Notice variant="error" title="Could not load this document">
          {error ?? 'This document is unavailable.'}
        </Notice>
      </PageLayout>
    );
  }

  return (
    <PageLayout
      title={`${version.documentTypeDisplayName} v${version.versionNumber}`}
      subtitle={`Finalized by the school ${formatDate(version.finalizedAt)}`}
      breadcrumb={[
        { label: 'Overview', to: backTo },
        { label: `${version.documentTypeDisplayName} v${version.versionNumber}` },
      ]}
      actions={<AuthoredPdfDownload versionId={version.id} initialStatus={version.pdfRenderStatus} />}
    >
      <Notice variant="info" title="This is the school's finalized document">
        It is read-only. Your private notes and analysis are never visible to the school.
      </Notice>
      <div className="mt-6">
        <AuthoredVersionSnapshot version={version} />
      </div>
    </PageLayout>
  );
}

function formatDate(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString();
}
