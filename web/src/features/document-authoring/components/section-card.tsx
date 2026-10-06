import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { cn } from '@/lib/cn';
import type { AutosaveStatus } from '@/hooks/use-autosave';
import { useFlushRegistry } from '@/hooks/use-flush-registry';
import { AutosaveIndicator } from '@/features/admin/templates/components/autosave-indicator';
import type { DocumentFieldValue, DocumentValuePatch, TableRowValue, TemplateFieldDto, TemplateSectionDto } from '../types';
import type { CompletenessItem } from '../lib/completeness';
import { isBlank } from '../lib/completeness';
import { sectionDomId } from '../lib/section-dom';
import type { SaveResult } from '../hooks/use-document-instance';
import { DocumentFlushContext } from '../hooks/flush-registry-context';
import { DocumentField } from './field-renderers/document-field';
import { ReadField } from './field-renderers/read/read-field';

const SAVED_LINGER_MS = 1500;

interface SectionCardProps {
  section: TemplateSectionDto;
  /** Latest server-saved values for the whole document (keyed by fieldKey). */
  values: Record<string, unknown>;
  disabled: boolean;
  saveValues: (patch: DocumentValuePatch) => Promise<SaveResult>;
  isOpen: boolean;
  onOpen: () => void;
  onClose: () => void;
  /** This section's own completeness flags, for the header badge. */
  items: CompletenessItem[];
  /** Registers a getter for "does this section currently have an unresolved
   *  save failure" with the document-level editing controller, so Finalize
   *  can refuse to close every section (and snapshot) while one of them still
   *  has a field that failed to save. Returns an unregister cleanup, same
   *  shape as `FlushRegistry.register`. Omitted in tests that don't exercise
   *  Finalize's gating. */
  registerFailureStatus?: (hasFailures: () => boolean) => () => void;
}

function editButtonDomId(sectionId: number): string {
  return `section-${sectionId}-edit-button`;
}

/** Default a field's snapshot value the same way its own renderer coerces an
 *  absent value, so a restored snapshot round-trips through save the same as
 *  a freshly-loaded one. */
function snapshotValue(field: TemplateFieldDto, raw: unknown): string | boolean | TableRowValue[] {
  if (field.fieldType === 'Checkbox') return raw === true;
  if (field.fieldType === 'Table') return Array.isArray(raw) ? (raw as TableRowValue[]) : [];
  return typeof raw === 'string' ? raw : '';
}

function snapshotSection(fields: TemplateFieldDto[], values: Record<string, unknown>): DocumentValuePatch {
  const patch: DocumentValuePatch = {};
  for (const field of fields) patch[field.fieldKey] = snapshotValue(field, values[field.fieldKey]);
  return patch;
}

function discardButtonDomId(sectionId: number): string {
  return `section-${sectionId}-discard-button`;
}

/** Cheap structural-equality fallback for "did anything actually change since
 *  the snapshot" — both sides are built the same way (`snapshotSection`), by
 *  the same field order, from JSON-safe values, so key order is stable and a
 *  string comparison is sufficient without pulling in a deep-equal dependency. */
function patchesEqual(a: DocumentValuePatch, b: DocumentValuePatch): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

/**
 * One section: read-only by default, with its own Edit → Done / Discard
 * changes toggle. Edits autosave while open (each field registers its flush
 * into a registry scoped to THIS section, which in turn registers into the
 * document-wide registry — so Finalize's single `flushAll()` still drains
 * every open section, and Done can flush just this one).
 */
