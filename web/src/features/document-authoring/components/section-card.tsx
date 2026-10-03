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
  const [dirty, setDirty] = useState(false);
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
      pendingCountRef.current += 1;
      setSaveStatus('saving');
      if (savedTimerRef.current) {
        clearTimeout(savedTimerRef.current);
        savedTimerRef.current = null;
      }
      try {
        const result = await saveValues(patch);
        pendingCountRef.current -= 1;
        if (pendingCountRef.current === 0) {
          if (result.ok) {
            setDirty(true);
            setSaveStatus('saved');
            savedTimerRef.current = setTimeout(() => setSaveStatus('idle'), SAVED_LINGER_MS);
          } else {
            setSaveStatus('error');
          }
        }
        return result;
      } catch (err) {
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
      setDirty(false);
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
      const raf = requestAnimationFrame(() => {
        fieldsBodyRef.current
          ?.querySelector<HTMLElement>('input,textarea,select,[contenteditable="true"]')
          ?.focus();
      });
      return () => cancelAnimationFrame(raf);
    }
    if (!firstRun) document.getElementById(editButtonDomId(section.id))?.focus();
    return undefined;
  }, [isOpen, section.id]);

  const handleDone = async () => {
    await sectionFlushRegistry.flushAll();
    onClose();
  };

  const handleDiscardClick = () => {
    if (!dirty) {
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
            <span className="text-brand-slate-700">Discard the changes saved since you started editing?</span>
            <button
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
              type="button"
              className="text-brand-slate-600 hover:underline"
              onClick={handleDiscardClick}
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
            <ReadField key={field.id} field={field} value={values[field.fieldKey]} onEditRow={editRow} />
          ))}
        </div>
      )}
    </Card>
  );
}
