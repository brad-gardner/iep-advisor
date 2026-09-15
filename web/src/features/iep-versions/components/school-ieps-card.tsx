import { Link } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import { useChildAuthoredVersions } from '@/features/document-authoring/hooks/use-child-authored-versions';
import { useChildVersions } from '../hooks/use-version-list';
import { VersionHistoryList } from './version-history-list';
import { PdfStatusBadge } from './pdf-status-badge';

interface SchoolIepsCardProps {
  childId: number;
}

// Parent-side card listing every finalized document the school has shared:
// template-authored versions (IEP / ETR / 504 — the current engine) and any
// legacy typed IEP versions. Renders nothing when there are none, so parents
// whose child has no school link never see an empty card.
export function SchoolIepsCard({ childId }: SchoolIepsCardProps) {
  const legacy = useChildVersions(childId);
  const authored = useChildAuthoredVersions(childId);
  const isLoading = legacy.isLoading || authored.isLoading;

  if (!isLoading && legacy.versions.length === 0 && authored.versions.length === 0) return null;

  return (
    <Card data-testid="school-ieps-section">
      <h2 className="font-serif mb-1">School documents</h2>
      <p className="text-sm text-brand-slate-400 mb-4">
        Finalized documents shared by your child's school.
      </p>
      {authored.versions.length > 0 && (
        <ul className="divide-y divide-brand-slate-100" data-testid="school-documents-list">
          {authored.versions.map((v) => (
            <li key={v.id} className="py-3">
              <Link
                to={`/children/${childId}/authored-versions/${v.id}`}
                className="flex items-center justify-between gap-4 hover:underline"
                data-testid={`authored-version-link-${v.id}`}
              >
                <span className="flex flex-col">
                  <span className="text-sm font-medium text-brand-slate-800">
                    {v.documentTypeDisplayName} <span className="text-brand-slate-400">v{v.versionNumber}</span>
                  </span>
                  <span className="text-xs text-brand-slate-500">Finalized {formatDate(v.finalizedAt)}</span>
                </span>
                <PdfStatusBadge status={v.pdfRenderStatus} />
              </Link>
            </li>
          ))}
        </ul>
      )}
      {legacy.versions.length > 0 && (
        <VersionHistoryList
          versions={legacy.versions}
          isLoading={legacy.isLoading}
          linkBase={`/children/${childId}/iep-versions`}
        />
      )}
    </Card>
  );
}

function formatDate(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString();
}
