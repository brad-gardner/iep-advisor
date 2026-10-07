import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AxiosError } from 'axios';
import type { ApiResponse } from '@/types/api';
import {
  createDraft,
  createField,
  createSection,
  deleteField as deleteFieldApi,
  deleteSection as deleteSectionApi,
  getTemplateVersion,
  listTemplates,
  publishTemplate,
  reorderFields,
  reorderSections,
  updateField as updateFieldApi,
  updateSection,
} from '../admin-templates-api';
import type {
  CreateFieldRequest,
  DocumentTemplateDto,
  TemplateVersionDetailDto,
  UpdateFieldRequest,
} from '../types';

/** Payload for a field mutation (create/update share the same shape sans rowVersion). */
export type FieldInput = Omit<CreateFieldRequest, 'rowVersion'>;

export interface MutationResult {
  ok: boolean;
  /** Stale rowVersion (409) — the working copy must be reloaded. */
  conflict?: boolean;
  /** Field-level validation errors (e.g. publish gating). */
  errors?: string[];
  message?: string;
}

const OK: MutationResult = { ok: true };

// The mount-effect load can fail for several distinct reasons, each with its
// own message — a flag/kind per reason, not the translated string, so a
// language switch after a failed load shows the new language immediately
// rather than a stale snapshot (see `docs/i18n/README.md`'s note on never
// putting `t` in a mount-effect's dependency array). A server-provided
// message is already resolved text and shown as-is.
type LoadErrorKind =
  | { kind: 'server'; message: string }
  | { kind: 'notFound' }
  | { kind: 'noVersion' }
  | { kind: 'versionLoadFailed' }
  | { kind: 'generic' };

/**
 * Holds a template's working version tree and exposes structural + text
 * mutations against it. Every backend mutation returns the refreshed full tree
 * plus a fresh base64 rowVersion, so each success simply replaces both.
 *
 * All mutations run through a single serialized queue: the version's rowVersion
 * is one optimistic-concurrency token for the whole tree, so two in-flight PUTs
 * would race and 409 each other. Serializing (and reading rowVersion fresh from
 * a ref at execution time) keeps concurrent autosaves + structural edits safe.
 */
