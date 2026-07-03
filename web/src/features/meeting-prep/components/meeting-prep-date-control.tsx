import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

// Format a date-only or ISO datetime string as a local calendar date without
// the UTC-parse off-by-one that `new Date("YYYY-MM-DD")` causes in negative
// timezone offsets.
function formatMeetingDate(value: string): string {
  const [y, m, d] = value.slice(0, 10).split("-").map(Number);
  if (!y || !m || !d) return value;
  return new Date(y, m - 1, d).toLocaleDateString();
}

interface MeetingPrepDateControlProps {
  meetingDate: string;
  onMeetingDateChange: (value: string) => void;
  savedMeetingDate?: string | null;
  isGenerating: boolean;
  onGenerate: () => void;
}

/**
 * Optional meeting-date input + Generate button shown above the reused
 * MeetingPrepTab on the child-level Meeting Prep tab. Lets a parent pick a
 * meeting date before generating a goals-based checklist.
 */
export function MeetingPrepDateControl({
  meetingDate,
  onMeetingDateChange,
  savedMeetingDate,
  isGenerating,
  onGenerate,
}: MeetingPrepDateControlProps) {
  return (
    <div className="flex flex-wrap items-end gap-3">
      <div className="w-52">
        <Input
          id="meeting-prep-date"
          label="Meeting date (optional)"
          type="date"
          value={meetingDate}
          onChange={(e) => onMeetingDateChange(e.target.value)}
          data-testid="meeting-prep-date-input"
        />
      </div>
      <Button
        onClick={onGenerate}
        loading={isGenerating}
        data-testid="meeting-prep-generate-button"
      >
        Generate
      </Button>
      {savedMeetingDate && (
        <p
          className="text-[12px] text-brand-slate-400"
          data-testid="meeting-prep-saved-date"
        >
          Current checklist meeting date: {formatMeetingDate(savedMeetingDate)}
        </p>
      )}
    </div>
  );
}
