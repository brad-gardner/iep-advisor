import {
  HelpCircle,
  AlertTriangle,
  ClipboardList,
  RefreshCw,
} from "lucide-react";
import type { MeetingPrepChecklist, CheckItemRequest } from "@/types/api";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Notice } from "@/components/ui/notice";
import { ChecklistSection } from "./checklist-section";
import { MeetingPrepEmptyState } from "./meeting-prep-empty-state";
import { checkItem } from "../api/meeting-prep-api";
import { useState } from "react";

interface MeetingPrepTabProps {
  checklist: MeetingPrepChecklist | null;
  isLoading: boolean;
  isGenerating: boolean;
  onGenerate: () => void;
  onReload?: () => void;
  analysisCreatedAt?: string | null;
}

const SECTIONS = [
  { key: "questionsToAsk", title: "Questions to Ask", icon: HelpCircle },
  { key: "redFlagsToRaise", title: "Red Flags to Raise", icon: AlertTriangle },
  {
    key: "preparationNotes",
    title: "Preparation Notes",
    icon: ClipboardList,
  },
] as const;

// Legacy sections from older checklists — rendered if they have data
const LEGACY_SECTIONS = [
  { key: "documentsToBring", title: "Documents to Bring" },
  { key: "rightsToReference", title: "Rights to Reference" },
  { key: "goalGaps", title: "Goal Gaps" },
  { key: "generalTips", title: "General Tips" },
] as const;

type SectionKey = keyof Pick<
  MeetingPrepChecklist,
  | "questionsToAsk"
  | "redFlagsToRaise"
  | "preparationNotes"
  | "documentsToBring"
  | "rightsToReference"
  | "goalGaps"
  | "generalTips"
>;

function getAllItems(checklist: MeetingPrepChecklist) {
  const items = [
    ...checklist.questionsToAsk,
    ...checklist.redFlagsToRaise,
    ...(checklist.preparationNotes ?? []),
  ];
  // Include legacy sections if they have data (old checklists)
  for (const s of LEGACY_SECTIONS) {
    const legacyItems = checklist[s.key];
    if (legacyItems?.length) {
      items.push(...legacyItems);
    }
  }
  return items;
}

