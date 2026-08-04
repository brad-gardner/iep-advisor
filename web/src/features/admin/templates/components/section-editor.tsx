import { useState } from 'react';
import { ChevronDown, ChevronUp, Plus, Trash2 } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { useAutosave } from '@/features/iep-authoring/hooks/use-autosave';
import type { TemplateSectionDto } from '../types';
import type { TemplateBuilder } from '../hooks/use-template-builder';
import { AutosaveIndicator } from './autosave-indicator';
import { FieldEditor } from './field-editor';

interface SectionEditorProps {
  section: TemplateSectionDto;
  builder: TemplateBuilder;
  readOnly: boolean;
  canMoveUp: boolean;
  canMoveDown: boolean;
  onMoveUp: () => void;
  onMoveDown: () => void;
}

/** Edits one section: autosaved title, its fields, and add/reorder/delete controls. */
export function SectionEditor({
  section,
  builder,
  readOnly,
  canMoveUp,
  canMoveDown,
  onMoveUp,
  onMoveDown,
}: SectionEditorProps) {
  const [title, setTitle] = useState(section.title);
  const [addingField, setAddingField] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const titleAutosave = useAutosave<void>(async () => {
    const result = await builder.updateSectionTitle(section.id, title);
    if (!result.ok) throw new Error(result.message ?? 'Save failed');
  });

  const handleTitle = (value: string) => {
    setTitle(value);
    if (value.trim()) titleAutosave.save(undefined);
  };

  const handleAddField = async () => {
    setAddingField(true);
    await builder.addField(section.id, { fieldType: 'Text', label: 'New field', required: false });
    setAddingField(false);
  };

  const handleDelete = async () => {
    setDeleting(true);
    setDeleteError(null);
    titleAutosave.cancel();
    const result = await builder.deleteSection(section.id);
    setDeleting(false);
    if (result.ok) setConfirmDelete(false);
    else setDeleteError(result.message ?? 'Failed to delete section.');
  };

  const moveField = (index: number, direction: -1 | 1) => {
    const ids = section.fields.map((f) => f.id);
    const target = index + direction;
    if (target < 0 || target >= ids.length) return;
    [ids[index], ids[target]] = [ids[target], ids[index]];
    void builder.reorderFields(section.id, ids);
  };

  return (
    <Card data-testid={`section-${section.id}`}>
      <div className="mb-4 flex items-start gap-3">
        <div className="flex-1">
          <Input
            label="Section title"
            id={`section-${section.id}-title`}
            value={title}
            onChange={(e) => handleTitle(e.target.value)}
            disabled={readOnly}
            data-testid={`section-${section.id}-title`}
          />
          <div className="mt-1 min-h-[1rem]">
            <AutosaveIndicator status={titleAutosave.status} />
          </div>
        </div>
        {!readOnly && (
          <div className="flex items-center gap-1 pt-6">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={onMoveUp}
              disabled={!canMoveUp || builder.isMutating}
              aria-label="Move section up"
              data-testid={`section-${section.id}-move-up`}
            >
              <ChevronUp size={16} strokeWidth={1.8} aria-hidden="true" />
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={onMoveDown}
              disabled={!canMoveDown || builder.isMutating}
              aria-label="Move section down"
              data-testid={`section-${section.id}-move-down`}
            >
              <ChevronDown size={16} strokeWidth={1.8} aria-hidden="true" />
            </Button>
            <Button
              type="button"
              variant="danger"
              size="sm"
              onClick={() => setConfirmDelete(true)}
              aria-label="Delete section"
              data-testid={`section-${section.id}-delete`}
            >
              <Trash2 size={16} strokeWidth={1.8} aria-hidden="true" />
            </Button>
          </div>
        )}
      </div>

      <div className="space-y-3">
        {section.fields.length === 0 ? (
          <p className="text-sm text-brand-slate-400">No fields yet.</p>
        ) : (
          section.fields.map((f, i) => (
            <FieldEditor
              key={f.id}
              field={f}
              builder={builder}
              readOnly={readOnly}
              canMoveUp={i > 0}
              canMoveDown={i < section.fields.length - 1}
              onMoveUp={() => moveField(i, -1)}
              onMoveDown={() => moveField(i, 1)}
            />
          ))
        )}
      </div>

      {!readOnly && (
        <div className="mt-4">
          <Button
            type="button"
            variant="secondary"
            size="sm"
            onClick={handleAddField}
            loading={addingField}
            data-testid={`section-${section.id}-add-field`}
          >
            <Plus size={14} strokeWidth={1.8} className="mr-1" aria-hidden="true" />
            Add field
          </Button>
        </div>
      )}

      <ConfirmDialog
        open={confirmDelete}
        title="Delete section"
        message={`Delete "${title.trim() || 'this section'}" and all its fields? This cannot be undone.`}
        confirmLabel="Delete section"
        loading={deleting}
        error={deleteError}
        onConfirm={handleDelete}
        onCancel={() => {
          setConfirmDelete(false);
          setDeleteError(null);
        }}
        data-testid={`section-${section.id}-delete-confirm`}
      />
    </Card>
  );
}
