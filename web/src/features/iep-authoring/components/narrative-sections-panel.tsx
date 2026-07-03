import { useState } from 'react';
import { Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/input';
import { useToast } from '@/components/ui/toast';
import { useEditorContext } from '../hooks/use-editor-context';
import { SECTION_KINDS } from '../lib/section-kinds';
import type { IepSectionKind } from '../types';
import { EmptyHint } from './empty-hint';
import { SectionEditor } from './section-editor';

export function NarrativeSectionsPanel() {
  const { draftApi } = useEditorContext();
  const { show } = useToast();
  const sections = draftApi.draft?.sections ?? [];
  const [kind, setKind] = useState<IepSectionKind>(SECTION_KINDS[0].value);

  const handleDelete = async (id: number) => {
    if (!confirm('Delete this section? This cannot be undone.')) return;
    if (await draftApi.removeSection(id)) {
      show({ message: 'Section deleted', variant: 'success' });
    }
  };

  return (
    <section className="space-y-4" data-testid="narrative-sections-panel">
      {sections.length === 0 && (
        <EmptyHint>No narrative sections yet. Pick a kind and add one.</EmptyHint>
      )}
      {sections.map((section) => (
        <SectionEditor key={section.id} section={section} onDelete={handleDelete} />
      ))}
      <div className="flex items-end gap-2">
        <div className="w-56">
          <Select
            label="Section kind"
            value={kind}
            onChange={(e) => setKind(e.target.value as IepSectionKind)}
            data-testid="section-kind-select"
          >
            {SECTION_KINDS.map((k) => (
              <option key={k.value} value={k.value}>
                {k.label}
              </option>
            ))}
          </Select>
        </div>
        <Button
          variant="secondary"
          onClick={() => void draftApi.addSection(kind)}
          data-testid="add-section"
        >
          <Plus className="w-4 h-4 mr-1" aria-hidden="true" />
          Add section
        </Button>
      </div>
    </section>
  );
}
