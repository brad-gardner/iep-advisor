import { Fragment, type Key, type ReactNode } from 'react';
import { EmptyHint } from './empty-hint';
import { HomeSection } from './home-section';

interface ListSectionProps<T> {
  title: string;
  'data-testid': string;
  items: T[];
  emptyHint: string;
  itemKey: (item: T) => Key;
  renderRow: (item: T) => ReactNode;
}

/**
 * The shared "titled card with an empty-safe list of work-item rows" scaffold
 * used by every home-page list section (drafts, due-soon, documents to
 * review, progress reports, provider requests, unsigned finalized, shared
 * drafts). Each caller supplies only its own key extraction and row
 * rendering — `renderRow` returns a `WorkItemRow` (itself the `<li>`), so
 * this wraps it in a keyed `Fragment` rather than nesting another list item.
 */
export function ListSection<T>({
  title,
  'data-testid': testId,
  items,
  emptyHint,
  itemKey,
  renderRow,
}: ListSectionProps<T>) {
  return (
    <HomeSection title={title} data-testid={testId}>
      {items.length === 0 ? (
        <EmptyHint data-testid={`${testId}-empty`}>{emptyHint}</EmptyHint>
      ) : (
        <ul className="divide-y divide-brand-slate-100">
          {items.map((item) => (
            <Fragment key={itemKey(item)}>{renderRow(item)}</Fragment>
          ))}
        </ul>
      )}
    </HomeSection>
  );
}
