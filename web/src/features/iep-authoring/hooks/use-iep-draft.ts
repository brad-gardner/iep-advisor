import { useCallback, useEffect, useRef, useState } from 'react';
import * as api from '../api/iep-drafts-api';
import type {
  AccommodationDto,
  GoalDto,
  IepDraftDto,
  IepSectionKind,
  SectionDto,
  ServiceLineDto,
  TransitionItemDto,
} from '../types';

interface UseIepDraftResult {
  draft: IepDraftDto | null;
  isLoading: boolean;
  error: string | null;

  // Add → POST an empty (or kinded) row, append the returned DTO, return its id.
  addGoal: () => Promise<number | null>;
  addServiceLine: () => Promise<number | null>;
  addAccommodation: () => Promise<number | null>;
  addTransitionItem: () => Promise<number | null>;
  addSection: (kind: IepSectionKind) => Promise<number | null>;

  // Local-only patch (immediate, owns the field while the user types).
  patchGoal: (id: number, patch: Partial<GoalDto>) => void;
  patchServiceLine: (id: number, patch: Partial<ServiceLineDto>) => void;
  patchAccommodation: (id: number, patch: Partial<AccommodationDto>) => void;
  patchTransitionItem: (id: number, patch: Partial<TransitionItemDto>) => void;
  patchSection: (id: number, patch: Partial<SectionDto>) => void;

  // Persist a single row by id; merges only the server's metadata stamps back.
  saveGoal: (id: number) => Promise<void>;
  saveServiceLine: (id: number) => Promise<void>;
  saveAccommodation: (id: number) => Promise<void>;
  saveTransitionItem: (id: number) => Promise<void>;
  saveSection: (id: number) => Promise<void>;

  removeGoal: (id: number) => Promise<void>;
  removeServiceLine: (id: number) => Promise<void>;
  removeAccommodation: (id: number) => Promise<void>;
  removeTransitionItem: (id: number) => Promise<void>;
  removeSection: (id: number) => Promise<void>;
}

// Maps each editable child collection key to its element type. Lets the generic
// add/patch/remove helpers stay precisely typed instead of casting through a union.
interface ListMap {
  goals: GoalDto;
  serviceLines: ServiceLineDto;
  accommodations: AccommodationDto;
  transitionItems: TransitionItemDto;
  sections: SectionDto;
}
type ListKey = keyof ListMap;

// Metadata-only fields that the server stamps; merged back after a row's own save.
type Stamp = Pick<GoalDto, 'lastEditedByUserId' | 'lastEditedAt'>;
function stampOf(dto: Stamp): Stamp {
  return { lastEditedByUserId: dto.lastEditedByUserId, lastEditedAt: dto.lastEditedAt };
}

