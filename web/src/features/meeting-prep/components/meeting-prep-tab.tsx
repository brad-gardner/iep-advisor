import {
  HelpCircle,
  AlertTriangle,
  ClipboardList,
  RefreshCw,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import type { MeetingPrepChecklist, CheckItemRequest } from "@/types/api";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { formatDate } from "@/lib/format-date";
import { GeneratedLanguageNotice } from "@/lib/i18n/generated-language-notice";
import { Notice } from "@/components/ui/notice";
import { Spinner } from "@/components/ui/spinner";
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
  contextLabel?: 'IEP' | 'ETR';
  // Suppress the empty-state's own generate button when a generate affordance
  // is provided elsewhere (e.g. the standalone tab's date control).
  hideEmptyStateCta?: boolean;
}

const SECTIONS = [
  { key: "questionsToAsk", icon: HelpCircle },
  { key: "redFlagsToRaise", icon: AlertTriangle },
  { key: "preparationNotes", icon: ClipboardList },
] as const;

// Legacy sections from older checklists — rendered if they have data
const LEGACY_SECTIONS = [
  { key: "documentsToBring" },
  { key: "rightsToReference" },
  { key: "goalGaps" },
  { key: "generalTips" },
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
  const { t } = useTranslation(['meeting-prep', 'common']);
  const {
    checklist,
    isLoading,
    isGenerating,
    onGenerate,
    analysisCreatedAt,
    contextLabel,
    hideEmptyStateCta,
  } = props;
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
        <Spinner label={t('tab.loading')} />
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
        contextLabel={contextLabel}
        hideCta={hideEmptyStateCta}
      />
    );
  }

  if (
    displayChecklist.status === "generating" ||
    displayChecklist.status === "pending"
  ) {
    return (
      <div className="flex flex-col items-center justify-center py-16 px-4">
        <Spinner size="lg" className="mb-4" label={t('tab.generatingSpinner')} />
        <h3 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-2">
          {t('tab.generatingTitle')}
        </h3>
        <p className="text-brand-slate-500 text-sm text-center max-w-md mb-6">
          {t('tab.generatingBody')}
        </p>
      </div>
    );
  }

  if (displayChecklist.status === "error") {
    return (
      <div className="flex flex-col items-center justify-center py-16 px-4">
        <Card className="max-w-md text-center">
          <Notice variant="error" title={t('tab.generationFailedTitle')}>
            {displayChecklist.errorMessage || t('tab.genericGenerationError')}
          </Notice>
          <div className="mt-4">
            <Button onClick={onGenerate} loading={isGenerating}>
              {t('tab.retry')}
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
      <GeneratedLanguageNotice generatedLanguage={displayChecklist.generatedLanguage} />

      {/* Stale prep banner */}
      {isStalePrep && (
        <Notice variant="warning" title={t('tab.staleTitle')}>
          {t('tab.staleBody')}
        </Notice>
      )}

      {/* What the checklist was grounded in */}
      <p className="text-[13px] text-brand-slate-600" data-testid="meeting-prep-source">
        {displayChecklist.iepDocumentId
          ? displayChecklist.iepDocumentDate
            ? t('tab.sourceIepDated', { date: formatDate(displayChecklist.iepDocumentDate.slice(0, 10)) })
            : t('tab.sourceIep')
          : displayChecklist.etrDocumentId
            ? t('tab.sourceEtr')
            : t('tab.sourceGoalsOnly')}
      </p>

      {/* Progress bar + regenerate */}
      <div className="space-y-2" data-testid="meeting-prep-progress">
        <div className="flex items-center justify-between">
          <span className="text-[13px] font-medium text-brand-slate-600">
            {t('tab.itemsChecked', { checked: checkedCount, total: totalCount })}
          </span>
          <div className="flex items-center gap-3">
            <span className="text-[13px] font-medium text-brand-teal-500">
              {t('tab.percent', { percent: progressPercent })}
            </span>
            <Button
              variant="ghost"
              onClick={() => setShowRegenerateConfirm(true)}
              className="text-[12px] gap-1"
              data-testid="regenerate-prep-button"
            >
              <RefreshCw className="w-3.5 h-3.5" strokeWidth={1.8} />
              {t('tab.regenerate')}
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
        <Notice variant="warning" title={t('tab.regenerateConfirmTitle')}>
          <p className="mb-3">
            {t('tab.regenerateConfirmBody')}
          </p>
          <div className="flex gap-2">
            <Button onClick={handleRegenerate} loading={isGenerating}>
              {t('tab.regenerate')}
            </Button>
            <Button
              variant="ghost"
              onClick={() => setShowRegenerateConfirm(false)}
            >
              {t('common:ui.cancel')}
            </Button>
          </div>
        </Notice>
      )}

      {/* New sections */}
      {SECTIONS.map(({ key, icon }) => {
        const items = displayChecklist[key] ?? [];
        if (!items.length) return null;
        return (
          <ChecklistSection
            key={key}
            title={t(`tab.sections.${key}`)}
            icon={icon}
            items={items}
            section={key}
            onCheck={(index, isChecked) => handleCheck(key, index, isChecked)}
          />
        );
      })}

      {/* Legacy sections — only render if they have data */}
      {activeLegacySections.map(({ key }) => (
        <ChecklistSection
          key={key}
          title={t(`tab.sections.${key}`)}
          icon={ClipboardList}
          items={displayChecklist[key]}
          section={key}
          onCheck={(index, isChecked) => handleCheck(key, index, isChecked)}
        />
      ))}
    </div>
  );
}
