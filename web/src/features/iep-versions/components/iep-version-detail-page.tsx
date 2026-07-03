import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { PageLayout } from '@/components/ui/page-layout';
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

export function IepVersionDetailPage({ canRetry, backTo, backLabel }: IepVersionDetailPageProps) {
  const { versionId: versionIdParam } = useParams<{ versionId: string }>();
  const versionId = Number(versionIdParam);

  const [version, setVersion] = useState<IepVersionDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    getVersion(versionId)
      .then((res) => {
        if (!active) return;
        if (res.success && res.data) setVersion(res.data);
        else setError(res.message || 'This IEP version is unavailable.');
      })
      .catch(() => {
        if (active) setError('This IEP version is unavailable.');
      })
      .finally(() => {
        if (active) setIsLoading(false);
      });
    return () => {
      active = false;
    };
  }, [versionId]);

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label="Loading IEP version…" />
      </div>
    );
  }

  if (error || !version) {
    return (
      <div className="space-y-4">
        <Notice variant="error" title="Could not load this IEP version">
          {error ?? 'The version is unavailable.'}
        </Notice>
        <Link to={backTo} className="text-sm text-brand-teal-500 hover:underline">
          ← {backLabel}
        </Link>
      </div>
    );
  }

  const subtitle =
    `Finalized ${formatDate(version.finalizedAt)}` +
    (version.effectiveDate ? ` · Effective ${formatDate(version.effectiveDate)}` : '');

  return (
    <PageLayout
      title={`${version.title || 'IEP'} v${version.versionNumber}`}
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

function formatDate(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString();
}
