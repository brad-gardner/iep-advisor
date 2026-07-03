import { Search as SearchIcon } from 'lucide-react';
import { PageLayout } from '@/components/ui/page-layout';
import { Spinner } from '@/components/ui/spinner';
import { EmptyState } from '@/components/ui/empty-state';
import { useKnowledgeBase } from '../hooks/use-knowledge-base';
import { KnowledgeBaseSearch } from './knowledge-base-search';
import { CategoryTabs } from './category-tabs';
import { KnowledgeBaseEntryCard } from './knowledge-base-entry-card';

export function KnowledgeBasePage() {
  const {
    entries,
    categories,
    isLoading,
    query,
    setQuery,
    category,
    setCategory,
  } = useKnowledgeBase();

  return (
    <PageLayout
      title="Knowledge Base"
      subtitle="Plain-language guides to IEP laws, your rights, and special education terms"
      className="max-w-3xl"
    >
      {/* Search */}
      <KnowledgeBaseSearch value={query} onChange={setQuery} />

      {/* Category tabs */}
      {categories.length > 0 && (
        <CategoryTabs
          categories={categories}
          active={category}
          onChange={setCategory}
        />
      )}

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
            <KnowledgeBaseEntryCard key={entry.id} entry={entry} />
          ))}
        </div>
      )}

      {/* Legal disclaimer */}
      <p className="text-xs text-brand-slate-400 border-t border-brand-slate-100 pt-4">
        This information is provided for educational purposes. It is not legal advice.
      </p>
    </PageLayout>
  );
}
