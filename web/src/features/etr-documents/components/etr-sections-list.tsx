import { FileText } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Spinner } from '@/components/ui/spinner';
import { Notice } from '@/components/ui/notice';
import { EmptyState } from '@/components/ui/empty-state';
import { type LoadError, loadErrorText } from '@/lib/api-error';
import type { EtrSection } from '../types';
import { EtrSectionCard } from './etr-section-card';

interface EtrSectionsListProps {
  sections: EtrSection[];
  isLoading: boolean;
  error: LoadError | null;
}

export function EtrSectionsList({ sections, isLoading, error }: EtrSectionsListProps) {
  const { t } = useTranslation(['etr-documents', 'common']);
  if (isLoading) {
    return (
      <div className="flex justify-center py-8" data-testid="etr-sections-loading">
        <Spinner size="sm" label={t('sectionsList.loading')} />
      </div>
    );
  }

  if (error) {
    const message = loadErrorText(error, t('common:ui.genericError'));
    return <Notice variant="error" title={message ?? t('common:ui.genericError')} data-testid="etr-sections-error" />;
  }

  if (sections.length === 0) {
    return (
      <EmptyState
        icon={FileText}
        title={t('sectionsList.empty')}
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
