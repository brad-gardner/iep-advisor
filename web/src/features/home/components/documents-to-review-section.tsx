import { Badge } from '@/components/ui/badge';
import { formatDate } from '@/lib/format-date';
import { EmptyHint } from './empty-hint';
import { HomeSection } from './home-section';
import { WorkItemRow } from './work-item-row';
import type { ParentDocumentDto } from '../types';

/** Parent "Documents to review": finalized versions now, plan-6 shared drafts
 * ([] until then). `linkPath` is server-provided, so no client-side route
 * guessing is needed. */
export function DocumentsToReviewSection({ items }: { items: ParentDocumentDto[] }) {
  return (
    <HomeSection title="Documents to review" data-testid="home-documents-to-review">
      {items.length === 0 ? (
        <EmptyHint data-testid="home-documents-to-review-empty">
          No documents waiting on your review.
        </EmptyHint>
      ) : (
        <ul className="divide-y divide-brand-slate-100">
          {items.map((doc) => (
            <WorkItemRow
              key={`${doc.kind}-${doc.id}`}
              title={`${doc.documentTypeDisplayName}${doc.versionNumber ? ` v${doc.versionNumber}` : ''}`}
              subtitle={doc.childName}
              href={doc.linkPath}
              data-testid={`home-documents-to-review-${doc.kind}-${doc.id}`}
              meta={
                <>
                  <Badge variant={doc.kind === 'Finalized' ? 'success' : 'info'}>
                    {doc.kind === 'Finalized' ? 'Finalized' : 'Shared draft'}
                  </Badge>
                  {formatDate(doc.date)}
                </>
              }
            />
          ))}
        </ul>
      )}
    </HomeSection>
  );
}
