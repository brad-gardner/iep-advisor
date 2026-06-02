import { Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface AddRowButtonProps {
  onClick: () => void;
  label: string;
  testId: string;
}

export function AddRowButton({ onClick, label, testId }: AddRowButtonProps) {
  return (
    <Button variant="secondary" onClick={onClick} data-testid={testId}>
      <Plus className="w-4 h-4 mr-1" aria-hidden="true" />
      {label}
    </Button>
  );
}
