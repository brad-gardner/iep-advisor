import { useState } from 'react';
import { Pencil, Trash2 } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import type { StudentWorkspaceEntryDto } from '../types';
import { EntryEditor } from './entry-editor';
import { ShareToggle } from './share-toggle';

interface EntryCardProps {
  entry: StudentWorkspaceEntryDto;
  onUpdate: (id: number, content: string, isShareable: boolean) => Promise<boolean>;
  onSetShareable: (id: number, isShareable: boolean) => void;
  onDelete: (id: number) => void;
}

// A single workspace entry: its content, a share toggle, and edit/delete
// affordances. Switches into an inline editor when the student edits.
export function EntryCard({
  entry,
  onUpdate,
  onSetShareable,
  onDelete,
}: EntryCardProps) {
  const [editing, setEditing] = useState(false);
  const testId = `entry-${entry.id}`;

  if (editing) {
    return (
      <Card className="space-y-3" data-testid={testId}>
        <EntryEditor
          initialContent={entry.content}
          initialShareable={entry.isShareable}
          submitLabel="Save"
          testIdPrefix={testId}
          onCancel={() => setEditing(false)}
          onSubmit={async (content, isShareable) => {
            const ok = await onUpdate(entry.id, content, isShareable);
            if (ok) setEditing(false);
          }}
        />
      </Card>
    );
  }

  return (
    <Card className="space-y-3" data-testid={testId}>
      <p className="whitespace-pre-wrap text-sm text-brand-slate-800" data-testid={`${testId}-content`}>
        {entry.content}
      </p>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <ShareToggle entry={entry} onToggle={onSetShareable} testIdPrefix={testId} />
        <div className="flex items-center gap-1">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => setEditing(true)}
            className="gap-1.5"
            data-testid={`${testId}-edit`}
          >
            <Pencil className="h-3.5 w-3.5" strokeWidth={1.8} aria-hidden="true" />
            Edit
          </Button>
          <Button
            type="button"
            variant="danger"
            size="sm"
            onClick={() => onDelete(entry.id)}
            className="gap-1.5"
            data-testid={`${testId}-delete`}
          >
            <Trash2 className="h-3.5 w-3.5" strokeWidth={1.8} aria-hidden="true" />
            Delete
          </Button>
        </div>
      </div>
    </Card>
  );
}
