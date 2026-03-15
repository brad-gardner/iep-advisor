import { useState, useEffect, useRef } from 'react';
import type { KnowledgeBaseEntry, CategoryCount } from '@/types/api';
import { searchKnowledgeBase, getCategories } from '../api/knowledge-base-api';

export function useKnowledgeBase() {
  const [query, setQuery] = useState('');
  const [category, setCategory] = useState<string | null>(null);
  const [entries, setEntries] = useState<KnowledgeBaseEntry[]>([]);
  const [categories, setCategories] = useState<CategoryCount[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Fetch categories on mount
  useEffect(() => {
    getCategories()
      .then(setCategories)
      .catch(() => setCategories([]));
  }, []);

  // Debounced search when query or category changes
  useEffect(() => {
    if (debounceRef.current) {
      clearTimeout(debounceRef.current);
    }

    setIsLoading(true);

    debounceRef.current = setTimeout(() => {
      searchKnowledgeBase(query || undefined, category)
        .then(setEntries)
        .catch(() => setEntries([]))
        .finally(() => setIsLoading(false));
    }, 300);

    return () => {
      if (debounceRef.current) {
        clearTimeout(debounceRef.current);
      }
    };
  }, [query, category]);

  return {
    entries,
    categories,
    isLoading,
    query,
    setQuery,
    category,
    setCategory,
  };
}
