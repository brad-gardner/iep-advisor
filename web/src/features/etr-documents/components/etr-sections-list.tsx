import { FileText } from 'lucide-react';
import { Spinner } from '@/components/ui/spinner';
import { Notice } from '@/components/ui/notice';
import { EmptyState } from '@/components/ui/empty-state';
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
        <Spinner size="sm" label="Loading sections…" />
      </div>
    );
  }

  if (error) {
    return <Notice variant="error" title={error} data-testid="etr-sections-error" />;
  }

  if (sections.length === 0) {
    return (
      <EmptyState
        icon={FileText}
        title="No sections parsed yet."
        data-testid="etr-sections-empty"
      />
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
