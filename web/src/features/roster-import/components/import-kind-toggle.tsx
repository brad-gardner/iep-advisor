import { Button } from '@/components/ui/button';
import type { ImportKind } from '../types';

interface ImportKindToggleProps {
  value: ImportKind;
  onChange: (kind: ImportKind) => void;
}

const OPTIONS: { kind: ImportKind; label: string }[] = [
  { kind: 'Students', label: 'Students' },
  { kind: 'Staff', label: 'Staff' },
];

// Students | Staff switch. A radiogroup of two buttons: the selected option is
// the filled variant AND carries aria-checked, so the state is never colour-only.
export function ImportKindToggle({ value, onChange }: ImportKindToggleProps) {
  return (
    <div
      role="radiogroup"
      aria-label="What to import"
      className="inline-flex gap-1 rounded-card border border-brand-slate-200 bg-brand-slate-50 p-1"
      data-testid="import-kind-toggle"
    >
      {OPTIONS.map((option) => {
        const selected = option.kind === value;
        return (
          <Button
            key={option.kind}
            type="button"
            role="radio"
            aria-checked={selected}
            size="sm"
            variant={selected ? 'primary' : 'ghost'}
            onClick={() => onChange(option.kind)}
            data-testid={`import-kind-${option.kind.toLowerCase()}`}
          >
            {option.label}
          </Button>
        );
      })}
    </div>
  );
}
