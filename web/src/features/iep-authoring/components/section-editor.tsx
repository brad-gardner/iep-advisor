import { useCallback } from 'react';
import { Card } from '@/components/ui/card';
import { Textarea } from '@/components/ui/input';
import { assistSection } from '../api/iep-assist-api';
import type { AssistKind } from '../api/iep-assist-types';
import { useEditorContext } from '../hooks/use-editor-context';
import { useRowAutosave } from '../hooks/use-row-autosave';
import { sectionKindLabel } from '../lib/section-kinds';
import type { SectionDto } from '../types';
import { AssistPopover } from './assist/assist-popover';
import { PullFromStudentButton } from './pull-from-student/pull-from-student-button';
import { LastEditedStamp } from './last-edited-stamp';
import { RowDeleteButton } from './row-delete-button';

interface SectionEditorProps {
  section: SectionDto;
  onDelete: (id: number) => void;
}

export function SectionEditor({ section, onDelete }: SectionEditorProps) {
  const { draftId, draftApi, bus, registry, currentUserId } = useEditorContext();
  const { schedule, cancel } = useRowAutosave({
    rowKey: `section-${section.id}`,
    save: () => draftApi.saveSection(section.id),
    bus,
    registry,
  });

  const edit = (richText: string) => {
    draftApi.patchSection(section.id, { richText });
    schedule();
  };

  const assistRequest = useCallback(
    (kind: AssistKind) => assistSection(draftId, section.id, kind),
    [draftId, section.id]
  );

  // Cancel any pending debounced save first so the unmount-flush is a no-op and
  // no PUT races the DELETE against a now-deleted id.
  const handleDelete = async () => {
    cancel();
    await onDelete(section.id);
  };

  return (
    <Card className="space-y-3" data-testid={`section-row-${section.id}`}>
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-medium text-brand-slate-700">
          {sectionKindLabel(section.sectionKind)}
        </h3>
      </div>
      <Textarea
        rows={6}
        value={section.richText ?? ''}
        onChange={(e) => edit(e.target.value)}
        placeholder="Write the narrative for this section…"
        data-testid={`section-text-${section.id}`}
        aria-label={`${sectionKindLabel(section.sectionKind)} narrative`}
      />
      <div className="flex flex-wrap items-start gap-2">
        <AssistPopover
          requestFn={assistRequest}
          kinds={['Rewrite', 'Improve']}
          onApply={(text) => edit(text)}
          testIdPrefix={`section-assist-${section.id}`}
        />
        <PullFromStudentButton
          onPick={(content) => edit(content)}
          testIdPrefix={`section-pull-${section.id}`}
        />
      </div>
      <div className="flex items-center justify-between pt-1">
        <LastEditedStamp
          lastEditedAt={section.lastEditedAt}
          lastEditedByUserId={section.lastEditedByUserId}
          currentUserId={currentUserId}
        />
        <RowDeleteButton
          onDelete={() => void handleDelete()}
          label="Delete section"
          testId={`section-delete-${section.id}`}
        />
      </div>
    </Card>
  );
}