export function SectionCard({
  section,
  values,
  disabled,
  saveValues,
  isOpen,
  onOpen,
  onClose,
  items,
  registerFailureStatus,
}: SectionCardProps) {
  const fields = useMemo(() => [...section.fields].sort((a, b) => a.displayOrder - b.displayOrder), [section.fields]);
  const isEmpty = useMemo(() => fields.every((f) => isBlank(values[f.fieldKey])), [fields, values]);

  const required = items.filter((i) => i.severity === 'required').length;
  const advisory = items.length - required;

  // Nested registry: every field rendered below registers its flush here
  // (via the normal DocumentFlushContext.Provider a layer down); this
  // section's own flushAll is then registered into the OUTER (document-wide)
  // registry, so a finalize's single flushAll() still reaches every field.
  const sectionFlushRegistry = useFlushRegistry();
  // Read the OUTER (document-wide) registry directly via useContext — called
  // here, before this component's own nested <DocumentFlushContext.Provider>
  // (rendered below) can shadow it for its children.
  const outerFlush = useContext(DocumentFlushContext);
  useEffect(() => {
    if (!outerFlush) return undefined;
    return outerFlush.register(`section-${section.id}`, sectionFlushRegistry.flushAll);
  }, [outerFlush, section.id, sectionFlushRegistry]);

  const [saveStatus, setSaveStatus] = useState<AutosaveStatus>('idle');
  const pendingCountRef = useRef(0);
  const savedTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  // Whether ANY save has been attempted (dispatched, not necessarily settled)
  // since this section opened — read synchronously by Discard right after an
  // `await flushAll()`, where a React STATE flag would be stale (it could only
  // be set once a save RESOLVES, on a later render this same async handler
  // can't see yet). Reset on each closed→open transition below.
  const saveAttemptedRef = useRef(false);
  // Which field keys currently have an UNRESOLVED save failure, and the last
  // value each one tried (and failed) to save — keyed so one field's failure
  // is never masked by a different field's later success (that was the P2:
  // `lastSaveOkRef`/`pendingCountRef` tracked only the single most-recently-
  // settled save, so A failing then B succeeding reported "ok"). Read
  // synchronously by Done (after an `await flushAll()`, where a React STATE
  // flag would be stale) and by `registerFailureStatus` for Finalize's gate.
  // The remembered value is what Retry resends: a field's own `onSave`
  // callback discards a non-throwing `{ok:false}` as a plain success (see
  // TextField/RichTextField), so the field's OWN autosave has nothing left
  // queued to retry on its own once that happens — this is the only place
  // that still has the value that failed.
  const failedFieldsRef = useRef<Map<string, DocumentFieldValue>>(new Map());
  // Reactive mirror of `failedFieldsRef` for rendering the failure banner —
  // the ref stays the source of truth for synchronous reads.
  const [failedFields, setFailedFields] = useState<Array<{ key: string; label: string }>>([]);
  const [retrying, setRetrying] = useState(false);
  // True while confirmDiscard's restore save is in flight — disables both
  // confirmation buttons and the fields themselves so nothing can race the
  // restore (P3: a field left enabled during the restore could autosave an
  // edit that lands right after it, silently undoing the discard).
  const [restoring, setRestoring] = useState(false);
  // Mirrors `confirmingDiscard`, read synchronously after `confirmDiscard`'s
  // `await saveValues(snapshot)` — `confirmingDiscard` state could be stale if
  // "Keep editing" were clicked mid-request (it's disabled while `restoring`,
  // but this is the one safe way to tell rather than assume that).
  const confirmingDiscardRef = useRef(false);

  // Lets Finalize (via `useSectionEditing`) ask "does this section have an
  // unresolved save failure right now" without flushAndCloseBeforeFinalize
  // needing its own copy of the per-field bookkeeping — it reads
  // `failedFieldsRef` fresh on every call, so no re-registration is needed
  // when the set's contents change.
  useEffect(() => {
    if (!registerFailureStatus) return undefined;
    return registerFailureStatus(() => failedFieldsRef.current.size > 0);
  }, [registerFailureStatus]);

  // Lazy-initialized so a card that mounts ALREADY open (e.g. remounted by a
  // reload-triggered `key` bump while it was open) still has something valid
  // to restore — the closed→open transition below only fires for a LATER
  // transition, not for openness that was already true on the first render.
  const [snapshot, setSnapshot] = useState<DocumentValuePatch | null>(() =>
    isOpen ? snapshotSection(fields, values) : null
  );
  const [confirmingDiscard, setConfirmingDiscard] = useState(false);
  const [discardError, setDiscardError] = useState<string | null>(null);
  const fieldsBodyRef = useRef<HTMLDivElement>(null);
  const keepEditingRef = useRef<HTMLButtonElement>(null);
  // Row to focus once a field mounts in edit mode — set when a read-mode row
  // itself requests editing (a goal card's "Edit goal"), rather than the
  // generic Edit button opening the whole section (plan 2026-10-02-002, Phase
  // 3). Cleared on the generic Edit/Start-editing path so a later plain Edit
  // doesn't land back on a stale row from an earlier visit.
  const [pendingFocusRowKey, setPendingFocusRowKey] = useState<string | undefined>(undefined);
  const openSection = () => {
    setPendingFocusRowKey(undefined);
    onOpen();
  };
  const editRow = (rowKey: string) => {
    setPendingFocusRowKey(rowKey);
    onOpen();
  };

  useEffect(
    () => () => {
      if (savedTimerRef.current) clearTimeout(savedTimerRef.current);
    },
    []
  );

  // Reconciles one settled save (ok or not) against `failedFieldsRef`: a
  // success clears exactly the keys IT touched (never a different field's
  // still-outstanding failure); a failure records each touched key with the
  // value that failed. The resulting banner/Done-gate reflects the UNION of
  // every field's last outcome, not just this one call's — the fix for "a
  // failed save masked by a later success".
  const settleSave = useCallback(
    (ok: boolean, patch: DocumentValuePatch) => {
      pendingCountRef.current -= 1;
      if (ok) {
        for (const key of Object.keys(patch)) failedFieldsRef.current.delete(key);
      } else {
        for (const [key, value] of Object.entries(patch)) failedFieldsRef.current.set(key, value);
      }
      setFailedFields(
        Array.from(failedFieldsRef.current.keys(), (key) => ({
          key,
          label: fields.find((f) => f.fieldKey === key)?.label || key,
        }))
      );
      // A failure is surfaced immediately, even while another field's save is
      // still queued (`pendingCountRef.current > 0`) — the previous version
      // only recomputed status once the LAST-settling save reached 0 pending,
      // so a failure that settled first could be overwritten by a sibling
      // save's later success.
      if (failedFieldsRef.current.size > 0) {
        setSaveStatus('error');
      } else if (pendingCountRef.current === 0) {
        setSaveStatus('saved');
        savedTimerRef.current = setTimeout(() => setSaveStatus('idle'), SAVED_LINGER_MS);
      }
    },
    [fields]
  );

  // Every field editor in this section saves through this wrapper (instead of
  // the raw saveValues) so the header can show THIS section's own save state
  // — several sections can be mid-save independently.
  const wrappedSave = useCallback(
    async (patch: DocumentValuePatch): Promise<SaveResult> => {
      // Recorded at ENTRY (before the await) so Discard — which can run
      // `await sectionFlushRegistry.flushAll()` while this call is still in
      // flight — already sees that a save was attempted, rather than racing it.
      saveAttemptedRef.current = true;
      pendingCountRef.current += 1;
      setSaveStatus('saving');
      if (savedTimerRef.current) {
        clearTimeout(savedTimerRef.current);
        savedTimerRef.current = null;
      }
      try {
        const result = await saveValues(patch);
        settleSave(result.ok, patch);
        return result;
      } catch (err) {
        // `saveValues` (use-document-instance.ts) never actually throws — it
        // resolves `{ok:false, ...}` on every failure path — but this stays
        // defensive rather than assuming that forever. Not a NEW throw: field
        // saveFns must never throw on `{ok:false}` (that would re-queue the
        // value in the field's own autosave and re-send it after Discard).
        settleSave(false, patch);
        throw err;
      }
    },
    [saveValues, settleSave]
  );

  // Resends exactly the fields that currently have an unresolved failure,
  // using the value THAT failed (not the stale `values` prop, which only
  // reflects the last successfully-saved state) — the field's own autosave
  // has nothing queued to retry on its own (see `failedFieldsRef` above).
  // Flushes first: a field can carry a NEWER edit than the one that failed
  // (its own debounce hasn't fired yet, or a blur flush it's mid-flight —
  // mousedown on this very button blurs the field BEFORE the click handler
  // runs), and `failedFieldsRef` only reflects the last SETTLED outcome.
  // Rebuilding the patch only after that flush settles means a newer edit's
  // own save always lands first on the serialized chain, and this never
  // resends a now-stale value behind it.
  const handleRetryFailedSaves = async () => {
    setRetrying(true);
    try {
      await sectionFlushRegistry.flushAll();
      const patch: DocumentValuePatch = Object.fromEntries(failedFieldsRef.current);
      if (Object.keys(patch).length === 0) return;
      await wrappedSave(patch);
    } finally {
      setRetrying(false);
    }
  };

  // Snapshot on the closed→open transition (Edit click or the "E" shortcut).
  // `isOpen` is owned by the parent (useSectionEditing), so this reacts to it
  // by comparing against a STATE-tracked previous value DURING RENDER — React's
  // "adjust state during render" pattern (see react.dev: storing information
  // from previous renders). Refs can't back the comparison here — reading or
  // writing a ref during render is itself disallowed — so both the "was it
  // open last render" flag and the snapshot are plain state.
  const [wasOpen, setWasOpen] = useState(isOpen);
  if (isOpen !== wasOpen) {
    setWasOpen(isOpen);
    if (isOpen) {
      setSnapshot(snapshotSection(fields, values));
      setSaveStatus('idle');
      setConfirmingDiscard(false);
      setDiscardError(null);
      setFailedFields([]);
    }
  }

  // Pure DOM focus move (no state updates): into the first field when opening,
  // back to Edit when closing. `hasMountedRef` is read/written only here, inside
  // the effect, which is where ref access belongs — it just skips the
  // "just closed" branch on the very first render (every section starts closed).
  const hasMountedRef = useRef(false);
  useEffect(() => {
    const firstRun = !hasMountedRef.current;
    hasMountedRef.current = true;
    if (isOpen) {
      // Fresh slate for "has a save been attempted / did the last one fail"
      // each time a section opens — runs before any field below can possibly
      // have dispatched a save (that needs a user interaction, which can't
      // happen before this commit), so it never races `wrappedSave`.
      saveAttemptedRef.current = false;
      failedFieldsRef.current = new Map();
      confirmingDiscardRef.current = false;
      // A specific row's own focused editor (e.g. GoalEditor) already claims
      // focus on its own mount when a row-level edit was requested — moving
      // focus again here would fight it (and did: the generic query below
      // can't see a row's inputs before THAT editor is even selected).
      if (pendingFocusRowKey) return undefined;
      const raf = requestAnimationFrame(() => {
        const body = fieldsBodyRef.current;
        if (!body) return;
        // Two-step, not one combined selector: a RichText field's toolbar
        // renders its (focusable) formatting buttons BEFORE the contenteditable
        // in DOM order, so a single comma-separated query that includes
        // `button` would match the toolbar's first button (Bold) instead of
        // the field itself. Only fall back to a button — e.g. "Add goal", or a
        // card's own "Edit goal" — when the opened view has no actual editable
        // control at all, which is exactly the Goals/Services card list before
        // any row is expanded.
        const editable = body.querySelector<HTMLElement>('input,textarea,select,[contenteditable="true"]');
        if (editable) {
          editable.focus();
          return;
        }
        body.querySelector<HTMLButtonElement>('button:not(:disabled)')?.focus();
      });
      return () => cancelAnimationFrame(raf);
    }
    if (!firstRun) document.getElementById(editButtonDomId(section.id))?.focus();
    return undefined;
    // eslint-disable-next-line react-hooks/exhaustive-deps -- pendingFocusRowKey only ever changes in the same commit as isOpen flipping true (see editRow/openSection above), so it needs no dependency entry of its own
  }, [isOpen, section.id]);

  // Focuses "Keep editing" the moment the confirmation appears — it's the
  // non-destructive default, and a screen reader user landing on a brand-new
  // row of controls needs a focus target to be told it moved at all. On the
  // reverse transition (cancelled via "Keep editing", not confirmed-and-closed
  // — that path's focus is already owned by the isOpen effect above), returns
  // focus to the Discard button that opened the confirmation. Plain effect,
  // not a requestAnimationFrame off the click handler: it fires synchronously
  // in the same commit, so nothing is left armed to later fire against some
  // OTHER section's same-numbered DOM id once this one closes.
  const wasConfirmingDiscardRef = useRef(false);
  useEffect(() => {
    if (confirmingDiscard) keepEditingRef.current?.focus();
    else if (wasConfirmingDiscardRef.current) document.getElementById(discardButtonDomId(section.id))?.focus();
    wasConfirmingDiscardRef.current = confirmingDiscard;
  }, [confirmingDiscard, section.id]);

  const handleDone = async () => {
    await sectionFlushRegistry.flushAll();
    // A save that's still failing after the flush (including one retried by
    // the flush itself) leaves the section open with the failure banner below
    // visible instead of quietly discarding the local edit the close would
    // otherwise imply. `failedFieldsRef` — not a single last-outcome flag —
    // so a DIFFERENT field's later success here never hides this one's.
    if (failedFieldsRef.current.size > 0) return;
    onClose();
  };

  const handleDiscardClick = async () => {
    // Drain any still-pending debounced edit FIRST — otherwise a save that
    // hasn't reached its own debounce yet (or is mid-flight) can land AFTER
    // this decides "nothing to discard" and closes, via the unmount flush
    // that follows, with no restore to undo it.
    await sectionFlushRegistry.flushAll();
    const changed = saveAttemptedRef.current || (snapshot != null && !patchesEqual(snapshotSection(fields, values), snapshot));
    if (!changed) {
      onClose();
      return;
    }
    setDiscardError(null);
    confirmingDiscardRef.current = true;
    setConfirmingDiscard(true);
  };

  const confirmDiscard = async () => {
    if (!snapshot) {
      // Nothing to restore — but a field could still have failed earlier
      // (handleDiscardClick's own `changed` check reads `saveAttemptedRef`,
      // not just the snapshot diff), so this exit must clear the record too,
      // the same as the restored-successfully path below.
      failedFieldsRef.current = new Map();
      setFailedFields([]);
      confirmingDiscardRef.current = false;
      setConfirmingDiscard(false);
      onClose();
      return;
    }
    setDiscardError(null);
    setRestoring(true);
    try {
      // Fields are disabled the instant `confirmingDiscard` is true, but a
      // debounce armed just before that disable took effect could still be
      // sitting unflushed — drain it before the restore so it can never land
      // AFTER the restore and silently resurrect part of the discarded edit.
      await sectionFlushRegistry.flushAll();
      // The restore itself — the fields' own unmount flush that follows
      // `onClose()` has nothing left pending to redo.
      const result = await saveValues(snapshot);
      if (result.ok) {
        // Only close if this is still the confirmation being looked at — read
        // the ref rather than assume "Keep editing" (disabled while
        // `restoring`) couldn't have raced it some other way.
        if (confirmingDiscardRef.current) {
          // The restore just resent EVERY field's pre-edit value (via raw
          // `saveValues`, not `wrappedSave`), including whichever one(s)
          // previously failed — clear the failure record now, BEFORE
          // closing, rather than leaving it for the next reopen. Otherwise
          // the closed card would still show "Couldn't save" with a Retry
          // that would resend the just-discarded value, and the registered
          // getter would keep blocking Finalize for a section that is no
          // longer even open.
          failedFieldsRef.current = new Map();
          setFailedFields([]);
          confirmingDiscardRef.current = false;
          setConfirmingDiscard(false);
          onClose();
        }
      } else if (result.conflict) {
        setDiscardError('This document changed elsewhere. Reload to continue.');
      } else {
        setDiscardError(result.message || 'Could not discard changes. Please try again.');
      }
    } finally {
      setRestoring(false);
    }
  };

  const badge = isOpen ? (
    <Badge variant="success" data-testid={`section-${section.id}-status`}>
      Editing
    </Badge>
  ) : required > 0 ? (
    <Badge variant="error" data-testid={`section-${section.id}-status`}>
      {required} required item{required === 1 ? '' : 's'}
    </Badge>
  ) : advisory > 0 ? (
    <Badge variant="warning" data-testid={`section-${section.id}-status`}>
      {advisory} item{advisory === 1 ? '' : 's'} to review
    </Badge>
  ) : null;

  return (
    <Card
      id={sectionDomId(section.id)}
      tabIndex={-1}
      className={cn(
        'scroll-mt-4 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-500',
        isOpen && 'border-2 border-brand-teal-400 shadow-sm'
      )}
      data-testid={`section-${section.id}`}
    >
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <h2 className="font-serif text-lg text-brand-slate-800">{section.title || 'Untitled section'}</h2>
        {badge}
        {!isOpen ? (
          <Button
            variant="secondary"
            size="sm"
            id={editButtonDomId(section.id)}
            className="ml-auto"
            disabled={disabled}
            onClick={openSection}
            data-testid={`section-${section.id}-edit`}
          >
            Edit
          </Button>
        ) : confirmingDiscard ? (
          <div className="ml-auto flex flex-wrap items-center gap-3 text-sm">
            {/* Once a restore attempt has failed, the error message below takes
                over as the one `role="alert"` — two simultaneous alert regions
                both fire, which is noisy and (worse) ambiguous to tests and
                screen readers about which one is the actual news. */}
            <span className="text-brand-slate-700" role={discardError ? undefined : 'alert'}>
              Discard the changes saved since you started editing?
            </span>
            <button
              ref={keepEditingRef}
              type="button"
              className="text-brand-slate-600 hover:underline disabled:cursor-not-allowed disabled:opacity-50"
              disabled={restoring}
              onClick={() => {
                confirmingDiscardRef.current = false;
                setConfirmingDiscard(false);
              }}
              data-testid={`section-${section.id}-discard-cancel`}
            >
              Keep editing
            </button>
            <Button
              variant="danger"
              size="sm"
              disabled={restoring}
              onClick={() => void confirmDiscard()}
              data-testid={`section-${section.id}-discard-confirm`}
            >
              Discard changes
            </Button>
          </div>
        ) : (
          <div className="ml-auto flex items-center gap-3 text-sm">
            {failedFields.length === 0 && <AutosaveIndicator status={saveStatus} />}
            <button
              id={discardButtonDomId(section.id)}
              type="button"
              className="text-brand-slate-600 hover:underline"
              onClick={() => void handleDiscardClick()}
              data-testid={`section-${section.id}-discard`}
            >
              Discard changes
            </button>
            <Button size="sm" onClick={() => void handleDone()} data-testid={`section-${section.id}-done`}>
              Done
            </Button>
          </div>
        )}
      </div>

      {confirmingDiscard && discardError && (
        <p className="mb-3 text-sm text-brand-danger-700" role="alert">
          {discardError}
        </p>
      )}

      {/* Suppressed during the discard confirmation so there is never more
          than one `role="alert"` region at once (see the comment above the
          confirmation text) — Discard already supersedes any unsaved failure.
          Gated on `isOpen` too: a CLOSED card must never show a stale failure
          (or offer a Retry that would resend it) — the normal clearing path
          is confirmDiscard above, but this is the backstop. */}
      {isOpen && !confirmingDiscard && failedFields.length > 0 && (
        <div className="mb-3 flex flex-wrap items-center gap-3 text-sm text-brand-danger-700" role="alert">
          <span>
            Couldn&apos;t save {failedFields.map((f) => f.label).join(', ')} — check your connection and try again
          </span>
          <Button
            variant="secondary"
            size="sm"
            disabled={retrying}
            onClick={() => void handleRetryFailedSaves()}
            data-testid={`section-${section.id}-retry`}
          >
            {retrying ? 'Retrying…' : 'Retry'}
          </Button>
        </div>
      )}

      {isOpen ? (
        fields.length === 0 ? (
          <p className="text-sm text-brand-slate-500">No fields.</p>
        ) : (
          <div ref={fieldsBodyRef} className="space-y-4">
            <DocumentFlushContext.Provider value={sectionFlushRegistry}>
              {fields.map((field) => (
                <DocumentField
                  key={field.id}
                  field={field}
                  value={values[field.fieldKey]}
                  disabled={disabled || confirmingDiscard || restoring}
                  onSave={wrappedSave}
                  initialFocusRowKey={pendingFocusRowKey}
                />
              ))}
            </DocumentFlushContext.Provider>
          </div>
        )
      ) : isEmpty ? (
        <p className="text-sm text-brand-slate-500">
          Not started.{' '}
          <button
            type="button"
            className="text-brand-teal-600 hover:underline"
            disabled={disabled}
            onClick={openSection}
            data-testid={`section-${section.id}-start-editing`}
          >
            Start editing
          </button>
        </p>
      ) : fields.length === 0 ? (
        <p className="text-sm text-brand-slate-500">No fields.</p>
      ) : (
        <div className="space-y-4">
          {fields.map((field) => (
            <ReadField
              key={field.id}
              field={field}
              value={values[field.fieldKey]}
              onEditRow={editRow}
              // The section's own heading already names the field when it's
              // the section's only one — a second, repeated label right below
              // it is pure noise.
              hideLabel={fields.length === 1}
            />
          ))}
        </div>
      )}
    </Card>
  );
}
