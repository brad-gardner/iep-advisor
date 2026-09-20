import { useId, useState, type FormEvent } from 'react';
import { MessageSquarePlus, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Input } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import {
  PARENT_QUESTION_MAX_LENGTH,
  type AddParentQuestionResult,
  type MoveDirection,
  type ParentQuestion,
  type SaveParentQuestionResult,
} from '../hooks/use-parent-questions';
import { ParentQuestionRow } from './parent-question-row';

interface ParentQuestionsProps {
  questions: ParentQuestion[];
  isLoading?: boolean;
  loadError?: string | null;
  /** True while an up/down move is being saved; the arrows wait for it. */
  isReordering?: boolean;
  /** Ids with a check-toggle PUT in flight — their checkbox waits for it. */
  checkingIds?: ReadonlySet<number>;
  onAdd: (text: string) => Promise<AddParentQuestionResult>;
  onCheck: (id: number, isChecked: boolean) => void;
  onEdit: (id: number, text: string) => Promise<SaveParentQuestionResult>;
  onMove: (id: number, direction: MoveDirection) => void;
  /** Resolves true once removed; false keeps the confirmation open with an error. */
  onRemove: (id: number) => Promise<boolean>;
  /** Viewers see the list but cannot change it. */
  readOnly?: boolean;
}

const ADD_ERRORS: Record<Exclude<AddParentQuestionResult, 'added'>, string> = {
  duplicate: 'That question is already on your list.',
  invalid: `Write a question of up to ${PARENT_QUESTION_MAX_LENGTH} characters.`,
  failed: 'Could not save your question. Please try again.',
};

const REMOVE_ERROR = 'Could not remove this question.';

/**
 * "Your questions": the parent's own list for the meeting, next to the
 * generated checklist. Questions arrive from the add form here or from an
 * advocate suggestion (`?addQuestion=`), and are kept by the API.
 */
export function ParentQuestions({
  questions,
  isLoading = false,
  loadError = null,
  isReordering = false,
  checkingIds,
  onAdd,
  onCheck,
  onEdit,
  onMove,
  onRemove,
  readOnly = false,
}: ParentQuestionsProps) {
  const inputId = useId();
  const [draft, setDraft] = useState('');
  const [adding, setAdding] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [confirmRemove, setConfirmRemove] = useState<ParentQuestion | null>(null);
  const [removing, setRemoving] = useState(false);
  const [removeError, setRemoveError] = useState<string | null>(null);
  const checkedCount = questions.filter((q) => q.isChecked).length;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (adding) return;
    setAdding(true);
    try {
      const result = await onAdd(draft);
      if (result === 'added') {
        setDraft('');
        setError(null);
      } else {
        setError(ADD_ERRORS[result]);
      }
    } finally {
      setAdding(false);
    }
  };

  // A failed remove keeps the dialog open with the reason so the parent can
  // retry or cancel; only success closes it.
  const remove = async () => {
    if (!confirmRemove || removing) return;
    setRemoving(true);
    setRemoveError(null);
    try {
      const ok = await onRemove(confirmRemove.id);
      if (ok) setConfirmRemove(null);
      else setRemoveError(REMOVE_ERROR);
    } finally {
      setRemoving(false);
    }
  };

  const closeRemove = () => {
    if (removing) return;
    setConfirmRemove(null);
    setRemoveError(null);
  };

  return (
    <Card data-testid="parent-questions">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <MessageSquarePlus className="h-5 w-5 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
          <h3 className="font-serif text-[17px] font-semibold text-brand-slate-800">Your questions</h3>
        </div>
        {questions.length > 0 && (
          <span className="text-[12px] font-medium text-brand-slate-500">
            {checkedCount} of {questions.length} asked
          </span>
        )}
      </div>
      <p className="mt-1 text-[12px] text-brand-slate-500">Questions you add yourself or accept from the advocate.</p>

      {isLoading ? (
        <div className="mt-3 space-y-2" role="status" aria-label="Loading your questions" data-testid="parent-questions-loading">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
      ) : loadError ? (
        <Notice variant="error" title={loadError} className="mt-3" data-testid="parent-questions-error" />
      ) : questions.length > 0 ? (
        <ul className="mt-3 space-y-2" aria-label="Your questions" data-testid="parent-questions-list">
          {questions.map((q, index) => (
            <ParentQuestionRow
              key={q.id}
              question={q}
              readOnly={readOnly}
              isFirst={index === 0}
              isLast={index === questions.length - 1}
              isReordering={isReordering}
              isChecking={checkingIds?.has(q.id) ?? false}
              onCheck={onCheck}
              onMove={onMove}
              onEdit={onEdit}
              onRemoveRequest={(question) => {
                setRemoveError(null);
                setConfirmRemove(question);
              }}
            />
          ))}
        </ul>
      ) : (
        <p className="mt-3 text-sm text-brand-slate-500" data-testid="parent-questions-empty">
          Nothing yet. Add a question below, or accept one the advocate suggests.
        </p>
      )}

      {!readOnly && (
        <form
          onSubmit={(e) => void submit(e)}
          className="mt-3 flex flex-col gap-2 sm:flex-row sm:items-end"
          data-testid="parent-questions-form"
        >
          <div className="min-w-0 flex-1">
            <Input
              id={inputId}
              label="Add a question"
              value={draft}
              maxLength={PARENT_QUESTION_MAX_LENGTH}
              placeholder="What would you like to ask the team?"
              disabled={adding}
              onChange={(e) => {
                setDraft(e.target.value);
                if (error) setError(null);
              }}
              aria-invalid={error ? true : undefined}
              aria-describedby={error ? `${inputId}-error` : undefined}
            />
          </div>
          <Button type="submit" variant="secondary" loading={adding} disabled={!draft.trim()} data-testid="parent-questions-add">
            <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
            Add
          </Button>
        </form>
      )}
      {error && (
        <p id={`${inputId}-error`} role="alert" className="mt-2 text-xs text-brand-danger-700">
          {error}
        </p>
      )}

      <ConfirmDialog
        open={confirmRemove !== null}
        title="Remove question"
        message={confirmRemove ? `Remove "${confirmRemove.text}" from your list?` : ''}
        confirmLabel="Remove question"
        loading={removing}
        error={removeError}
        onConfirm={() => void remove()}
        onCancel={closeRemove}
        data-testid="parent-question-remove-dialog"
      />
    </Card>
  );
}
