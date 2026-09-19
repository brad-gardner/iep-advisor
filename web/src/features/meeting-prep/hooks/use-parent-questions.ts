import axios from 'axios';
import { useCallback, useEffect, useState } from 'react';
import { useToast } from '@/components/ui/toast';
import {
  createPrepQuestion,
  deletePrepQuestion,
  listPrepQuestions,
  reorderPrepQuestions,
  updatePrepQuestion,
  type ParentPrepQuestionDto,
  type PrepQuestionSource,
} from '../api/prep-questions-api';

/** A question the parent wrote (or accepted from the advocate) for the next meeting. */
export type ParentQuestion = ParentPrepQuestionDto;

/** Same cap the API enforces and the advocate applies to a suggestion payload. */
export const PARENT_QUESTION_MAX_LENGTH = 500;

export type AddParentQuestionResult = 'added' | 'duplicate' | 'invalid' | 'failed';
export type SaveParentQuestionResult = 'saved' | 'duplicate' | 'invalid' | 'failed';
export type MoveDirection = 'up' | 'down';

export const QUESTIONS_FORBIDDEN_MESSAGE = "You don't have permission to change these questions.";
export const QUESTIONS_LOAD_ERROR = 'Could not load your questions.';

function normalise(text: string): string {
  return text.replace(/\s+/g, ' ').trim();
}

/** The API treats questions as the same when they match case-insensitively; mirror that before asking it. */
function sameText(a: string, b: string): boolean {
  return a.toLowerCase() === b.toLowerCase();
}

function isForbidden(err: unknown): boolean {
  return axios.isAxiosError(err) && err.response?.status === 403;
}

/**
 * The parent's own questions for one child's meetings, kept by the API so
 * they follow the parent across devices and reach the advocate's tools.
 * Writes are optimistic where the UI benefits (check, reorder) and reverted
 * on failure; a 403 on any write flips `writeForbidden` so the caller can
 * hide the controls for a viewer whose role changed mid-session.
 */
