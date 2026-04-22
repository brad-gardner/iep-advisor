import type { EtrSection } from '../types';
import { EtrSectionCard } from './etr-section-card';

interface EtrSectionsListProps {
  sections: EtrSection[];
  isLoading: boolean;
  error: string | null;
}

export function EtrSectionsList({ sections, isLoading, error }: EtrSectionsListProps) {
  if (isLoading) {
    return (
      <div className="flex justify-center py-8" data-testid="etr-sections-loading">
        <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (error) {
    return (
      <p className="text-sm text-brand-red" data-testid="etr-sections-error">
        {error}
      </p>
    );
  }

  if (sections.length === 0) {
    return (
      <p className="text-sm text-brand-slate-400" data-testid="etr-sections-empty">
        No sections parsed yet.
      </p>
    );
  }

  return (
    <div className="space-y-3" data-testid="etr-sections-list">
      {sections.map((section, idx) => (
        <EtrSectionCard key={section.id} section={section} defaultOpen={idx === 0} />
      ))}
    </div>
  );
}
