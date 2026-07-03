import { entryKindLabel } from '@/features/student/lib/entry-kinds';
import type { StudentWorkspaceEntryDto } from '@/features/student/types';

interface StudentEntryPickerProps {
  entries: StudentWorkspaceEntryDto[];
  isLoading: boolean;
  error: boolean;
  onPick: (content: string) => void;
  testIdPrefix: string;
}

// Small dropdown listing the student's shareable entries. Picking one calls
// onPick with the entry content (the caller copies it into the field).
export function StudentEntryPicker({
  entries,
  isLoading,
  error,
  onPick,
  testIdPrefix,
}: StudentEntryPickerProps) {
  return (
    <div
      role="menu"
      className="absolute z-10 mt-1 max-h-80 w-80 overflow-y-auto rounded-card border-[0.5px] border-brand-slate-200 bg-white p-1 shadow-lg"
      data-testid={`${testIdPrefix}-picker`}
    >
      {isLoading && (
        <p className="px-3 py-2 text-sm text-brand-slate-400" data-testid={`${testIdPrefix}-loading`}>
          Loading shared entries…
        </p>
      )}

      {!isLoading && error && (
        <p className="px-3 py-2 text-sm text-brand-danger-700" data-testid={`${testIdPrefix}-error`}>
          Could not load shared entries.
        </p>
      )}

      {!isLoading && !error && entries.length === 0 && (
        <p className="px-3 py-2 text-sm italic text-brand-slate-400" data-testid={`${testIdPrefix}-empty`}>
          No shared entries yet.
        </p>
      )}

      {!isLoading &&
        !error &&
        entries.map((entry) => (
          <button
            key={entry.id}
            type="button"
            role="menuitem"
            onClick={() => onPick(entry.content)}
            className="block w-full rounded-button px-3 py-2 text-left transition-colors hover:bg-brand-teal-50"
            data-testid={`${testIdPrefix}-option-${entry.id}`}
          >
            <span className="block text-[11px] font-medium uppercase tracking-wide text-brand-teal-600">
              {entryKindLabel(entry.entryKind)}
            </span>
            <span className="mt-0.5 block whitespace-pre-wrap text-sm text-brand-slate-800">
              {entry.content}
            </span>
          </button>
        ))}
    </div>
  );
}
