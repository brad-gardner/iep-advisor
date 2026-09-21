import { useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { Search as SearchIcon } from 'lucide-react';
import { PageLayout } from '@/components/ui/page-layout';
import { Spinner } from '@/components/ui/spinner';
import { EmptyState } from '@/components/ui/empty-state';
import { usePageTitle } from '@/hooks/use-page-title';
import { useKnowledgeBase } from '../hooks/use-knowledge-base';
import { KnowledgeBaseSearch } from './knowledge-base-search';
import { CategoryTabs } from './category-tabs';
import { KnowledgeBaseEntryCard } from './knowledge-base-entry-card';

/** DOM id of one entry's card (set by `KnowledgeBaseEntryCard`) — `/knowledge-base/:entryId` scrolls to it. */
const entryElementId = (id: number) => `kb-entry-${id}`;

export function KnowledgeBasePage() {
  usePageTitle('Knowledge Base');
  // `/knowledge-base/:entryId` (e.g. from an advocate citation) opens the
  // same list and brings that entry into view; an id the list doesn't hold
  // (inactive, another state) simply shows the list.
  const { entryId: entryIdParam } = useParams<{ entryId?: string }>();
  const targetEntryId = entryIdParam && /^\d+$/.test(entryIdParam) ? Number(entryIdParam) : null;
  const { entries, categories, isLoading, query, setQuery, category, setCategory } = useKnowledgeBase();

  useEffect(() => {
    if (targetEntryId == null || isLoading) return;
    const el = document.getElementById(entryElementId(targetEntryId));
    if (el && typeof el.scrollIntoView === 'function') el.scrollIntoView({ block: 'start' });
  }, [targetEntryId, isLoading, entries]);

  return (
    <PageLayout
      title="Knowledge Base"
      subtitle="Plain-language guides to IEP laws, your rights, and special education terms"
      className="max-w-3xl"
    >
      {/* Search */}
      <KnowledgeBaseSearch value={query} onChange={setQuery} />

      {/* Category tabs */}
      {categories.length > 0 && <CategoryTabs categories={categories} active={category} onChange={setCategory} />}

      {/* Entry list */}
      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Spinner />
        </div>
      ) : entries.length === 0 ? (
        <EmptyState icon={SearchIcon} title="No entries match your search" />
      ) : (
        <div className="space-y-4" data-testid="kb-results">
          {entries.map((entry) => (
            <KnowledgeBaseEntryCard key={entry.id} entry={entry} highlighted={entry.id === targetEntryId} />
          ))}
        </div>
      )}

      {/* Legal disclaimer */}
      <p className="text-xs text-brand-slate-500 border-t border-brand-slate-100 pt-4">
        This information is provided for educational purposes. It is not legal advice.
      </p>
    </PageLayout>
  );
}
