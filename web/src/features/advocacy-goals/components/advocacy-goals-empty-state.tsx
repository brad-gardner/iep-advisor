import { Target } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface AdvocacyGoalsEmptyStateProps {
  childName: string;
  onAdd: () => void;
}

export function AdvocacyGoalsEmptyState({ childName, onAdd }: AdvocacyGoalsEmptyStateProps) {
  return (
    <div className="text-center py-8 px-4">
      <div className="w-12 h-12 mx-auto mb-3 bg-brand-teal-50 rounded-full flex items-center justify-center">
        <Target className="text-brand-teal-500" size={24} strokeWidth={1.8} aria-hidden="true" />
      </div>
      <h3 className="font-serif text-brand-slate-800 mb-1">
        Define your priorities for {childName}
      </h3>
      <p className="text-xs text-brand-slate-400 max-w-sm mx-auto mb-4">
        When you analyze an IEP, we'll check whether these goals are addressed
        and flag any gaps.
      </p>
      <Button onClick={onAdd}>Add Your First Goal</Button>
    </div>
  );
}
