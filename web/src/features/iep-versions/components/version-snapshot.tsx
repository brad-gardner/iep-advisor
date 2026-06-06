import { Card } from '@/components/ui/card';
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
  if (sections.length === 0) return null;
  return (
    <section data-testid="snapshot-sections" className="space-y-4">
      {sections.map((s) => (
        <Card key={s.id}>
          <h3 className="text-sm font-medium text-brand-slate-700 mb-1">{s.sectionKind}</h3>
          <p className="text-sm text-brand-slate-600 whitespace-pre-wrap">
            {s.richText || <span className="text-brand-slate-400">—</span>}
          </p>
        </Card>
      ))}
    </section>
  );
}

function GoalsBlock({ goals }: { goals: IepVersionDto['goals'] }) {
  if (goals.length === 0) return null;
  return (
    <section data-testid="snapshot-goals">
      <SectionHeading>Goals</SectionHeading>
      <div className="space-y-4">
        {goals.map((g) => (
          <Card key={g.id}>
            {g.domain && (
              <p className="text-xs uppercase tracking-wide text-brand-slate-400 mb-1">
                {g.domain}
              </p>
            )}
            <p className="text-sm text-brand-slate-800 whitespace-pre-wrap">
              {g.goalText || <span className="text-brand-slate-400">—</span>}
            </p>
            <dl className="mt-3 grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-1 text-sm">
              <Field label="Baseline" value={g.baseline} />
              <Field label="Target criteria" value={g.targetCriteria} />
              <Field label="Measurement" value={g.measurementMethod} />
              <Field label="Timeframe" value={g.timeframe} />
            </dl>
          </Card>
        ))}
      </div>
    </section>
  );
}

function ServicesBlock({ serviceLines }: { serviceLines: IepVersionDto['serviceLines'] }) {
  if (serviceLines.length === 0) return null;
  return (
    <section data-testid="snapshot-services">
      <SectionHeading>Services</SectionHeading>
      <Card className="overflow-x-auto p-0">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-brand-slate-500 border-b border-brand-slate-100">
              <Th>Service</Th>
              <Th>Frequency</Th>
              <Th>Duration</Th>
              <Th>Location</Th>
              <Th>Provider</Th>
              <Th>Dates</Th>
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
  if (accommodations.length === 0) return null;
  return (
    <section data-testid="snapshot-accommodations">
      <SectionHeading>Accommodations</SectionHeading>
      <Card>
        <ul className="space-y-2 text-sm">
          {accommodations.map((a) => (
            <li key={a.id} className="flex gap-2">
              {a.category && (
                <span className="text-brand-slate-400 shrink-0">{a.category}:</span>
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
  return (
    <section data-testid="snapshot-transition">
      <SectionHeading>Transition</SectionHeading>
      <div className="space-y-4">
        {items.map((t) => (
          <Card key={t.id}>
            {t.postsecondaryGoalArea && (
              <p className="text-sm font-medium text-brand-slate-700 mb-1">
                {t.postsecondaryGoalArea}
              </p>
            )}
            <p className="text-sm text-brand-slate-600 whitespace-pre-wrap">
              {t.servicesText || <span className="text-brand-slate-400">—</span>}
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
      <dt className="text-brand-slate-400">{label}</dt>
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
  const fmt = (iso: string | null) => {
    if (!iso) return '';
    const d = new Date(iso);
    return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString();
  };
  const s = fmt(start);
  const e = fmt(end);
  if (s && e) return `${s} – ${e}`;
  return s || e || '';
}
