import { Button } from "@/components/ui/button";

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
      <div className="flex flex-col gap-1">
        <label
          htmlFor="meeting-prep-date"
          className="text-[12px] font-medium text-brand-slate-600"
        >
          Meeting date (optional)
        </label>
        <input
          id="meeting-prep-date"
          type="date"
          value={meetingDate}
          onChange={(e) => onMeetingDateChange(e.target.value)}
          data-testid="meeting-prep-date-input"
          className="rounded-button border border-brand-slate-200 px-3 py-2 text-[13px] text-brand-slate-800 focus:border-brand-teal-500 focus:outline-none"
        />
      </div>
      <Button
        onClick={onGenerate}
        disabled={isGenerating}
        data-testid="meeting-prep-generate-button"
      >
        {isGenerating ? "Generating..." : "Generate"}
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
