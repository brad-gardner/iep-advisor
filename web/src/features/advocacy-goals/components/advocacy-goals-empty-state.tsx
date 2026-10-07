import { useTranslation } from 'react-i18next';
import { Target } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/empty-state';

interface AdvocacyGoalsEmptyStateProps {
  childName: string;
  onAdd: () => void;
}

export function AdvocacyGoalsEmptyState({ childName, onAdd }: AdvocacyGoalsEmptyStateProps) {
  const { t } = useTranslation('advocacy-goals');
  return (
    <EmptyState
      icon={Target}
      title={t('emptyState.title', { childName })}
      description={t('emptyState.description')}
      action={
        <Button onClick={onAdd} data-testid="add-goal-button">
          {t('emptyState.addFirst')}
        </Button>
      }
    />
  );
}
