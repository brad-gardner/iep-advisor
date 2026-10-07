import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { formatDate } from "@/lib/format-date";

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
  const { t } = useTranslation('meeting-prep');
  return (
    <div className="flex flex-wrap items-end gap-3">
      <div className="w-52">
        <Input
          id="meeting-prep-date"
          label={t('dateControl.label')}
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
        {t('dateControl.generate')}
      </Button>
      {savedMeetingDate && (
        <p
          className="text-[12px] text-brand-slate-500"
          data-testid="meeting-prep-saved-date"
        >
          {t('dateControl.savedDate', { date: formatDate(savedMeetingDate) })}
        </p>
      )}
    </div>
  );
}
