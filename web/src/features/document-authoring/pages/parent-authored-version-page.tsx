import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Spinner } from '@/components/ui/spinner';
import { formatDate } from '@/lib/format-date';
import { AuthoredPdfDownload } from '../components/authored-pdf-download';
import { AuthoredVersionSnapshot } from '../components/authored-version-snapshot';
import { useAuthoredVersion } from '../hooks/use-authored-version';
import { usePageTitle } from '@/hooks/use-page-title';

/**
 * Parent read-only view of a finalized document the school shared for their
 * child. Same frozen snapshot the educator sees; the server enforces the child
 * link, so an unrelated or revoked parent gets the error state.
 *
 * This page is "iep-versions (incl. authored versions viewer)" per the i18n
 * plan's Phase 3 assignment, so it uses the `iep-versions` namespace even
 * though the file lives under `features/document-authoring`. The shared
 * `AuthoredVersionSnapshot` content below (field/table labels) is NOT
 * translated here — it's shared with the educator detail page
 * (document-authoring, a later phase) and the parent shared-draft review
 * page (draft-sharing, a concurrent phase-3 assignment); converting it here
 * risked a conflicting edit on a file outside this assignment's feature
 * list. See the implementation report's concerns.
 */
export function ParentAuthoredVersionPage() {
  const { t } = useTranslation('iep-versions');
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
        <Spinner label={t('authoredVersion.loading')} />
      </div>
    );
  }

  if (error || !version) {
    return (
      <PageLayout title={t('authoredVersion.unavailableTitle')} breadcrumb={[{ label: t('authoredVersion.overviewBreadcrumb'), to: backTo }]}>
        <Notice variant="error" title={t('authoredVersion.loadErrorTitle')}>
          {error ?? t('authoredVersion.loadErrorGeneric')}
        </Notice>
      </PageLayout>
    );
  }

  return (
    <PageLayout
      title={`${version.documentTypeDisplayName} v${version.versionNumber}`}
      subtitle={t('authoredVersion.finalizedBySchool', { date: formatDate(version.finalizedAt) })}
      breadcrumb={[
        { label: t('authoredVersion.overviewBreadcrumb'), to: backTo },
        { label: `${version.documentTypeDisplayName} v${version.versionNumber}` },
      ]}
      actions={<AuthoredPdfDownload versionId={version.id} initialStatus={version.pdfRenderStatus} />}
    >
      <Notice variant="info" title={t('authoredVersion.readOnlyTitle')}>
        {t('authoredVersion.readOnlyBody')}
      </Notice>
      <div className="mt-6">
        <AuthoredVersionSnapshot templateVersion={version.templateVersion} values={version.values} />
      </div>
    </PageLayout>
  );
}