export function MeetingPrepTab(props: MeetingPrepTabProps) {
  const { checklist, isLoading, isGenerating, onGenerate, analysisCreatedAt } =
    props;
  const [localChecklist, setLocalChecklist] =
    useState<MeetingPrepChecklist | null>(null);
  const [showRegenerateConfirm, setShowRegenerateConfirm] = useState(false);

  // Use local state for optimistic check updates, fall back to prop
  const displayChecklist = localChecklist ?? checklist;

  // Sync when prop changes
  if (checklist && localChecklist && checklist.id !== localChecklist.id) {
    setLocalChecklist(null);
  }

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (
    !displayChecklist ||
    (!displayChecklist.id && displayChecklist.status !== "generating")
  ) {
    return (
      <MeetingPrepEmptyState
        onGenerate={onGenerate}
        isGenerating={isGenerating}
      />
    );
  }

  if (
    displayChecklist.status === "generating" ||
    displayChecklist.status === "pending"
  ) {
    return (
      <div className="flex flex-col items-center justify-center py-16 px-4">
        <div className="animate-spin rounded-full h-10 w-10 border-b-2 border-brand-teal-500 mb-4" />
        <h3 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-2">
          Generating Your Checklist
        </h3>
        <p className="text-brand-slate-400 text-sm text-center max-w-md mb-6">
          Building a personalized meeting prep checklist. This typically takes
          30-60 seconds.
        </p>
      </div>
    );
  }

  if (displayChecklist.status === "error") {
    return (
      <div className="flex flex-col items-center justify-center py-16 px-4">
        <Card className="max-w-md text-center">
          <Notice variant="error" title="Generation Failed">
            {displayChecklist.errorMessage ||
              "An error occurred while generating the checklist."}
          </Notice>
          <div className="mt-4">
            <Button onClick={onGenerate} disabled={isGenerating}>
              {isGenerating ? "Retrying..." : "Retry"}
            </Button>
          </div>
        </Card>
      </div>
    );
  }

  // Completed state
  const allItems = getAllItems(displayChecklist);
  const totalCount = allItems.length;
  const checkedCount = allItems.filter((i) => i.isChecked).length;
  const progressPercent =
    totalCount > 0 ? Math.round((checkedCount / totalCount) * 100) : 0;

  // Check if prep is stale (generated before latest analysis)
  const isStalePrep =
    analysisCreatedAt &&
    displayChecklist.createdAt &&
    new Date(analysisCreatedAt) > new Date(displayChecklist.createdAt);

  const handleCheck = async (
    sectionKey: SectionKey,
    index: number,
    isChecked: boolean,
  ) => {
    // Optimistic update
    const updated = { ...displayChecklist };
    const items = [...updated[sectionKey]];
    items[index] = { ...items[index], isChecked };
    (updated as Record<string, unknown>)[sectionKey] = items;
    setLocalChecklist(updated as MeetingPrepChecklist);

    // API call
    if (displayChecklist.id) {
      const request: CheckItemRequest = {
        section: sectionKey,
        index,
        isChecked,
      };
      try {
        await checkItem(displayChecklist.id, request);
      } catch {
        // Revert on error
        setLocalChecklist(null);
      }
    }
  };

  const handleRegenerate = () => {
    setShowRegenerateConfirm(false);
    onGenerate();
  };

  // Determine which legacy sections have data
  const activeLegacySections = LEGACY_SECTIONS.filter(
    (s) => displayChecklist[s.key]?.length > 0,
  );

  return (
    <div className="space-y-6">
      {/* Stale prep banner */}
      {isStalePrep && (
        <Notice variant="warning" title="Meeting prep may be outdated">
          This checklist was generated before the latest analysis. Regenerate to
          include updated insights.
        </Notice>
      )}

      {/* Progress bar + regenerate */}
      <div className="space-y-2" data-testid="meeting-prep-progress">
        <div className="flex items-center justify-between">
          <span className="text-[13px] font-medium text-brand-slate-600">
            {checkedCount} of {totalCount} items checked
          </span>
          <div className="flex items-center gap-3">
            <span className="text-[13px] font-medium text-brand-teal-500">
              {progressPercent}%
            </span>
            <Button
              variant="ghost"
              onClick={() => setShowRegenerateConfirm(true)}
              className="text-[12px] gap-1"
              data-testid="regenerate-prep-button"
            >
              <RefreshCw className="w-3.5 h-3.5" strokeWidth={1.8} />
              Regenerate
            </Button>
          </div>
        </div>
        <div className="h-2 bg-brand-slate-100 rounded-full overflow-hidden">
          <div
            className="h-full bg-brand-teal-500 rounded-full transition-all duration-300"
            style={{ width: `${progressPercent}%` }}
          />
        </div>
      </div>

      {/* Regenerate confirmation */}
      {showRegenerateConfirm && (
        <Notice variant="warning" title="Regenerate checklist?">
          <p className="mb-3">
            This will create a new checklist. Your current progress will not be
            carried over.
          </p>
          <div className="flex gap-2">
            <Button onClick={handleRegenerate} disabled={isGenerating}>
              {isGenerating ? "Generating..." : "Regenerate"}
            </Button>
            <Button
              variant="ghost"
              onClick={() => setShowRegenerateConfirm(false)}
            >
              Cancel
            </Button>
          </div>
        </Notice>
      )}

      {/* New sections */}
      {SECTIONS.map(({ key, title, icon }) => {
        const items = displayChecklist[key] ?? [];
        if (!items.length) return null;
        return (
          <ChecklistSection
            key={key}
            title={title}
            icon={icon}
            items={items}
            section={key}
            onCheck={(index, isChecked) => handleCheck(key, index, isChecked)}
          />
        );
      })}

      {/* Legacy sections — only render if they have data */}
      {activeLegacySections.map(({ key, title }) => (
        <ChecklistSection
          key={key}
          title={title}
          icon={ClipboardList}
          items={displayChecklist[key]}
          section={key}
          onCheck={(index, isChecked) => handleCheck(key, index, isChecked)}
        />
      ))}
    </div>
  );
}
