import { useState } from 'react';
import { UserRoundCheck } from 'lucide-react';
import { useFeatureFlag } from '@/hooks/use-feature-flags';
import { useEditorContext } from '../../hooks/use-editor-context';
import { useStudentShareableEntries } from '../../hooks/use-student-shareable-entries';
import { StudentEntryPicker } from './student-entry-picker';

interface PullFromStudentButtonProps {
  // Copies the picked entry content into the field via the SAME edit/patch +
  // autosave path used by typing and AI-assist accept. This makes the pulled
  // text an independent snapshot (a plain copy), not a live link.
  onPick: (content: string) => void;
  testIdPrefix: string;
}

// Educator affordance: open a picker of the student's shareable workspace
// entries and copy one into the current field. Gated on the StudentWorkspace
// flag — renders nothing when off.
export function PullFromStudentButton({
  onPick,
  testIdPrefix,
}: PullFromStudentButtonProps) {
  const enabled = useFeatureFlag('StudentWorkspace');
  const { studentId } = useEditorContext();
  const { entries, isLoading, isError, ensureLoaded } =
    useStudentShareableEntries(studentId);
  const [open, setOpen] = useState(false);

  if (!enabled) return null;

  const handleToggle = () => {
    setOpen((prev) => {
      const next = !prev;
      if (next) void ensureLoaded();
      return next;
    });
  };

  const handlePick = (content: string) => {
    onPick(content);
    setOpen(false);
  };

  return (
    <div className="relative inline-block">
      <button
        type="button"
        onClick={handleToggle}
        aria-haspopup="menu"
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
