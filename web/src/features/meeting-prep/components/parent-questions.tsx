import { useId, useState, type FormEvent } from 'react';
import { MessageSquarePlus, Plus, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/cn';
import { PARENT_QUESTION_MAX_LENGTH, type AddParentQuestionResult, type ParentQuestion } from '../hooks/use-parent-questions';

interface ParentQuestionsProps {
  questions: ParentQuestion[];
  onAdd: (text: string) => AddParentQuestionResult;
  onCheck: (id: string, isChecked: boolean) => void;
  onRemove: (id: string) => void;
  /** Viewers see the list but cannot change it. */
  readOnly?: boolean;
}

/**
 * "Your questions": the parent's own list for the meeting, next to the
 * generated checklist. Questions arrive from the add form here or from an
 * advocate suggestion (`?addQuestion=`). Kept on this device — the note says so.
 */
export function ParentQuestions({ questions, onAdd, onCheck, onRemove, readOnly = false }: ParentQuestionsProps) {
  const inputId = useId();
  const [draft, setDraft] = useState('');
  const [error, setError] = useState<string | null>(null);
  const checkedCount = questions.filter((q) => q.isChecked).length;

  const submit = (e: FormEvent) => {
    e.preventDefault();
    const result = onAdd(draft);
    if (result === 'added') {
      setDraft('');
      setError(null);
    } else if (result === 'duplicate') {
      setError('That question is already on your list.');
    } else {
      setError(`Write a question of up to ${PARENT_QUESTION_MAX_LENGTH} characters.`);
    }
  };

  return (
    <Card data-testid="parent-questions">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <MessageSquarePlus className="h-5 w-5 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
          <h3 className="font-serif text-[17px] font-semibold text-brand-slate-800">Your questions</h3>
        </div>
        {questions.length > 0 && (
          <span className="text-[12px] font-medium text-brand-slate-400">
            {checkedCount} of {questions.length} asked
          </span>
        )}
      </div>
      <p className="mt-1 text-[12px] text-brand-slate-400">
        Questions you add yourself or accept from the advocate. Saved on this device.
      </p>

      {questions.length > 0 ? (
        <ul className="mt-3 space-y-2" aria-label="Your questions" data-testid="parent-questions-list">
          {questions.map((q) => (
            <li
              key={q.id}
              className="flex items-start gap-3 rounded-card border-[0.5px] border-brand-slate-200 p-3"
              data-testid="parent-question"
            >
              <input
                type="checkbox"
                id={`${inputId}-${q.id}`}
                checked={q.isChecked}
                disabled={readOnly}
                onChange={(e) => onCheck(q.id, e.target.checked)}
                className="mt-0.5 h-4 w-4 shrink-0 rounded border-brand-slate-300 text-brand-teal-500 focus:ring-brand-teal-400"
              />
              <label
                htmlFor={`${inputId}-${q.id}`}
                className={cn('min-w-0 flex-1 text-sm leading-relaxed', q.isChecked ? 'text-brand-slate-400 line-through' : 'text-brand-slate-700')}
              >
                {q.text}
              </label>
              {!readOnly && (
                <button
                  type="button"
                  onClick={() => onRemove(q.id)}
                  aria-label={`Remove question: ${q.text}`}
                  className="shrink-0 rounded p-1 text-brand-slate-400 transition-colors hover:bg-brand-slate-100 hover:text-brand-slate-600 focus:outline-none focus:ring-1 focus:ring-brand-teal-400"
                  data-testid="parent-question-remove"
                >
                  <X className="h-4 w-4" strokeWidth={1.8} aria-hidden="true" />
                </button>
              )}
            </li>
          ))}
        </ul>
      ) : (
        <p className="mt-3 text-sm text-brand-slate-500" data-testid="parent-questions-empty">
          Nothing yet. Add a question below, or accept one the advocate suggests.
        </p>
      )}

      {!readOnly && (
        <form onSubmit={submit} className="mt-3 flex flex-col gap-2 sm:flex-row sm:items-end" data-testid="parent-questions-form">
          <div className="min-w-0 flex-1">
            <Input
              id={inputId}
              label="Add a question"
              value={draft}
              maxLength={PARENT_QUESTION_MAX_LENGTH}
              placeholder="What would you like to ask the team?"
              onChange={(e) => {
                setDraft(e.target.value);
                if (error) setError(null);
              }}
              aria-invalid={error ? true : undefined}
              aria-describedby={error ? `${inputId}-error` : undefined}
            />
          </div>
          <Button type="submit" variant="secondary" disabled={!draft.trim()} data-testid="parent-questions-add">
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
    </Card>
  );
}
