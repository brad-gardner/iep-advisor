import { useTranslation } from 'react-i18next';
import { Card } from '@/components/ui/card';
import { Markdown } from '@/components/ui/markdown';
import { formatDate } from '@/lib/format-date';
import type { IepVersionDto } from '../types';

interface VersionSnapshotProps {
  version: IepVersionDto;
}

// Read-only render of an immutable IEP version snapshot. Composed of small,
// section-scoped pieces below. No editing — versions are immutable.
export function VersionSnapshot({ version }: VersionSnapshotProps) {
  return (
    <div className="space-y-6">
      <SectionsBlock sections={version.sections} />
      <GoalsBlock goals={version.goals} />
      <ServicesBlock serviceLines={version.serviceLines} />
      <AccommodationsBlock accommodations={version.accommodations} />
      {version.transitionItems.length > 0 && (
        <TransitionBlock items={version.transitionItems} />
      )}
    </div>
  );
}

function SectionHeading({ children }: { children: React.ReactNode }) {
  return <h2 className="font-serif text-lg mb-3">{children}</h2>;
}

function SectionsBlock({ sections }: { sections: IepVersionDto['sections'] }) {
  // `s.sectionKind` below is the district's own template-authored section
  // title (district content, like `orgRoleLabel`'s stored value) — NOT
  // translated, same as the rest of this frozen snapshot's values.
  if (sections.length === 0) return null;
  return (
    <section data-testid="snapshot-sections" className="space-y-4">
      {sections.map((s) => (
        <Card key={s.id}>
          <h3 className="text-sm font-medium text-brand-slate-700 mb-1">{s.sectionKind}</h3>
          {s.richText ? (
            <Markdown content={s.richText} className="text-sm text-brand-slate-600" />
          ) : (
            <p className="text-sm text-brand-slate-500">—</p>
          )}
        </Card>
      ))}
    </section>
  );
}

function GoalsBlock({ goals }: { goals: IepVersionDto['goals'] }) {
  const { t } = useTranslation('iep-versions');
  if (goals.length === 0) return null;
  return (
    <section data-testid="snapshot-goals">
      <SectionHeading>{t('snapshot.goals')}</SectionHeading>
      <div className="space-y-4">
        {goals.map((g) => (
          <Card key={g.id}>
            {g.domain && (
              <p className="text-xs uppercase tracking-wide text-brand-slate-500 mb-1">
                {g.domain}
              </p>
            )}
            <p className="text-sm text-brand-slate-800 whitespace-pre-wrap">
              {g.goalText || <span className="text-brand-slate-500">—</span>}
            </p>
            <dl className="mt-3 grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-1 text-sm">
              <Field label={t('snapshot.baseline')} value={g.baseline} />
              <Field label={t('snapshot.targetCriteria')} value={g.targetCriteria} />
              <Field label={t('snapshot.measurement')} value={g.measurementMethod} />
              <Field label={t('snapshot.timeframe')} value={g.timeframe} />
            </dl>
          </Card>
        ))}
      </div>
    </section>
  );
}

function ServicesBlock({ serviceLines }: { serviceLines: IepVersionDto['serviceLines'] }) {
  const { t } = useTranslation('iep-versions');
  if (serviceLines.length === 0) return null;
  return (
    <section data-testid="snapshot-services">
      <SectionHeading>{t('snapshot.services')}</SectionHeading>
      <Card className="overflow-x-auto p-0">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-brand-slate-500 border-b border-brand-slate-100">
              <Th>{t('snapshot.serviceColumn')}</Th>
              <Th>{t('snapshot.frequencyColumn')}</Th>
              <Th>{t('snapshot.durationColumn')}</Th>
              <Th>{t('snapshot.locationColumn')}</Th>
              <Th>{t('snapshot.providerColumn')}</Th>
              <Th>{t('snapshot.datesColumn')}</Th>
            </tr>
          </thead>
          <tbody>
            {serviceLines.map((s) => (
              <tr key={s.id} className="border-b border-brand-slate-50">
                <Td>{s.serviceType}</Td>
                <Td>{s.frequency}</Td>
                <Td>{s.duration}</Td>
                <Td>{s.location}</Td>
                <Td>{s.providerRole}</Td>
                <Td>{formatRange(s.startDate, s.endDate)}</Td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
    </section>
  );
}

function AccommodationsBlock({
  accommodations,
}: {
  accommodations: IepVersionDto['accommodations'];
}) {
  const { t } = useTranslation('iep-versions');
  if (accommodations.length === 0) return null;
  return (
    <section data-testid="snapshot-accommodations">
      <SectionHeading>{t('snapshot.accommodations')}</SectionHeading>
      <Card>
        <ul className="space-y-2 text-sm">
          {accommodations.map((a) => (
            <li key={a.id} className="flex gap-2">
              {a.category && (
                <span className="text-brand-slate-500 shrink-0">{a.category}:</span>
              )}
              <span className="text-brand-slate-700">{a.text || '—'}</span>
            </li>
          ))}
        </ul>
      </Card>
    </section>
  );
}

function TransitionBlock({ items }: { items: IepVersionDto['transitionItems'] }) {
  const { t } = useTranslation('iep-versions');
  return (
    <section data-testid="snapshot-transition">
      <SectionHeading>{t('snapshot.transition')}</SectionHeading>
      <div className="space-y-4">
        {items.map((item) => (
          <Card key={item.id}>
            {item.postsecondaryGoalArea && (
              <p className="text-sm font-medium text-brand-slate-700 mb-1">
                {item.postsecondaryGoalArea}
              </p>
            )}
            <p className="text-sm text-brand-slate-600 whitespace-pre-wrap">
              {item.servicesText || <span className="text-brand-slate-500">—</span>}
            </p>
          </Card>
        ))}
      </div>
    </section>
  );
}

function Field({ label, value }: { label: string; value: string | null }) {
  if (!value) return null;
  return (
    <div>
      <dt className="text-brand-slate-500">{label}</dt>
      <dd className="text-brand-slate-700">{value}</dd>
    </div>
  );
}

function Th({ children }: { children: React.ReactNode }) {
  return <th className="px-4 py-2 font-medium">{children}</th>;
}

function Td({ children }: { children: React.ReactNode }) {
  return <td className="px-4 py-2 text-brand-slate-700">{children || '—'}</td>;
}

function formatRange(start: string | null, end: string | null): string {
  const s = start ? formatDate(start) : '';
  const e = end ? formatDate(end) : '';
  if (s && e) return `${s} – ${e}`;
  return s || e || '';
}
