import { Trash2 } from 'lucide-react';

interface RowDeleteButtonProps {
  onDelete: () => void;
  label: string;
  testId: string;
}

export function RowDeleteButton({ onDelete, label, testId }: RowDeleteButtonProps) {
  return (
    <button
      type="button"
      onClick={onDelete}
      className="inline-flex items-center gap-1 text-xs text-brand-danger-700 hover:underline"
      data-testid={testId}
      aria-label={label}
    >
      <Trash2 className="w-3.5 h-3.5" aria-hidden="true" /> Delete
    </button>
  );
}
