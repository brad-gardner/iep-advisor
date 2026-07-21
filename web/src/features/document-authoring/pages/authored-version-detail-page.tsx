import { useParams } from 'react-router-dom';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Spinner } from '@/components/ui/spinner';
import { AuthoredPdfDownload } from '../components/authored-pdf-download';
import { AuthoredVersionSnapshot } from '../components/authored-version-snapshot';
import { useAuthoredVersion } from '../hooks/use-authored-version';

// Educator read-only view of a finalized authored version: the frozen snapshot
// plus a download-with-status (retry allowed for educators).
export function AuthoredVersionDetailPage() {
  const { studentId: studentIdParam, versionId: versionIdParam } = useParams<{
    studentId: string;
    versionId: string;
  }>();
  const studentId = Number(studentIdParam);
  const versionId = Number(versionIdParam);
  const { version, isLoading, error } = useAuthoredVersion(versionId);

  const backTo = `/educator/students/${studentId}/documents`;

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label="Loading version…" />
      </div>
    );
  }

  if (error || !version) {
    return (
      <PageLayout
        title="Version unavailable"
        breadcrumb={[{ label: 'Documents', to: backTo }]}
      >
        <Notice variant="error" title="Could not load this version">
          {error ?? 'This version is unavailable.'}
        </Notice>
      </PageLayout>
    );
  }

  return (
    <PageLayout
      title={`${version.documentTypeDisplayName} v${version.versionNumber}`}
      subtitle={`Finalized ${formatDate(version.finalizedAt)}`}
      breadcrumb={[
        { label: 'Documents', to: backTo },
        { label: `v${version.versionNumber}` },
      ]}
      actions={
        <AuthoredPdfDownload
          versionId={version.id}
          initialStatus={version.pdfRenderStatus}
          canRetry
        />
      }
    >
      <AuthoredVersionSnapshot version={version} />
    </PageLayout>
  );
}

function formatDate(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString();
}
