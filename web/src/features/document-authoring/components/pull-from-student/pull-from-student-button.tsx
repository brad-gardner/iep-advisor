import { useEffect, useRef, useState } from 'react';
import { UserRoundCheck } from 'lucide-react';
import { useToast } from '@/components/ui/toast';
import type { StudentShareableEntries } from '../../hooks/use-student-shareable-entries';
import { StudentEntryPicker } from './student-entry-picker';

interface PullFromStudentButtonProps {
  /** The editor's shared entries cache (one fetch per document session). */
  source: StudentShareableEntries;
  // Copies the picked entry content into the field via the SAME edit/patch +
  // autosave path used by typing and AI-assist accept. This makes the pulled
  // text an independent snapshot (a plain copy), not a live link.
  onPick: (content: string) => void;
  testIdPrefix: string;
}

// Educator affordance: open a picker of the student's shareable workspace
// entries and copy one into the current field.
export function PullFromStudentButton({
  source,
  onPick,
  testIdPrefix,
}: PullFromStudentButtonProps) {
  const { show } = useToast();
  const { entries, isLoading, isError, ensureLoaded } = source;
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);

  const handleToggle = () => {
    const next = !open;
    setOpen(next);
    if (next) void ensureLoaded();
  };

  // Esc closes (returning focus to the trigger); a click outside dismisses.
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.stopPropagation();
        setOpen(false);
        triggerRef.current?.focus();
      }
    };
    const onPointer = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('keydown', onKey);
    document.addEventListener('mousedown', onPointer);
    return () => {
      document.removeEventListener('keydown', onKey);
      document.removeEventListener('mousedown', onPointer);
    };
  }, [open]);

  const handlePick = (content: string) => {
    onPick(content);
    setOpen(false);
    show({ message: 'Pulled from student', variant: 'success' });
  };

  return (
    <div ref={containerRef} className="relative inline-block">
      <button
        ref={triggerRef}
        type="button"
        onClick={handleToggle}
        aria-haspopup="listbox"
        aria-expanded={open}
        className="inline-flex items-center gap-1.5 rounded-button border border-brand-slate-200 px-2.5 py-1 text-[13px] font-medium text-brand-slate-600 transition-colors hover:bg-brand-slate-100"
        data-testid={`${testIdPrefix}-button`}
      >
        <UserRoundCheck className="h-3.5 w-3.5" strokeWidth={1.8} aria-hidden="true" />
        Pull from student
      </button>
      {open && (
        <StudentEntryPicker
          entries={entries}
          isLoading={isLoading}
          error={isError}
          onPick={handlePick}
          testIdPrefix={testIdPrefix}
        />
      )}
    </div>
  );
}
