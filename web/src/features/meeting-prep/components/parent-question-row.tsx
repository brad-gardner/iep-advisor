import { useId, useState, type ButtonHTMLAttributes, type FormEvent, type KeyboardEvent } from 'react';
import { Check, ChevronDown, ChevronUp, Pencil, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/cn';
import {
  PARENT_QUESTION_MAX_LENGTH,
  type MoveDirection,
  type ParentQuestion,
  type SaveParentQuestionResult,
} from '../hooks/use-parent-questions';

interface ParentQuestionRowProps {
  question: ParentQuestion;
  readOnly: boolean;
  isFirst: boolean;
  isLast: boolean;
  /** A reorder is in flight — the arrows wait for it so two swaps cannot cross. */
  isReordering: boolean;
  /** This question's own check-toggle PUT is in flight — the checkbox waits for it. */
  isChecking: boolean;
  onCheck: (id: number, isChecked: boolean) => void;
  onMove: (id: number, direction: MoveDirection) => void;
  onEdit: (id: number, text: string) => Promise<SaveParentQuestionResult>;
  onRemoveRequest: (question: ParentQuestion) => void;
}

const EDIT_ERRORS: Record<Exclude<SaveParentQuestionResult, 'saved'>, string> = {
  duplicate: 'That question is already on your list.',
  invalid: `Write a question of up to ${PARENT_QUESTION_MAX_LENGTH} characters.`,
  failed: 'Could not save your change. Please try again.',
};

function IconButton({ className, ...props }: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type="button"
      {...props}
      className={cn(
        'shrink-0 rounded p-1 text-brand-slate-400 transition-colors hover:bg-brand-slate-100 hover:text-brand-slate-600 focus:outline-none focus:ring-1 focus:ring-brand-teal-400 disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:bg-transparent',
        className,
      )}
    />
  );
}

/**
 * One question on the parent's list: tick it off, nudge it up or down, fix
 * its wording in place, or ask to remove it (the list owns the confirmation).
 */
export function ParentQuestionRow({
  question,
  readOnly,
  isFirst,
  isLast,
  isReordering,
  isChecking,
  onCheck,
  onMove,
  onEdit,
  onRemoveRequest,
}: ParentQuestionRowProps) {
  const id = useId();
  const checkboxId = `${id}-check`;
  const editId = `${id}-edit`;
  const errorId = `${id}-error`;
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(question.text);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const startEdit = () => {
    setDraft(question.text);
    setError(null);
    setEditing(true);
  };

  const cancelEdit = () => {
    if (saving) return;
    setEditing(false);
    setError(null);
  };

  const saveEdit = async (e: FormEvent) => {
    e.preventDefault();
    if (saving) return;
    if (draft.trim() === question.text) {
      setEditing(false);
      return;
    }
    setSaving(true);
    try {
      const result = await onEdit(question.id, draft);
      if (result === 'saved') setEditing(false);
      else setError(EDIT_ERRORS[result]);
    } finally {
      setSaving(false);
    }
  };

  const onEditKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Escape') {
      e.preventDefault();
      cancelEdit();
    }
  };

  return (
    <li className="rounded-card border-[0.5px] border-brand-slate-200 p-3" data-testid="parent-question">
      <div className="flex items-start gap-3">
        <input
          type="checkbox"
          id={checkboxId}
          checked={question.isChecked}
          disabled={readOnly || editing}
          aria-disabled={isChecking || undefined}
          // The hook already refuses overlapping writes per id; `disabled` would blur a keyboard user
          // after every Space press, so the in-flight state is advisory only.
          onChange={(e) => {
            if (isChecking) return;
            onCheck(question.id, e.target.checked);
          }}
          className="mt-0.5 h-4 w-4 shrink-0 rounded border-brand-slate-300 text-brand-teal-500 focus:ring-brand-teal-400"
        />
        {editing ? (
          <form onSubmit={(e) => void saveEdit(e)} className="min-w-0 flex-1" data-testid="parent-question-edit-form">
            <Input
              id={editId}
              aria-label="Edit question"
              value={draft}
              maxLength={PARENT_QUESTION_MAX_LENGTH}
              autoFocus
              disabled={saving}
              onChange={(e) => {
                setDraft(e.target.value);
                if (error) setError(null);
              }}
              onKeyDown={onEditKeyDown}
              aria-invalid={error ? true : undefined}
              aria-describedby={error ? errorId : undefined}
            />
            {error && (
              <p id={errorId} role="alert" className="mt-1 text-xs text-brand-danger-700">
                {error}
              </p>
            )}
            <div className="mt-2 flex gap-2">
              <Button type="submit" size="sm" loading={saving} disabled={!draft.trim()} data-testid="parent-question-save">
                <Check className="mr-1 h-3.5 w-3.5" aria-hidden="true" />
                Save
              </Button>
              <Button type="button" size="sm" variant="ghost" disabled={saving} onClick={cancelEdit}>
                Cancel
              </Button>
            </div>
          </form>
        ) : (
          <label
            htmlFor={checkboxId}
            className={cn(
              'min-w-0 flex-1 text-sm leading-relaxed',
              question.isChecked ? 'text-brand-slate-400 line-through' : 'text-brand-slate-700',
            )}
          >
            {question.text}
          </label>
        )}
        {!readOnly && !editing && (
          <div className="flex shrink-0 items-center" role="group" aria-label={`Actions for: ${question.text}`}>
            <IconButton
              onClick={() => onMove(question.id, 'up')}
              disabled={isFirst || isReordering}
              aria-label={`Move up: ${question.text}`}
              data-testid="parent-question-move-up"
            >
              <ChevronUp className="h-4 w-4" strokeWidth={1.8} aria-hidden="true" />
            </IconButton>
            <IconButton
              onClick={() => onMove(question.id, 'down')}
              disabled={isLast || isReordering}
              aria-label={`Move down: ${question.text}`}
              data-testid="parent-question-move-down"
            >
              <ChevronDown className="h-4 w-4" strokeWidth={1.8} aria-hidden="true" />
            </IconButton>
            <IconButton onClick={startEdit} aria-label={`Edit question: ${question.text}`} data-testid="parent-question-edit">
              <Pencil className="h-4 w-4" strokeWidth={1.8} aria-hidden="true" />
            </IconButton>
            <IconButton
              onClick={() => onRemoveRequest(question)}
              aria-label={`Remove question: ${question.text}`}
              data-testid="parent-question-remove"
            >
              <X className="h-4 w-4" strokeWidth={1.8} aria-hidden="true" />
            </IconButton>
          </div>
        )}
      </div>
    </li>
  );
}
