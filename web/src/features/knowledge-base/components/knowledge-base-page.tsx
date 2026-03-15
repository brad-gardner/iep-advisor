import { Search as SearchIcon } from 'lucide-react';
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
    <div className="space-y-6 max-w-3xl">
      {/* Header */}
      <div>
        <h1 className="font-serif text-2xl text-brand-slate-800">
          Knowledge Base
        </h1>
        <p className="text-sm text-brand-slate-400 mt-1">
          Plain-language guides to IEP laws, your rights, and special education terms
        </p>
      </div>

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
          <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-brand-teal-500" />
        </div>
      ) : entries.length === 0 ? (
        <div className="text-center py-12">
          <SearchIcon
            className="mx-auto text-brand-slate-300 mb-3"
            size={32}
            strokeWidth={1.8}
            aria-hidden="true"
          />
          <p className="text-sm text-brand-slate-400">
            No entries match your search
          </p>
        </div>
      ) : (
        <div className="space-y-4">
          {entries.map((entry) => (
            <KnowledgeBaseEntryCard key={entry.id} entry={entry} />
          ))}
        </div>
      )}

      {/* Legal disclaimer */}
      <p className="text-xs text-brand-slate-400 border-t border-brand-slate-100 pt-4">
        This information is provided for educational purposes. It is not legal advice.
      </p>
    </div>
  );
}
