import { cn } from '@/lib/cn';
import type { ImportKind } from '../types';

interface ImportKindToggleProps {
  value: ImportKind;
  onChange: (kind: ImportKind) => void;
  // Locked while a step is busy (e.g. a preview upload in flight) so a
  // response for one kind can never land on the other kind's wizard.
  disabled?: boolean;
}

const OPTIONS: { kind: ImportKind; label: string }[] = [
  { kind: 'Students', label: 'Students' },
  { kind: 'Staff', label: 'Staff' },
];

// Students | Staff switch: real radio inputs in a fieldset (native arrow-key
// group navigation) styled as a segmented control. The selected option is
// filled AND checked, so the state is never colour-only.
export function ImportKindToggle({ value, onChange, disabled = false }: ImportKindToggleProps) {
  return (
    <fieldset
      className="inline-flex gap-1 rounded-card border border-brand-slate-200 bg-brand-slate-50 p-1"
      disabled={disabled}
      data-testid="import-kind-toggle"
    >
      <legend className="sr-only">What to import</legend>
      {OPTIONS.map((option) => {
        const selected = option.kind === value;
        return (
          <label key={option.kind} className="relative">
            <input
              type="radio"
              name="import-kind"
              value={option.kind}
              checked={selected}
              onChange={() => onChange(option.kind)}
              className="peer sr-only"
              data-testid={`import-kind-${option.kind.toLowerCase()}`}
            />
            <span
              className={cn(
                'inline-flex cursor-pointer items-center justify-center rounded-button border px-3 py-1.5 text-xs font-medium leading-[1.3] transition-colors',
                'peer-focus-visible:ring-1 peer-focus-visible:ring-brand-teal-400 peer-focus-visible:ring-offset-2',
                'peer-disabled:cursor-not-allowed peer-disabled:opacity-50',
                selected
                  ? 'border-transparent bg-brand-teal-500 text-white'
                  : 'border-transparent bg-transparent text-brand-slate-600 hover:bg-brand-slate-100'
              )}
            >
              {option.label}
            </span>
          </label>
        );
      })}
    </fieldset>
  );
}
