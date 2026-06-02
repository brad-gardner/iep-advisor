import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Notice } from '@/components/ui/notice';
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
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
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

  return (
    <div className="space-y-6">
      <header className="space-y-3">
        <Link to={backTo} className="text-sm text-brand-teal-500 hover:underline">
          ← {backLabel}
        </Link>
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 className="font-serif">
              {version.title || 'IEP'}{' '}
              <span className="text-brand-slate-400">v{version.versionNumber}</span>
            </h1>
            <p className="text-sm text-brand-slate-500">
              Finalized {formatDate(version.finalizedAt)}
              {version.effectiveDate && ` · Effective ${formatDate(version.effectiveDate)}`}
            </p>
          </div>
          <DownloadPdfButton
            versionId={version.id}
            initialStatus={version.pdfRenderStatus}
            canRetry={canRetry}
          />
        </div>
      </header>

      <VersionSnapshot version={version} />
    </div>
  );
}

function formatDate(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString();
}