export function useIepDraft(draftId: number): UseIepDraftResult {
  const [draft, setDraft] = useState<IepDraftDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Mirror of draft for reading current local row values inside save callbacks
  // without making those callbacks depend on (and churn with) draft state.
  const draftRef = useRef<IepDraftDto | null>(null);
  useEffect(() => {
    draftRef.current = draft;
  }, [draft]);

  useEffect(() => {
    // isLoading/error already start in their pending state, so the effect only
    // resolves them — avoiding a synchronous setState in the effect body.
    let cancelled = false;
    api
      .getDraft(draftId)
      .then((res) => {
        if (cancelled) return;
        if (res.success && res.data) setDraft(res.data);
        else setError(res.message ?? 'Failed to load IEP draft');
      })
      .catch(() => {
        if (!cancelled) setError('Failed to load IEP draft');
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [draftId]);

  // Generic local list updater keyed off the draft field name.
  const mutateList = useCallback(
    <K extends ListKey>(key: K, fn: (list: ListMap[K][]) => ListMap[K][]) => {
      setDraft((prev) => (prev ? { ...prev, [key]: fn(prev[key] as ListMap[K][]) } : prev));
    },
    []
  );

  // ---- Add helpers ----
  const add = useCallback(
    async <K extends ListKey>(
      key: K,
      call: () => Promise<{ success: boolean; data?: ListMap[K] }>
    ): Promise<number | null> => {
      try {
        const res = await call();
        if (res.success && res.data) {
          const created = res.data;
          mutateList(key, (list) => [...list, created]);
          return created.id;
        }
      } catch {
        /* swallow — surfaced via missing row */
      }
      return null;
    },
    [mutateList]
  );

  const addGoal = useCallback(
    () => add('goals', () => api.createGoal(draftId, {})),
    [add, draftId]
  );
  const addServiceLine = useCallback(
    () => add('serviceLines', () => api.createServiceLine(draftId, {})),
    [add, draftId]
  );
  const addAccommodation = useCallback(
    () => add('accommodations', () => api.createAccommodation(draftId, {})),
    [add, draftId]
  );
  const addTransitionItem = useCallback(
    () =>
      add('transitionItems', () => api.createTransitionItem(draftId, {})),
    [add, draftId]
  );
  const addSection = useCallback(
    (kind: IepSectionKind) =>
      add('sections', () => api.createSection(draftId, { sectionKind: kind })),
    [add, draftId]
  );

  // ---- Local patch helpers ----
  const patch = useCallback(
    <K extends ListKey>(key: K, id: number, p: Partial<ListMap[K]>) => {
      mutateList(key, (list) => list.map((row) => (row.id === id ? { ...row, ...p } : row)));
    },
    [mutateList]
  );

  const patchGoal = useCallback((id: number, p: Partial<GoalDto>) => patch('goals', id, p), [patch]);
  const patchServiceLine = useCallback(
    (id: number, p: Partial<ServiceLineDto>) => patch('serviceLines', id, p),
    [patch]
  );
  const patchAccommodation = useCallback(
    (id: number, p: Partial<AccommodationDto>) => patch('accommodations', id, p),
    [patch]
  );
  const patchTransitionItem = useCallback(
    (id: number, p: Partial<TransitionItemDto>) => patch('transitionItems', id, p),
    [patch]
  );
  const patchSection = useCallback(
    (id: number, p: Partial<SectionDto>) => patch('sections', id, p),
    [patch]
  );

  // ---- Save helpers: PUT current local row, merge back metadata stamps only ----
  const saveGoal = useCallback(
    async (id: number) => {
      const row = draftRef.current?.goals.find((g) => g.id === id);
      if (!row) return;
      const res = await api.updateGoal(draftId, id, {
        domain: row.domain,
        goalText: row.goalText,
        baseline: row.baseline,
        targetCriteria: row.targetCriteria,
        measurementMethod: row.measurementMethod,
        timeframe: row.timeframe,
      });
      if (res.success && res.data) patch('goals', id, stampOf(res.data));
    },
    [draftId, patch]
  );

  const saveServiceLine = useCallback(
    async (id: number) => {
      const row = draftRef.current?.serviceLines.find((s) => s.id === id);
      if (!row) return;
      const res = await api.updateServiceLine(draftId, id, {
        serviceType: row.serviceType,
        frequency: row.frequency,
        duration: row.duration,
        location: row.location,
        providerRole: row.providerRole,
        startDate: row.startDate,
        endDate: row.endDate,
      });
      if (res.success && res.data) patch('serviceLines', id, stampOf(res.data));
    },
    [draftId, patch]
  );

  const saveAccommodation = useCallback(
    async (id: number) => {
      const row = draftRef.current?.accommodations.find((a) => a.id === id);
      if (!row) return;
      const res = await api.updateAccommodation(draftId, id, {
        category: row.category,
        text: row.text,
      });
      if (res.success && res.data) patch('accommodations', id, stampOf(res.data));
    },
    [draftId, patch]
  );

  const saveTransitionItem = useCallback(
    async (id: number) => {
      const row = draftRef.current?.transitionItems.find((t) => t.id === id);
      if (!row) return;
      const res = await api.updateTransitionItem(draftId, id, {
        postsecondaryGoalArea: row.postsecondaryGoalArea,
        servicesText: row.servicesText,
      });
      if (res.success && res.data) patch('transitionItems', id, stampOf(res.data));
    },
    [draftId, patch]
  );

  const saveSection = useCallback(
    async (id: number) => {
      const row = draftRef.current?.sections.find((s) => s.id === id);
      if (!row) return;
      const res = await api.updateSection(draftId, id, {
        sectionKind: row.sectionKind as IepSectionKind,
        richText: row.richText,
      });
      if (res.success && res.data) patch('sections', id, stampOf(res.data));
    },
    [draftId, patch]
  );

  // ---- Remove helpers ----
  const remove = useCallback(
    async <K extends ListKey>(
      key: K,
      id: number,
      call: (id: number) => Promise<{ success: boolean }>
    ) => {
      try {
        const res = await call(id);
        if (res.success) {
          mutateList(key, (list) => list.filter((row) => row.id !== id));
        }
      } catch {
        /* swallow — row stays so the user can retry */
      }
    },
    [mutateList]
  );

  const removeGoal = useCallback(
    (id: number) => remove('goals', id, (i) => api.deleteGoal(draftId, i)),
    [remove, draftId]
  );
  const removeServiceLine = useCallback(
    (id: number) =>
      remove('serviceLines', id, (i) => api.deleteServiceLine(draftId, i)),
    [remove, draftId]
  );
  const removeAccommodation = useCallback(
    (id: number) =>
      remove('accommodations', id, (i) => api.deleteAccommodation(draftId, i)),
    [remove, draftId]
  );
  const removeTransitionItem = useCallback(
    (id: number) =>
      remove('transitionItems', id, (i) =>
        api.deleteTransitionItem(draftId, i)
      ),
    [remove, draftId]
  );
  const removeSection = useCallback(
    (id: number) => remove('sections', id, (i) => api.deleteSection(draftId, i)),
    [remove, draftId]
  );

  return {
    draft,
    isLoading,
    error,
    addGoal,
    addServiceLine,
    addAccommodation,
    addTransitionItem,
    addSection,
    patchGoal,
    patchServiceLine,
    patchAccommodation,
    patchTransitionItem,
    patchSection,
    saveGoal,
    saveServiceLine,
    saveAccommodation,
    saveTransitionItem,
    saveSection,
    removeGoal,
    removeServiceLine,
    removeAccommodation,
    removeTransitionItem,
    removeSection,
  };
}
