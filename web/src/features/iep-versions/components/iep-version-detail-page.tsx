import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { PageLayout } from '@/components/ui/page-layout';
import { usePageTitle } from '@/hooks/use-page-title';
import { formatDate } from '@/lib/format-date';
import { getVersion } from '../api/iep-versions-api';
import type { IepVersionDto } from '../types';
import { DownloadPdfButton } from './download-pdf-button';
import { VersionSnapshot } from './version-snapshot';

interface IepVersionDetailPageProps {
  // The same read-only page serves both the educator and the linked parent.
  // Educators can retry a failed PDF render; parents cannot.
  canRetry: boolean;
  // Where the "back" link points (educator vs. parent context).
  backTo: string;
  backLabel: string;
}

// A server-provided message is already resolved text and shown as-is; the
// generic fallback is translated at RENDER time (see `error` below) rather
// than load time, so a language switch after a failed load shows the new
// language immediately, with no refetch (same pattern as
// upcoming-meeting-card.tsx — see docs/i18n/README.md).
type LoadError = { kind: 'server'; message: string } | { kind: 'generic' };

export function IepVersionDetailPage({ canRetry, backTo, backLabel }: IepVersionDetailPageProps) {
  const { t } = useTranslation('iep-versions');
  const { versionId: versionIdParam } = useParams<{ versionId: string }>();
  const versionId = Number(versionIdParam);

  const [version, setVersion] = useState<IepVersionDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<LoadError | null>(null);
  usePageTitle(
    version
      ? `${version.title || t('detailPage.titleFallback')} v${version.versionNumber}`
      : t('detailPage.pageTitleFallback')
  );

  useEffect(() => {
    let active = true;
    getVersion(versionId)
      .then((res) => {
        if (!active) return;
        if (res.success && res.data) setVersion(res.data);
        else setLoadError(res.message ? { kind: 'server', message: res.message } : { kind: 'generic' });
      })
      .catch(() => {
        if (active) setLoadError({ kind: 'generic' });
      })
      .finally(() => {
        if (active) setIsLoading(false);
      });
    return () => {
      active = false;
    };
    // `t` deliberately excluded — see the `LoadError` comment above.
  }, [versionId]);

  const error = loadError ? (loadError.kind === 'server' ? loadError.message : t('detailPage.loadErrorGeneric')) : null;

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label={t('detailPage.loading')} />
      </div>
    );
  }

  if (error || !version) {
    return (
      <div className="space-y-4">
        <Notice variant="error" title={t('detailPage.loadErrorTitle')}>
          {error ?? t('detailPage.loadErrorGeneric')}
        </Notice>
        <Link to={backTo} className="text-sm text-brand-teal-500 hover:underline">
          ← {backLabel}
        </Link>
      </div>
    );
  }

  const subtitle =
    t('detailPage.finalized', { date: formatDate(version.finalizedAt) }) +
    (version.effectiveDate ? t('detailPage.effective', { date: formatDate(version.effectiveDate) }) : '');

  return (
    <PageLayout
      title={`${version.title || t('detailPage.titleFallback')} v${version.versionNumber}`}
      subtitle={subtitle}
      breadcrumb={[{ label: backLabel, to: backTo }]}
      actions={
        <DownloadPdfButton
          versionId={version.id}
          initialStatus={version.pdfRenderStatus}
          canRetry={canRetry}
        />
      }
    >
      <VersionSnapshot version={version} />
    </PageLayout>
  );
}
