import { Badge } from '@/components/ui/badge';
import { formatDate } from '@/lib/format-date';
import { ListSection } from './list-section';
import { WorkItemRow } from './work-item-row';
import type { ParentDocumentDto } from '../types';

/** Parent "Documents to review": finalized versions now, plan-6 shared drafts
 * ([] until then). `linkPath` is server-provided, so no client-side route
 * guessing is needed. */
export function DocumentsToReviewSection({ items }: { items: ParentDocumentDto[] }) {
  return (
    <ListSection
      title="Documents to review"
      data-testid="home-documents-to-review"
      items={items}
      emptyHint="No documents waiting on your review."
      itemKey={(doc) => `${doc.kind}-${doc.id}`}
      renderRow={(doc) => (
        <WorkItemRow
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
      )}
    />
  );
}
