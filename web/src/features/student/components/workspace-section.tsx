import { useState } from 'react';
import { Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { EntryKindMeta } from '../lib/entry-kinds';
import type { StudentWorkspaceEntryDto } from '../types';
import { EntryCard } from './entry-card';
import { EntryEditor } from './entry-editor';

interface WorkspaceSectionProps {
  meta: EntryKindMeta;
  entries: StudentWorkspaceEntryDto[];
  onAdd: (content: string, isShareable: boolean) => Promise<boolean>;
  onUpdate: (id: number, content: string, isShareable: boolean) => Promise<boolean>;
  onSetShareable: (id: number, isShareable: boolean) => void;
  onDelete: (id: number) => void;
}

// One section of the workspace (Strengths, Interests, etc.): its heading, the
// list of entries for that kind, and an inline "Add" affordance.
export function WorkspaceSection({
  meta,
  entries,
  onAdd,
  onUpdate,
  onSetShareable,
  onDelete,
}: WorkspaceSectionProps) {
  const [adding, setAdding] = useState(false);
  const testId = `section-${meta.kind}`;

  return (
    <section className="space-y-3" data-testid={testId} aria-labelledby={`${testId}-heading`}>
      <div>
        <h2 id={`${testId}-heading`} className="font-serif text-lg">
          {meta.sectionTitle}
        </h2>
        <p className="text-sm text-brand-slate-400">{meta.hint}</p>
      </div>

      {entries.length === 0 && !adding && (
        <p className="text-sm italic text-brand-slate-400" data-testid={`${testId}-empty`}>
          Nothing here yet.
        </p>
      )}

      {entries.map((entry) => (
        <EntryCard
          key={entry.id}
          entry={entry}
          onUpdate={onUpdate}
          onSetShareable={onSetShareable}
          onDelete={onDelete}
        />
      ))}

      {adding ? (
        <EntryEditor
          placeholder={meta.placeholder}
          submitLabel="Add"
          testIdPrefix={`${testId}-add`}
          onCancel={() => setAdding(false)}
          onSubmit={async (content, isShareable) => {
            const ok = await onAdd(content, isShareable);
            if (ok) setAdding(false);
          }}
        />
      ) : (
        <Button
          variant="secondary"
          onClick={() => setAdding(true)}
          data-testid={`${testId}-add-button`}
        >
          <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
          Add
        </Button>
      )}
    </section>
  );
}
