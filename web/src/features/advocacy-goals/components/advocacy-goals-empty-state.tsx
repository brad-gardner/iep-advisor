import { Target } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/empty-state';

interface AdvocacyGoalsEmptyStateProps {
  childName: string;
  onAdd: () => void;
}

export function AdvocacyGoalsEmptyState({ childName, onAdd }: AdvocacyGoalsEmptyStateProps) {
  return (
    <EmptyState
      icon={Target}
      title={`Define your priorities for ${childName}`}
      description="When you analyze an IEP, we'll check whether these goals are addressed and flag any gaps."
      action={
        <Button onClick={onAdd} data-testid="add-goal-button">
          Add Your First Goal
        </Button>
      }
    />
  );
}
