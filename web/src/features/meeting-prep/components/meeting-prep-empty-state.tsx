import { ClipboardCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/empty-state';

interface MeetingPrepEmptyStateProps {
  onGenerate: () => void;
  isGenerating: boolean;
  contextLabel?: 'IEP' | 'ETR';
  // When the generate affordance is provided elsewhere (e.g. the standalone
  // tab's date control), suppress this empty state's own button.
  hideCta?: boolean;
}

export function MeetingPrepEmptyState({
  onGenerate,
  isGenerating,
  contextLabel = 'IEP',
  hideCta = false,
}: MeetingPrepEmptyStateProps) {
  const { t } = useTranslation('meeting-prep');
  const source = contextLabel === 'ETR' ? t('emptyState.descriptionEtr') : t('emptyState.descriptionIep');
  return (
    <EmptyState
      icon={ClipboardCheck}
      title={t('emptyState.title')}
      description={t('emptyState.description', { source })}
      action={
        hideCta ? undefined : (
          <Button onClick={onGenerate} loading={isGenerating} data-testid="generate-meeting-prep">
            {t('emptyState.generate')}
          </Button>
        )
      }
    />
  );
}
