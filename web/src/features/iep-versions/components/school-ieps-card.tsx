import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Card } from '@/components/ui/card';
import { useChildAuthoredVersions } from '@/features/document-authoring/hooks/use-child-authored-versions';
import { formatDate } from '@/lib/format-date';
import { useChildVersions } from '../hooks/use-version-list';
import { PdfStatusBadge } from './pdf-status-badge';

interface SchoolIepsCardProps {
  childId: number;
}

interface SchoolDocumentRow {
  id: string;
  label: string;
  finalizedAt: string;
  to: string;
  pdfStatus: string | null | undefined;
}

// Parent-side card listing every finalized document the school has shared:
// template-authored versions (IEP / ETR / 504 — the current engine) merged with
// any legacy typed IEP versions into one date-sorted list. Renders nothing
// while loading and nothing when empty, so parents whose child has no school
// link never see a card appear and vanish.
export function SchoolIepsCard({ childId }: SchoolIepsCardProps) {
  const { t } = useTranslation('iep-versions');
  const legacy = useChildVersions(childId);
  const authored = useChildAuthoredVersions(childId);
  if (legacy.isLoading || authored.isLoading) return null;

  const rows: SchoolDocumentRow[] = [
    ...authored.versions.map((v) => ({
      id: `authored-${v.id}`,
      label: `${v.documentTypeDisplayName} v${v.versionNumber}`,
      finalizedAt: v.finalizedAt,
      to: `/children/${childId}/authored-versions/${v.id}`,
      pdfStatus: v.pdfRenderStatus,
    })),
    ...legacy.versions.map((v) => ({
      id: `legacy-${v.id}`,
      label: v.title || t('schoolCard.titleFallback', { number: v.versionNumber }),
      finalizedAt: v.finalizedAt,
      to: `/children/${childId}/iep-versions/${v.id}`,
      pdfStatus: v.pdfRenderStatus,
    })),
  ].sort((a, b) => b.finalizedAt.localeCompare(a.finalizedAt));

  if (rows.length === 0) return null;

  return (
    <Card data-testid="school-ieps-section">
      <h2 className="font-serif mb-1">{t('schoolCard.heading')}</h2>
      <p className="text-sm text-brand-slate-500 mb-4">{t('schoolCard.body')}</p>
      <ul className="divide-y divide-brand-slate-100" data-testid="school-documents-list">
        {rows.map((row) => (
          <li key={row.id} className="py-3">
            <Link
              to={row.to}
              className="flex items-center justify-between gap-4 hover:underline"
              data-testid={`${row.id}-link`}
            >
              <span className="flex flex-col">
                <span className="text-sm font-medium text-brand-slate-800">{row.label}</span>
                <span className="text-xs text-brand-slate-500">
                  {t('schoolCard.finalized', { date: formatDate(row.finalizedAt) })}
                </span>
              </span>
              <PdfStatusBadge status={row.pdfStatus} />
            </Link>
          </li>
        ))}
      </ul>
    </Card>
  );
}
