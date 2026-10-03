import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { cn } from '@/lib/cn';
import type { AutosaveStatus } from '@/hooks/use-autosave';
import { useFlushRegistry } from '@/hooks/use-flush-registry';
import { AutosaveIndicator } from '@/features/admin/templates/components/autosave-indicator';
import type { DocumentValuePatch, TableRowValue, TemplateFieldDto, TemplateSectionDto } from '../types';
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
export function SectionCard({ section, values, disabled, saveValues, isOpen, onOpen, onClose, items }: SectionCardProps) {
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
  // The last-settled save's outcome, mirroring `use-document-instance.ts`'s
  // own `errorRef` — Done reads this (also after an `await flushAll()`) to
  // decide whether it's safe to close; `saveStatus` state would be just as
  // stale as a state-based flag for the same reason.
  const lastSaveOkRef = useRef(true);
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
        lastSaveOkRef.current = result.ok;
        pendingCountRef.current -= 1;
        if (pendingCountRef.current === 0) {
          if (result.ok) {
            setSaveStatus('saved');
            savedTimerRef.current = setTimeout(() => setSaveStatus('idle'), SAVED_LINGER_MS);
          } else {
            setSaveStatus('error');
          }
        }
        return result;
      } catch (err) {
        lastSaveOkRef.current = false;
        pendingCountRef.current -= 1;
        if (pendingCountRef.current === 0) setSaveStatus('error');
        throw err;
      }
    },
    [saveValues]
  );

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
      lastSaveOkRef.current = true;
      // A specific row's own focused editor (e.g. GoalEditor) already claims
      // focus on its own mount when a row-level edit was requested — moving
      // focus again here would fight it (and did: the generic query below
      // can't see a row's inputs before THAT editor is even selected).
      if (pendingFocusRowKey) return undefined;
      const raf = requestAnimationFrame(() => {
        // Falls back to a button (e.g. "Add goal", or a card's own "Edit
        // goal") when the section opened into a view with no input/textarea/
        // select/contenteditable at all — the Goals/Services card list, before
        // any row is expanded, is exactly that case.
        fieldsBodyRef.current
          ?.querySelector<HTMLElement>('input,textarea,select,[contenteditable="true"],button')
          ?.focus();
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
    // the flush itself) leaves the section open with the failure visible
    // (AutosaveIndicator's "Save failed" pill, role="alert") instead of
    // quietly discarding the local edit the close would otherwise imply.
    if (!lastSaveOkRef.current) return;
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
    setConfirmingDiscard(true);
  };

  const confirmDiscard = async () => {
    if (!snapshot) {
      setConfirmingDiscard(false);
      onClose();
      return;
    }
    setDiscardError(null);
    // The flush already ran (in handleDiscardClick, before the confirmation
    // was even shown), so this restore is the only save left — the fields'
    // own unmount flush that follows `onClose()` has nothing pending to redo.
    const result = await saveValues(snapshot);
    if (result.ok) {
      setConfirmingDiscard(false);
      onClose();
    } else if (result.conflict) {
      setDiscardError('This document changed elsewhere. Reload to continue.');
    } else {
      setDiscardError(result.message || 'Could not discard changes. Please try again.');
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
              className="text-brand-slate-600 hover:underline"
              onClick={() => setConfirmingDiscard(false)}
              data-testid={`section-${section.id}-discard-cancel`}
            >
              Keep editing
            </button>
            <Button
              variant="danger"
              size="sm"
              onClick={() => void confirmDiscard()}
              data-testid={`section-${section.id}-discard-confirm`}
            >
              Discard changes
            </Button>
          </div>
        ) : (
          <div className="ml-auto flex items-center gap-3 text-sm">
            <AutosaveIndicator status={saveStatus} />
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
                  disabled={disabled}
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
