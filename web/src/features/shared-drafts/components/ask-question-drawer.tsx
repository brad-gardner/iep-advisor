import { useState, type FormEvent } from 'react';
import { Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Drawer } from '@/components/ui/drawer';
import { Notice } from '@/components/ui/notice';
import { Textarea } from '@/components/ui/input';
import { apiErrorMessage } from '@/lib/api-error';
import { askDraftQuestion } from '../api/shared-drafts-api';
import { useDraftReviewContext } from '../hooks/draft-review-context';

interface AskQuestionDrawerProps {
  open: boolean;
  onClose: () => void;
  revisionId: number;
  targetFieldKey?: string;
  targetRowId?: string;
  targetLabel: string;
  /** Unique per card — two cards render one Drawer each, always mounted
   *  (Drawer keeps its own `<dialog>` in the DOM even while closed). */
  'data-testid': string;
}

const MAX_QUESTION_LENGTH = 1000;

/** Private Q&A thread for one goal/service/accommodation (or field). Never
 *  visible to staff — the drawer says so up front. */
export function AskQuestionDrawer({
  open,
  onClose,
  revisionId,
  targetFieldKey,
  targetRowId,
  targetLabel,
  'data-testid': testId,
}: AskQuestionDrawerProps) {
  const ctx = useDraftReviewContext();
  const [question, setQuestion] = useState('');
  const [isAsking, setIsAsking] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!ctx) return null;
  const { notes, addNote, removeNote } = ctx;
  const thread = notes
    .filter(
      (n) => (n.targetFieldKey ?? null) === (targetFieldKey ?? null) && (n.targetRowId ?? null) === (targetRowId ?? null)
    )
    .sort((a, b) => a.createdAt.localeCompare(b.createdAt));

  const handleAsk = async (e: FormEvent) => {
    e.preventDefault();
    const trimmed = question.trim();
    if (!trimmed) return;
    setIsAsking(true);
    setError(null);
    try {
      const res = await askDraftQuestion(revisionId, { question: trimmed, targetFieldKey, targetRowId });
      if (res.success && res.data) {
        addNote({
          id: res.data.noteId,
          revisionId,
          question: res.data.question,
          answer: res.data.answer,
          targetFieldKey: targetFieldKey ?? null,
          targetRowId: targetRowId ?? null,
          createdAt: res.data.answeredAt,
        });
        setQuestion('');
      } else {
        setError(res.message ?? 'Could not get an answer right now.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not get an answer right now.'));
    } finally {
      setIsAsking(false);
    }
  };

  const handleDelete = async (noteId: number) => {
    const result = await removeNote(noteId);
    if (!result.ok) setError(result.message ?? 'Could not delete this note.');
  };

  return (
    <Drawer open={open} onClose={onClose} title={`Ask about: ${targetLabel}`} data-testid={testId}>
      <div className="space-y-4">
        <Notice variant="info" title="Private — only you can see this">
          Your questions and answers here are never visible to the school team.
        </Notice>

        {thread.length === 0 ? (
          <p className="text-sm text-brand-slate-400">No questions yet.</p>
        ) : (
          <ul className="space-y-3" data-testid={`${testId}-thread`}>
            {thread.map((note) => (
              <li key={note.id} className="rounded-card border border-brand-slate-200 p-3">
                <div className="flex items-start justify-between gap-2">
                  <p className="text-sm font-medium text-brand-slate-800">{note.question}</p>
                  <button
                    type="button"
                    onClick={() => handleDelete(note.id)}
                    aria-label="Delete this question"
                    data-testid={`delete-note-${note.id}`}
                    className="shrink-0 text-brand-slate-400 transition-colors hover:text-brand-danger-700"
                  >
                    <Trash2 className="h-3.5 w-3.5" aria-hidden="true" />
                  </button>
                </div>
                <p className="mt-1 whitespace-pre-wrap text-sm text-brand-slate-600">{note.answer}</p>
              </li>
            ))}
          </ul>
        )}

        {error && (
          <div role="alert">
            <Notice variant="error" title={error} />
          </div>
        )}

        <form onSubmit={handleAsk} className="space-y-2">
          <Textarea
            label="Your question"
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            maxLength={MAX_QUESTION_LENGTH}
            rows={3}
            data-testid={`${testId}-input`}
          />
          <Button type="submit" loading={isAsking} disabled={!question.trim()} data-testid={`${testId}-submit`}>
            Ask
          </Button>
        </form>
      </div>
    </Drawer>
  );
}
