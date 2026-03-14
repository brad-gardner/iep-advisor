import {
  HelpCircle,
  FileText,
  AlertTriangle,
  Scale,
  Target,
  Lightbulb,
} from 'lucide-react';
import type { MeetingPrepChecklist, CheckItemRequest } from '@/types/api';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { ChecklistSection } from './checklist-section';
import { MeetingPrepEmptyState } from './meeting-prep-empty-state';
import { checkItem } from '../api/meeting-prep-api';
import { useState } from 'react';

interface MeetingPrepTabProps {
  checklist: MeetingPrepChecklist | null;
  isLoading: boolean;
  isGenerating: boolean;
  onGenerate: () => void;
  onReload: () => void;
}

const SECTIONS = [
  { key: 'questionsToAsk', title: 'Questions to Ask', icon: HelpCircle },
  { key: 'documentsToBring', title: 'Documents to Bring', icon: FileText },
  { key: 'redFlagsToRaise', title: 'Red Flags to Raise', icon: AlertTriangle },
  { key: 'rightsToReference', title: 'Rights to Reference', icon: Scale },
  { key: 'goalGaps', title: 'Goal Gaps', icon: Target },
  { key: 'generalTips', title: 'General Tips', icon: Lightbulb },
] as const;

type SectionKey = (typeof SECTIONS)[number]['key'];

export function MeetingPrepTab({
  checklist,
  isLoading,
  isGenerating,
  onGenerate,
  onReload,
}: MeetingPrepTabProps) {
  const [localChecklist, setLocalChecklist] = useState<MeetingPrepChecklist | null>(null);

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

  if (!displayChecklist || (!displayChecklist.id && displayChecklist.status !== 'generating')) {
    return <MeetingPrepEmptyState onGenerate={onGenerate} isGenerating={isGenerating} />;
  }

  if (displayChecklist.status === 'generating' || displayChecklist.status === 'pending') {
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
        <Button variant="ghost" onClick={onReload}>
          Check Status
        </Button>
      </div>
    );
  }

  if (displayChecklist.status === 'error') {
    return (
      <div className="flex flex-col items-center justify-center py-16 px-4">
        <Card className="max-w-md text-center">
          <Notice variant="error" title="Generation Failed">
            {displayChecklist.errorMessage || 'An error occurred while generating the checklist.'}
          </Notice>
          <div className="mt-4">
            <Button onClick={onGenerate} disabled={isGenerating}>
              {isGenerating ? 'Retrying...' : 'Retry'}
            </Button>
          </div>
        </Card>
      </div>
    );
  }

  // Completed state
  const allItems = SECTIONS.flatMap((s) => displayChecklist[s.key]);
  const totalCount = allItems.length;
  const checkedCount = allItems.filter((i) => i.isChecked).length;
  const progressPercent = totalCount > 0 ? Math.round((checkedCount / totalCount) * 100) : 0;

  const handleCheck = async (sectionKey: SectionKey, index: number, isChecked: boolean) => {
    // Optimistic update
    const updated = { ...displayChecklist };
    const items = [...updated[sectionKey]];
    items[index] = { ...items[index], isChecked };
    updated[sectionKey] = items;
    setLocalChecklist(updated);

    // API call
    if (displayChecklist.id) {
      const request: CheckItemRequest = { section: sectionKey, index, isChecked };
      try {
        await checkItem(displayChecklist.id, request);
      } catch {
        // Revert on error
        setLocalChecklist(null);
      }
    }
  };

  return (
    <div className="space-y-6">
      {/* Progress bar */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <span className="text-[13px] font-medium text-brand-slate-600">
            {checkedCount} of {totalCount} items checked
          </span>
          <span className="text-[13px] font-medium text-brand-teal-500">
            {progressPercent}%
          </span>
        </div>
        <div className="h-2 bg-brand-slate-100 rounded-full overflow-hidden">
          <div
            className="h-full bg-brand-teal-500 rounded-full transition-all duration-300"
            style={{ width: `${progressPercent}%` }}
          />
        </div>
      </div>

      {/* Sections */}
      {SECTIONS.map(({ key, title, icon }) => (
        <ChecklistSection
          key={key}
          title={title}
          icon={icon}
          items={displayChecklist[key]}
          section={key}
          onCheck={(index, isChecked) => handleCheck(key, index, isChecked)}
        />
      ))}
    </div>
  );
}
