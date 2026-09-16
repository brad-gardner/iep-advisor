import { useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Spinner } from '@/components/ui/spinner';
import { formatDate } from '@/lib/format-date';
import { AmendDialog } from '../components/amend-dialog';
import { AmendmentChain } from '../components/amendment-chain';
import { AuthoredPdfDownload } from '../components/authored-pdf-download';
import { AuthoredVersionSnapshot } from '../components/authored-version-snapshot';
import { SignatureStatusBadge } from '../components/signature-status-badge';
import { SignedArtifactsPanel } from '../components/signed-artifacts-panel';
import { useAuthoredVersion } from '../hooks/use-authored-version';
import { useAuthoredVersions } from '../hooks/use-authored-versions';
import { usePageTitle } from '@/hooks/use-page-title';

// Educator read-only view of a finalized authored version: the frozen snapshot,
// signature status + amendment chain, signed-artifact upload/list, and an
// "Amend" action — plus a download-with-status (retry allowed for educators).
export function AuthoredVersionDetailPage() {
  const { studentId: studentIdParam, versionId: versionIdParam } = useParams<{
    studentId: string;
    versionId: string;
  }>();
  const studentId = Number(studentIdParam);
  const versionId = Number(versionIdParam);
  const navigate = useNavigate();
  const { version, isLoading, error, applyUpdate } = useAuthoredVersion(versionId);
  // Loaded to resolve amendedByVersionIds → version numbers for the chain links
  // (the version detail only carries the number for what IT amends, not the
  // reverse — the sibling list already fetched for this student has both).
  const { versions: siblingVersions } = useAuthoredVersions(studentId);
  const versionNumberById = useMemo(() => {
    const map = new Map<number, number>();
    for (const v of siblingVersions) map.set(v.id, v.versionNumber);
    return map;
  }, [siblingVersions]);
  usePageTitle(version ? `${version.documentTypeDisplayName} v${version.versionNumber}` : 'Document version');

  const [amendOpen, setAmendOpen] = useState(false);

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
        <div className="flex flex-wrap items-center gap-2">
          <AuthoredPdfDownload
            versionId={version.id}
            initialStatus={version.pdfRenderStatus}
            canRetry
          />
          <Button variant="secondary" size="sm" onClick={() => setAmendOpen(true)} data-testid="amend-open">
            Amend
          </Button>
        </div>
      }
    >
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <SignatureStatusBadge status={version.signatureStatus} data-testid="version-signature-status" />
        <AmendmentChain
          studentId={studentId}
          amendsVersionId={version.amendsVersionId}
          amendsVersionNumber={version.amendsVersionNumber}
          amendedByVersionIds={version.amendedByVersionIds}
          amendedByVersionNumbers={version.amendedByVersionIds.map((id) => versionNumberById.get(id) ?? null)}
          data-testid="version-amendment-chain"
        />
      </div>

      {version.amendmentReason && (
        <div className="mb-4">
          <Notice variant="info" title="Amendment reason">
            {version.amendmentReason}
            {version.effectiveDate ? ` · Effective ${formatDate(version.effectiveDate)}` : ''}
          </Notice>
        </div>
      )}

      <div className="space-y-6">
        <SignedArtifactsPanel
          versionId={version.id}
          onUploaded={(_artifact, status) =>
            applyUpdate({
              signatureStatus: status,
              signedArtifactCount: version.signedArtifactCount + 1,
            })
          }
        />

        <AuthoredVersionSnapshot templateVersion={version.templateVersion} values={version.values} />
      </div>

      <AmendDialog
        open={amendOpen}
        versionId={version.id}
        onClose={() => setAmendOpen(false)}
        onAmended={(instanceId) => navigate(`/educator/documents/${instanceId}`)}
      />
    </PageLayout>
  );
}