export function useParentQuestions(childId: number) {
  const { show } = useToast();
  const [questions, setQuestions] = useState<ParentQuestion[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isReordering, setIsReordering] = useState(false);
  const [writeForbidden, setWriteForbidden] = useState(false);
  /** Ids with a check-toggle PUT in flight — the checkbox is disabled meanwhile so a
   *  double-click cannot fire two overlapping writes that race on which one lands last. */
  const [checkingIds, setCheckingIds] = useState<ReadonlySet<number>>(new Set());

  // Another child on the same mounted tab: start over for that list.
  const [loadedFor, setLoadedFor] = useState(childId);
  if (loadedFor !== childId) {
    setLoadedFor(childId);
    setQuestions([]);
    setIsLoading(true);
    setLoadError(null);
    setWriteForbidden(false);
    setCheckingIds(new Set());
  }

  useEffect(() => {
    let active = true;
    listPrepQuestions(childId)
      .then((res) => {
        if (!active) return;
        if (res.success && res.data) setQuestions(res.data);
        else setLoadError(res.message ?? QUESTIONS_LOAD_ERROR);
      })
      .catch(() => {
        if (active) setLoadError(QUESTIONS_LOAD_ERROR);
      })
      .finally(() => {
        if (active) setIsLoading(false);
      });
    return () => {
      active = false;
    };
  }, [childId]);

  /** A write was refused: remember it (so controls hide) or tell the parent what went wrong. */
  const reportWriteFailure = useCallback(
    (err: unknown, fallback: string | null) => {
      if (isForbidden(err)) {
        setWriteForbidden(true);
        show({ message: QUESTIONS_FORBIDDEN_MESSAGE, variant: 'error' });
      } else if (fallback) {
        show({ message: fallback, variant: 'error' });
      }
    },
    [show],
  );

  /**
   * Adds one question. A repeat of one already on the list — here or, case-
   * insensitively, on the server — is reported as `duplicate` rather than
   * added twice. Callers message `failed` themselves (inline or toast).
   */
  const add = useCallback(
    async (text: string, source: PrepQuestionSource = 'parent'): Promise<AddParentQuestionResult> => {
      const clean = normalise(text);
      if (!clean || clean.length > PARENT_QUESTION_MAX_LENGTH) return 'invalid';
      if (questions.some((q) => sameText(q.text, clean))) return 'duplicate';
      try {
        const res = await createPrepQuestion(childId, { text: clean, source });
        const created = res.success ? res.data : undefined;
        if (!created) return 'failed';
        setQuestions((prev) => (prev.some((q) => q.id === created.id) ? prev : [...prev, created]));
        return created.alreadyExisted ? 'duplicate' : 'added';
      } catch (err) {
        reportWriteFailure(err, null);
        return 'failed';
      }
    },
    [childId, questions, reportWriteFailure],
  );

  const setChecked = useCallback(
    async (id: number, isChecked: boolean) => {
      // Guards the race a double-click (or two independent clicks before the
      // UI disables) would otherwise create: PUT(true) then PUT(false) in
      // flight together, with the response's `isChecked` discarded and a
      // negation-based revert landing on whichever side loses.
      if (checkingIds.has(id)) return;
      const previous = questions.find((q) => q.id === id)?.isChecked;
      setCheckingIds((prev) => new Set(prev).add(id));
      setQuestions((prev) => prev.map((q) => (q.id === id ? { ...q, isChecked } : q)));
      try {
        const res = await updatePrepQuestion(id, { isChecked });
        if (!res.success || !res.data) throw new Error(res.message ?? 'update failed');
        const applied = res.data.isChecked;
        setQuestions((prev) => prev.map((q) => (q.id === id ? { ...q, isChecked: applied } : q)));
      } catch (err) {
        setQuestions((prev) => prev.map((q) => (q.id === id ? { ...q, isChecked: previous ?? !isChecked } : q)));
        reportWriteFailure(err, 'Could not update that question.');
      } finally {
        setCheckingIds((prev) => {
          const next = new Set(prev);
          next.delete(id);
          return next;
        });
      }
    },
    [questions, checkingIds, reportWriteFailure],
  );

  const updateText = useCallback(
    async (id: number, text: string): Promise<SaveParentQuestionResult> => {
      const clean = normalise(text);
      if (!clean || clean.length > PARENT_QUESTION_MAX_LENGTH) return 'invalid';
      if (questions.some((q) => q.id !== id && sameText(q.text, clean))) return 'duplicate';
      try {
        const res = await updatePrepQuestion(id, { text: clean });
        const saved = res.success ? res.data : undefined;
        if (!saved) return 'failed';
        setQuestions((prev) => prev.map((q) => (q.id === id ? { ...q, ...saved } : q)));
        return 'saved';
      } catch (err) {
        reportWriteFailure(err, null);
        return 'failed';
      }
    },
    [questions, reportWriteFailure],
  );

  /** Resolves true once the server has dropped the question; false leaves it in place for a retry. */
  const remove = useCallback(
    async (id: number): Promise<boolean> => {
      try {
        const res = await deletePrepQuestion(id);
        if (!res.success) return false;
        setQuestions((prev) => prev.filter((q) => q.id !== id));
        return true;
      } catch (err) {
        reportWriteFailure(err, null);
        return false;
      }
    },
    [reportWriteFailure],
  );

  /** Swaps a question with its neighbour and persists the whole order; one reorder in flight at a time. */
  const move = useCallback(
    async (id: number, direction: MoveDirection) => {
      if (isReordering) return;
      const index = questions.findIndex((q) => q.id === id);
      const target = direction === 'up' ? index - 1 : index + 1;
      if (index < 0 || target < 0 || target >= questions.length) return;
      const movedId = questions[index].id;
      const swappedId = questions[target].id;
      const next = [...questions];
      [next[index], next[target]] = [next[target], next[index]];
      setIsReordering(true);
      setQuestions(next);
      try {
        const res = await reorderPrepQuestions(
          childId,
          next.map((q) => q.id),
        );
        if (!res.success) throw new Error(res.message ?? 'reorder failed');
      } catch (err) {
        // Swap the two moved ids back wherever they currently sit, rather
        // than reinstating the whole pre-move snapshot — a checkbox toggled
        // on either question while the PUT was in flight (checks are not
        // blocked during a reorder) must not be undone by this revert.
        setQuestions((prev) => {
          const i = prev.findIndex((q) => q.id === movedId);
          const j = prev.findIndex((q) => q.id === swappedId);
          if (i < 0 || j < 0) return prev;
          const reverted = [...prev];
          [reverted[i], reverted[j]] = [reverted[j], reverted[i]];
          return reverted;
        });
        reportWriteFailure(err, 'Could not reorder your questions.');
      } finally {
        setIsReordering(false);
      }
    },
    [childId, questions, isReordering, reportWriteFailure],
  );

  return {
    questions,
    isLoading,
    loadError,
    isReordering,
    writeForbidden,
    checkingIds,
    add,
    setChecked,
    updateText,
    remove,
    move,
  };
}
