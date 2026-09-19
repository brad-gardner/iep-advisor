import { useCallback, useState } from 'react';

/** A question the parent wrote (or accepted from the advocate) for the next meeting. */
export interface ParentQuestion {
  id: string;
  text: string;
  isChecked: boolean;
  addedAt: string;
}

/** Same cap the advocate applies to a suggestion payload. */
export const PARENT_QUESTION_MAX_LENGTH = 500;

export type AddParentQuestionResult = 'added' | 'duplicate' | 'invalid';

const STORAGE_PREFIX = 'iep-advisor:meeting-prep:my-questions';

function storageKey(userId: number | null | undefined, childId: number): string {
  return `${STORAGE_PREFIX}:${userId ?? 'anon'}:${childId}`;
}

function normalise(text: string): string {
  return text.replace(/\s+/g, ' ').trim();
}

function isParentQuestion(value: unknown): value is ParentQuestion {
  if (!value || typeof value !== 'object') return false;
  const q = value as Record<string, unknown>;
  return typeof q.id === 'string' && typeof q.text === 'string' && typeof q.isChecked === 'boolean' && typeof q.addedAt === 'string';
}

function readStored(key: string): ParentQuestion[] {
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return [];
    const parsed: unknown = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed.filter(isParentQuestion) : [];
  } catch {
    return [];
  }
}

function writeStored(key: string, questions: ParentQuestion[]): void {
  try {
    if (questions.length === 0) localStorage.removeItem(key);
    else localStorage.setItem(key, JSON.stringify(questions));
  } catch {
    // Storage unavailable (private mode, quota): the in-memory list still works for this visit.
  }
}

function newId(): string {
  const c = typeof crypto !== 'undefined' ? crypto : undefined;
  return c && typeof c.randomUUID === 'function' ? c.randomUUID() : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

/**
 * The parent's own questions for one child's meetings. The generated
 * checklist has no write path for parent-authored items, so these live in
 * this browser's storage (keyed by user and child) until the API grows one.
 */
export function useParentQuestions(userId: number | null | undefined, childId: number) {
  const key = storageKey(userId, childId);
  const [questions, setQuestions] = useState<ParentQuestion[]>(() => readStored(key));
  const [loadedKey, setLoadedKey] = useState(key);

  // Another child (or account) on the same mounted tab: re-read that list.
  if (loadedKey !== key) {
    setLoadedKey(key);
    setQuestions(readStored(key));
  }

  const update = useCallback(
    (fn: (prev: ParentQuestion[]) => ParentQuestion[]) => {
      setQuestions((prev) => {
        const next = fn(prev);
        writeStored(key, next);
        return next;
      });
    },
    [key],
  );

  /** Adds one question; a repeat of an existing question is reported rather than duplicated. */
  const add = useCallback(
    (text: string): AddParentQuestionResult => {
      const clean = normalise(text);
      if (!clean || clean.length > PARENT_QUESTION_MAX_LENGTH) return 'invalid';
      // Read the stored list directly so two adds in the same tick (or a
      // deep link arriving before state settles) still de-duplicate.
      const current = readStored(key);
      if (current.some((q) => q.text.toLowerCase() === clean.toLowerCase())) return 'duplicate';
      const next = [...current, { id: newId(), text: clean, isChecked: false, addedAt: new Date().toISOString() }];
      writeStored(key, next);
      setQuestions(next);
      return 'added';
    },
    [key],
  );

  const setChecked = useCallback(
    (id: string, isChecked: boolean) => update((prev) => prev.map((q) => (q.id === id ? { ...q, isChecked } : q))),
    [update],
  );

  const remove = useCallback((id: string) => update((prev) => prev.filter((q) => q.id !== id)), [update]);

  return { questions, add, setChecked, remove };
}
