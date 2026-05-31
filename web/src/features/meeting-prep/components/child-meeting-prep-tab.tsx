import { useState } from "react";
import { useOutletContext } from "react-router-dom";
import type { ChildOutletContext } from "@/features/children/components/child-detail-page";
import { useMeetingPrep } from "../hooks/use-meeting-prep";
import { MeetingPrepTab } from "./meeting-prep-tab";
import { MeetingPrepDateControl } from "./meeting-prep-date-control";

/**
 * Child-level (standalone) Meeting Prep tab, gated behind the
 * MeetingPrepStandalone feature flag. Reuses the child-scoped useMeetingPrep
 * (goals mode) and the shared MeetingPrepTab renderer, adding an optional
 * meeting-date control above it.
 */
export function ChildMeetingPrepTab() {
  const { childId } = useOutletContext<ChildOutletContext>();
  const { checklist, isLoading, isGenerating, generateFromGoals } =
    useMeetingPrep(childId);
  const [meetingDate, setMeetingDate] = useState("");

  return (
    <div className="space-y-6">
      <MeetingPrepDateControl
        meetingDate={meetingDate}
        onMeetingDateChange={setMeetingDate}
        savedMeetingDate={checklist?.meetingDate}
        isGenerating={isGenerating}
        onGenerate={() => generateFromGoals(meetingDate || undefined)}
      />
      <MeetingPrepTab
        checklist={checklist}
        isLoading={isLoading}
        isGenerating={isGenerating}
        onGenerate={() => generateFromGoals(meetingDate || undefined)}
        hideEmptyStateCta
      />
    </div>
  );
}
