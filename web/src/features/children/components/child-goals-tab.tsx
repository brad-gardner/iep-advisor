import { useOutletContext } from "react-router-dom";
import { Card } from "@/components/ui/card";
import { useAdvocacyGoals } from "@/features/advocacy-goals/hooks/use-advocacy-goals";
import { AdvocacyGoalsList } from "@/features/advocacy-goals/components/advocacy-goals-list";
import type { ChildOutletContext } from "./child-detail-page";

export function ChildGoalsTab() {
  const { child, childId } = useOutletContext<ChildOutletContext>();
  const {
    goals,
    isLoading: goalsLoading,
    reload: reloadGoals,
  } = useAdvocacyGoals(childId);
  const isViewer = child.role === "viewer";

  return (
    <Card data-testid="advocacy-goals-section">
      <h2 className="font-serif mb-4">Your Advocacy Goals</h2>
      <AdvocacyGoalsList
        childId={childId}
        childName={child.firstName}
        goals={goals}
        isLoading={goalsLoading}
        onReload={reloadGoals}
        readOnly={isViewer}
      />
    </Card>
  );
}
