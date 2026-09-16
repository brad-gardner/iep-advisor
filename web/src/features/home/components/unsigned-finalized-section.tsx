import { formatDate } from '@/lib/format-date';
import { EmptyHint } from './empty-hint';
import { HomeSection } from './home-section';
import { WorkItemRow } from './work-item-row';
import type { HomeUnsignedDto } from '../types';

/** SchoolAdmin/DistrictAdmin "Unsigned finalized documents" — plan 7 shape,
 * always `[]` until finalize-signing ships. Empty-safe by construction. */
export function UnsignedFinalizedSection({ items }: { items: HomeUnsignedDto[] }) {
  return (
    <HomeSection title="Unsigned finalized documents" data-testid="home-unsigned-finalized">
      {items.length === 0 ? (
        <EmptyHint data-testid="home-unsigned-finalized-empty">
          No finalized documents are waiting on a signature.
        </EmptyHint>
      ) : (
        <ul className="divide-y divide-brand-slate-100">
          {items.map((item) => (
            <WorkItemRow
              key={item.versionId}
              title={item.studentName}
              subtitle={`Finalized ${formatDate(item.finalizedAt)}`}
              href={`/educator/students/${item.studentId}/authored-versions/${item.versionId}`}
              data-testid={`home-unsigned-finalized-${item.versionId}`}
            />
          ))}
        </ul>
      )}
    </HomeSection>
  );
}
