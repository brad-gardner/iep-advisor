import { Link } from 'react-router-dom';
import { PdfStatusBadge } from './pdf-status-badge';
import type { IepVersionSummaryDto } from '../types';

interface VersionHistoryListProps {
  versions: IepVersionSummaryDto[];
  // Each row links to `${linkBase}/${version.id}` (the detail route).
  linkBase: string;
  isLoading?: boolean;
}

// Read-only list of finalized IEP versions. Used on both the educator
// student-detail page and the parent child overview.
export function VersionHistoryList({ versions, linkBase, isLoading }: VersionHistoryListProps) {
  if (isLoading) {
    return <p className="text-sm text-brand-slate-500">Loading versions…</p>;
  }

  if (versions.length === 0) {
    return (
      <p className="text-sm text-brand-slate-500" data-testid="no-versions">
        No finalized IEP versions yet.
      </p>
    );
  }

  return (
    <ul className="divide-y divide-brand-slate-100" data-testid="version-history-list">
      {versions.map((v) => (
        <li key={v.id} className="py-3">
          <Link
            to={`${linkBase}/${v.id}`}
            className="flex items-center justify-between gap-4 hover:underline"
            data-testid={`version-link-${v.id}`}
          >
            <span className="flex flex-col">
              <span className="text-sm font-medium text-brand-slate-800">
                {v.title || `IEP v${v.versionNumber}`}{' '}
                <span className="text-brand-slate-400">v{v.versionNumber}</span>
              </span>
              <span className="text-xs text-brand-slate-500">
                Finalized {formatDate(v.finalizedAt)}
              </span>
            </span>
            <PdfStatusBadge status={v.pdfRenderStatus} />
          </Link>
        </li>
      ))}
    </ul>
  );
}

function formatDate(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString();
}