export function useTemplateBuilder(templateId: number) {
  const { t } = useTranslation(['admin', 'common']);
  const [template, setTemplate] = useState<DocumentTemplateDto | null>(null);
  const [version, setVersion] = useState<TemplateVersionDetailDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadErrorKind, setLoadErrorKind] = useState<LoadErrorKind | null>(null);
  const [conflict, setConflict] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);
  // Count of queued/in-flight mutations. Structural controls (reorder) disable
  // while > 0 so a rapid double-click can't compute orderedIds from stale state.
  const [pendingCount, setPendingCount] = useState(0);

  // Always read the latest tree/rowVersion at mutation-execution time so queued
  // ops never send a stale token.
  const versionRef = useRef<TemplateVersionDetailDto | null>(null);
  const setVersionTree = useCallback((v: TemplateVersionDetailDto) => {
    versionRef.current = v;
    setVersion(v);
  }, []);

  // Promise chain that serializes every mutation.
  const chainRef = useRef<Promise<unknown>>(Promise.resolve());

  // The effect body only calls setState after an await, so it stays effect-safe
  // (no synchronous cascading render). `reload()` owns the pre-fetch resets.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const listRes = await listTemplates();
        if (cancelled) return;
        setLoadErrorKind(null);
        setConflict(false);
        if (!listRes.success || !listRes.data) {
          setLoadErrorKind(listRes.message ? { kind: 'server', message: listRes.message } : { kind: 'generic' });
          return;
        }
        const tmpl = listRes.data.find((tpl) => tpl.id === templateId);
        if (!tmpl) {
          setLoadErrorKind({ kind: 'notFound' });
          return;
        }
        setTemplate(tmpl);
        if (!tmpl.latestVersion) {
          setLoadErrorKind({ kind: 'noVersion' });
          return;
        }
        const verRes = await getTemplateVersion(tmpl.latestVersion.id);
        if (cancelled) return;
        if (verRes.success && verRes.data) {
          setVersionTree(verRes.data);
        } else {
          setLoadErrorKind(
            verRes.message ? { kind: 'server', message: verRes.message } : { kind: 'versionLoadFailed' }
          );
        }
      } catch {
        if (!cancelled) setLoadErrorKind({ kind: 'generic' });
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
    // `t` deliberately excluded — see the `LoadErrorKind` comment above.
  }, [templateId, reloadKey, setVersionTree]);

  let loadError: string | null = null;
  if (loadErrorKind) {
    switch (loadErrorKind.kind) {
      case 'server':
        loadError = loadErrorKind.message;
        break;
      case 'notFound':
        loadError = t('templates.builder.notFound');
        break;
      case 'noVersion':
        loadError = t('templates.builder.noVersionToEdit');
        break;
      case 'versionLoadFailed':
        loadError = t('templates.builder.versionLoadFailed');
        break;
      case 'generic':
        loadError = t('templates.builder.loadErrorFallback');
        break;
    }
  }

  const reload = useCallback(() => {
    setIsLoading(true);
    setLoadErrorKind(null);
    setConflict(false);
    setReloadKey((k) => k + 1);
  }, []);

  // Core serialized runner. `fn` receives the freshest tree so it can read the
  // current version id + rowVersion. A successful response replaces the tree.
  const runMutation = useCallback(
    (
      fn: (v: TemplateVersionDetailDto) => Promise<ApiResponse<TemplateVersionDetailDto>>
    ): Promise<MutationResult> => {
      setPendingCount((n) => n + 1);
      const run = chainRef.current.then(async (): Promise<MutationResult> => {
        const current = versionRef.current;
        if (!current) return { ok: false, message: t('templates.builder.noWorkingVersion') };
        try {
          const res = await fn(current);
          if (res.success && res.data) {
            setVersionTree(res.data);
            return OK;
          }
          return { ok: false, errors: res.errors, message: res.message };
        } catch (err) {
          if (err instanceof AxiosError) {
            const status = err.response?.status;
            const body = err.response?.data as ApiResponse<unknown> | undefined;
            if (status === 409) {
              setConflict(true);
              return { ok: false, conflict: true, message: body?.message };
            }
            return { ok: false, errors: body?.errors, message: body?.message };
          }
          return { ok: false, message: t('common:ui.genericError') };
        }
      });
      // Keep the chain alive even if this op rejected.
      chainRef.current = run.catch(() => undefined);
      void run.finally(() => setPendingCount((n) => n - 1));
      return run;
    },
    [setVersionTree, t]
  );

  const addSection = useCallback(
    (title: string) =>
      runMutation((v) => createSection(v.id, { title, rowVersion: v.rowVersion ?? undefined })),
    [runMutation]
  );

  const updateSectionTitle = useCallback(
    (sectionId: number, title: string) =>
      runMutation((v) => updateSection(sectionId, { title, rowVersion: v.rowVersion ?? undefined })),
    [runMutation]
  );

  const deleteSection = useCallback(
    (sectionId: number) => runMutation((v) => deleteSectionApi(sectionId, v.rowVersion)),
    [runMutation]
  );

  const reorderSectionsBy = useCallback(
    (orderedIds: number[]) =>
      runMutation((v) => reorderSections(v.id, { orderedIds, rowVersion: v.rowVersion ?? undefined })),
    [runMutation]
  );

  const addField = useCallback(
    (sectionId: number, input: FieldInput) =>
      runMutation((v) => createField(sectionId, { ...input, rowVersion: v.rowVersion ?? undefined })),
    [runMutation]
  );

  const updateField = useCallback(
    (fieldId: number, input: FieldInput) =>
      runMutation((v) => {
        const body: UpdateFieldRequest = { ...input, rowVersion: v.rowVersion ?? undefined };
        return updateFieldApi(fieldId, body);
      }),
    [runMutation]
  );

  const deleteField = useCallback(
    (fieldId: number) => runMutation((v) => deleteFieldApi(fieldId, v.rowVersion)),
    [runMutation]
  );

  const reorderFieldsBy = useCallback(
    (sectionId: number, orderedIds: number[]) =>
      runMutation((v) => reorderFields(sectionId, { orderedIds, rowVersion: v.rowVersion ?? undefined })),
    [runMutation]
  );

  const publish = useCallback(
    () => runMutation((v) => publishTemplate(v.documentTemplateId, { rowVersion: v.rowVersion ?? undefined })),
    [runMutation]
  );

  // Fork the latest Published version into a fresh Draft and load it. No
  // rowVersion — the server forks the current published tree.
  const fork = useCallback(async (): Promise<MutationResult> => {
    try {
      const res = await createDraft(templateId);
      if (res.success && res.data) {
        setVersionTree(res.data);
        setConflict(false);
        return OK;
      }
      return { ok: false, errors: res.errors, message: res.message };
    } catch (err) {
      const body =
        err instanceof AxiosError ? (err.response?.data as ApiResponse<unknown> | undefined) : undefined;
      return { ok: false, message: body?.message ?? t('templates.builder.forkFailedFallback') };
    }
  }, [templateId, setVersionTree, t]);

  return {
    template,
    version,
    isLoading,
    loadError,
    conflict,
    reloadKey,
    isMutating: pendingCount > 0,
    readOnly: version?.status === 'Published',
    reload,
    addSection,
    updateSectionTitle,
    deleteSection,
    reorderSections: reorderSectionsBy,
    addField,
    updateField,
    deleteField,
    reorderFields: reorderFieldsBy,
    publish,
    fork,
  };
}

export type TemplateBuilder = ReturnType<typeof useTemplateBuilder>;
