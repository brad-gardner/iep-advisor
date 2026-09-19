import { useEffect, useRef, useState } from "react";
import { useOutletContext, useSearchParams } from "react-router-dom";
import { useToast } from "@/components/ui/toast";
import type { ChildOutletContext } from "@/features/children/components/child-detail-page";
import { useMeetingPrep } from "../hooks/use-meeting-prep";
import { useParentQuestions } from "../hooks/use-parent-questions";
import { MeetingPrepTab } from "./meeting-prep-tab";
import { MeetingPrepDateControl } from "./meeting-prep-date-control";
import { ParentQuestions } from "./parent-questions";
import { StudentSharedEntries } from "./student-shared-entries";

/** `?addQuestion=<text>` — an advocate suggestion handed to the parent's own question list. */
export const ADD_QUESTION_PARAM = "addQuestion";

export const QUESTION_ADDED_TOAST = "Added to your questions";
export const QUESTION_EXISTS_TOAST = "Already in your questions";
export const QUESTION_FAILED_TOAST = "Could not add that question";

/**
 * Child-level (standalone) Meeting Prep tab, gated behind the
 * MeetingPrepStandalone feature flag. Reuses the child-scoped useMeetingPrep
 * (goals mode) and the shared MeetingPrepTab renderer, adding an optional
 * meeting-date control above it and the parent's own question list.
 */
export function ChildMeetingPrepTab() {
  const { child, childId } = useOutletContext<ChildOutletContext>();
  const { show } = useToast();
  const canEdit = child.role === "owner" || child.role === "collaborator";
  const { checklist, isLoading, isGenerating, generateFromGoals } =
    useMeetingPrep(childId);
  const [meetingDate, setMeetingDate] = useState("");
  const parentQuestions = useParentQuestions(childId);
  const { add: addQuestion, isLoading: questionsLoading, writeForbidden } = parentQuestions;
  const canEditQuestions = canEdit && !writeForbidden;

  const [searchParams, setSearchParams] = useSearchParams();
  const incoming = searchParams.get(ADD_QUESTION_PARAM);
  const consumedRef = useRef<string | null>(null);

  // Consume the handoff once the list is known (so a repeat is recognised
  // without a round trip): add it, say so, and take it out of the URL so a
  // reload or back-navigation does not add it twice. A viewer's URL is only
  // cleaned up (nothing is added for them).
  useEffect(() => {
    if (incoming === null || questionsLoading || consumedRef.current === incoming) return;
    consumedRef.current = incoming;
    if (canEditQuestions) {
      void addQuestion(incoming, "advocate").then((result) => {
        if (result === "added") show({ message: QUESTION_ADDED_TOAST, variant: "success" });
        else if (result === "duplicate") show({ message: QUESTION_EXISTS_TOAST, variant: "info" });
        else if (result === "failed") show({ message: QUESTION_FAILED_TOAST, variant: "error" });
      });
    }
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        next.delete(ADD_QUESTION_PARAM);
        return next;
      },
      { replace: true },
    );
  }, [incoming, questionsLoading, canEditQuestions, addQuestion, setSearchParams, show]);

  const addTyped = async (text: string) => {
    const result = await addQuestion(text, "parent");
    if (result === "added") show({ message: QUESTION_ADDED_TOAST, variant: "success" });
    return result;
  };

  return (
    <div className="space-y-6">
      <StudentSharedEntries childId={childId} />
      <MeetingPrepDateControl
        meetingDate={meetingDate}
        onMeetingDateChange={setMeetingDate}
        savedMeetingDate={checklist?.meetingDate}
        isGenerating={isGenerating}
        onGenerate={() => generateFromGoals(meetingDate || undefined)}
      />
      <ParentQuestions
        questions={parentQuestions.questions}
        isLoading={questionsLoading}
        loadError={parentQuestions.loadError}
        isReordering={parentQuestions.isReordering}
        onAdd={addTyped}
        onCheck={parentQuestions.setChecked}
        onEdit={parentQuestions.updateText}
        onMove={parentQuestions.move}
        onRemove={parentQuestions.remove}
        readOnly={!canEditQuestions}
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
