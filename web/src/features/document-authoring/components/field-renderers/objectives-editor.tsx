import { useRef, useState } from 'react';
import { ChevronDown, ChevronUp, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { coerceObjectives, emptyObjective, isKeyedObjective, type KeyedObjective } from '../../lib/objective-rows';

/**
 * Merges server-assigned `_rowId`s that `adoptRowObjectiveIds` (table-field.tsx)
 * already wrote into the goal row's `_objectives` cell back into THIS editor's
 * own objectives state — additively, by client key, never replacing content.
 * Needed because the editor deliberately never re-derives its list from props
 * after mount (that would be the 2026-09-15 bug), so the one thing it DOES
 * still need from the row — an id a brand-new objective didn't have yet — has
 * to be threaded back in explicitly instead. `incoming` is only ever the rich
 * `KeyedObjective[]` shape when it is an echo of what this editor itself last
 * wrote (plain JSON — the shape on first mount — has no `.key`/`.cells` to
 * match on, so it is harmlessly ignored here).
 */
function mergeAdoptedIds(current: KeyedObjective[], incoming: unknown): KeyedObjective[] {
  if (!Array.isArray(incoming)) return current;
  const idByKey = new Map<string, string>();
  for (const item of incoming) {
    if (isKeyedObjective(item) && item.cells._rowId) idByKey.set(item.key, item.cells._rowId);
  }
  if (idByKey.size === 0) return current;

  let changed = false;
  const next = current.map((o) => {
    if (o.cells._rowId) return o;
    const id = idByKey.get(o.key);
    if (!id) return o;
    changed = true;
    return { ...o, cells: { ...o.cells, _rowId: id } };
  });
  return changed ? next : current;
}

const inputClass =
  'w-full px-2 py-1.5 bg-white rounded-input text-brand-slate-800 text-sm border border-brand-slate-200 focus:outline-none focus:border-brand-teal-500 focus:ring-[3px] focus:ring-brand-teal-50 transition-colors';

/** Grows a textarea to fit its content (reset-then-measure is the standard,
 *  flicker-free technique — shrinking first is what lets it also get smaller). */
function autoGrow(el: HTMLTextAreaElement): void {
  el.style.height = 'auto';
  el.style.height = `${el.scrollHeight}px`;
}

interface ObjectivesEditorProps {
  /** The goal row's raw `_objectives` cell — read ONCE at mount (see
   *  `objective-rows.ts`'s header comment: this editor owns its own stable
   *  identity thereafter, same as `TableField` owns `rows`/`rowsRef`). */
  value: unknown;
  disabled?: boolean;
  testIdPrefix: string;
  /** Every mutation reports the full current list upward so the goal row's
   *  `_objectives` cell stays current for the next save; `immediate` asks the
   *  table-level autosave to flush right away (add/remove/reorder) rather than
   *  wait out its debounce (typing). */
  onChange: (objectives: KeyedObjective[], immediate: boolean) => void;
  /** Flushes the table-level autosave — called on blur, same as every other
   *  cell in a goal row, so navigating away persists a just-typed edit. */
  flush: () => Promise<void>;
}

/**
 * Ordered objectives/benchmarks list for a goal (plan 2026-10-02-002, Phase
 * 3): add/remove/reorder with keyboard-accessible up/down buttons (no drag —
 * the plan explicitly allows buttons instead, and they need no pointer-only
 * affordance to reach). Each objective keeps the row-identity invariants from
 * `docs/solutions/ui-bugs/2026-09-15-...`: the client key never changes, the
 * list manages its own state instead of re-deriving from `value` on every
 * parent render, and new objectives are always appended (never inserted) so a
 * still-blank one being dropped server-side — same "reduced to nothing" rule
 * as a table row — never shifts an already-identified sibling out of position.
 *
 * Unlike `TableField`'s `rows`/`rowsRef` (which needs a synchronous ref
 * because its autosave reads the latest value from an async callback that can
 * resolve well after the render that triggered it), every mutation here
 * starts and ends inside one synchronous event handler, so the component's
 * own `objectives` state — the closure a handler sees is always the latest
 * completed render's — is on its own a sufficient, always-current base. That
 * also keeps this component free of ref reads/writes during render, which a
 * ref-based mirror could not avoid once a prop-driven id adoption (below)
 * also needed to update the same value.
 */
export function ObjectivesEditor({ value, disabled, testIdPrefix, onChange, flush }: ObjectivesEditorProps) {
  const [objectives, setObjectives] = useState<KeyedObjective[]>(() => coerceObjectives(value));
  const descRefs = useRef(new Map<string, HTMLTextAreaElement>());
  // Scopes the post-mutation focus lookups below to this editor's own DOM, by
  // `data-testid` rather than a second per-row ref map — index-addressed,
  // which is exactly what a move/remove needs since the control to land on is
  // defined by WHERE an item ends up, not which item it is.
  const containerRef = useRef<HTMLDivElement>(null);
  // Polite live-region text for a move/remove — a focus change alone isn't
  // announced by itself, and the control that ends up focused (an arrow, a
  // sibling's description, "Add objective") doesn't say what just happened.
  const [announcement, setAnnouncement] = useState('');

  // `value` (the goal row's `_objectives` cell) changes after every save —
  // including one that just adopted a brand-new objective's server id. This
  // editor otherwise never re-derives from `value` (see the class comment
  // above), so an adopted id has to be merged in explicitly, by client key,
  // the moment `value` changes — React's "adjust state during render" pattern
  // (react.dev/learn/you-might-not-need-an-effect). `lastSeenValue` makes the
  // comparison fire only on an actual change, and `mergeAdoptedIds` only ever
  // ADDS a missing `_rowId` — it cannot touch content, so a row someone is
  // mid-keystroke in is never at risk.
  const [lastSeenValue, setLastSeenValue] = useState(value);
  if (value !== lastSeenValue) {
    setLastSeenValue(value);
    const merged = mergeAdoptedIds(objectives, value);
    if (merged !== objectives) setObjectives(merged);
  }

  const mutate = (updater: (current: KeyedObjective[]) => KeyedObjective[], immediate: boolean) => {
    const next = updater(objectives);
    setObjectives(next);
    onChange(next, immediate);
  };

  const updateField = (key: string, field: 'description' | 'criteria' | 'targetDate', text: string) =>
    mutate((current) => current.map((o) => (o.key === key ? { ...o, cells: { ...o.cells, [field]: text } } : o)), false);

  const addObjective = () => {
    const created = emptyObjective();
    mutate((current) => [...current, created], true);
    requestAnimationFrame(() => descRefs.current.get(created.key)?.focus());
  };

  // After removing the objective at `index`, focus the one that slides up
  // into its slot (its description) so keyboard use can keep working down
  // the list; with nothing left there (the removed item was last, or the
  // list is now empty), "Add objective" is the nearest live control.
  const removeObjective = (key: string) => {
    const index = objectives.findIndex((o) => o.key === key);
    mutate((current) => current.filter((o) => o.key !== key), true);
    // Position + remaining count, not a fixed string — a screen reader's live
    // region only reliably re-announces text that actually changed, and
    // removing two objectives in a row (often landing on the SAME index each
    // time, since the next one slides up into the removed slot) previously
    // sent the exact same "Objective removed" text twice in a row.
    const remaining = objectives.length - 1;
    setAnnouncement(`Objective ${index + 1} removed, ${remaining} remaining`);
    requestAnimationFrame(() => {
      const container = containerRef.current;
      if (!container || index === -1) return;
      const next = container.querySelector<HTMLElement>(`[data-testid="${testIdPrefix}-objective-${index}-description"]`);
      if (next) next.focus();
      else container.querySelector<HTMLElement>(`[data-testid="${testIdPrefix}-objectives-add"]`)?.focus();
    });
  };

  const moveObjective = (key: string, direction: -1 | 1) => {
    const index = objectives.findIndex((o) => o.key === key);
    const target = index + direction;
    if (index === -1 || target < 0 || target >= objectives.length) return; // mirrors the disabled-button bounds

    mutate((current) => {
      const i = current.findIndex((o) => o.key === key);
      const t = i + direction;
      if (i === -1 || t < 0 || t >= current.length) return current;
      const next = [...current];
      [next[i], next[t]] = [next[t], next[i]];
      return next;
    }, true);
    setAnnouncement(`Objective moved to position ${target + 1}`);

    // The arrow the user just pressed may now sit at a list end — its own
    // direction disabled there (same rule the `disabled` props below use) —
    // so land on the opposite arrow of the objective that moved, now at
    // `target`, instead of a button that just went disabled under focus.
    const pressed = direction === -1 ? 'up' : 'down';
    const opposite = direction === -1 ? 'down' : 'up';
    const pressedNowDisabled = (direction === -1 && target === 0) || (direction === 1 && target === objectives.length - 1);
    const suffix = pressedNowDisabled ? opposite : pressed;
    requestAnimationFrame(() => {
      containerRef.current
        ?.querySelector<HTMLElement>(`[data-testid="${testIdPrefix}-objective-${target}-${suffix}"]`)
        ?.focus();
    });
  };

  return (
    <div ref={containerRef}>
      <div aria-live="polite" className="sr-only">
        {announcement}
      </div>
      <div className="flex items-center">
        <h4 className="text-sm font-semibold text-brand-slate-700">Short-term objectives / benchmarks</h4>
        <span className="ml-2 text-xs text-brand-slate-500">ordered</span>
        <Button
          variant="ghost"
          size="sm"
          className="ml-auto"
          disabled={disabled}
          onClick={addObjective}
          data-testid={`${testIdPrefix}-objectives-add`}
        >
          <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
          Add objective
        </Button>
      </div>
      {objectives.length === 0 ? (
        <p className="mt-1 text-sm text-brand-slate-500">No objectives yet.</p>
      ) : (
        <ol className="mt-2 space-y-2">
          {objectives.map((o, index) => {
            const descId = `${testIdPrefix}-objective-${index}-description`;
            const criteriaId = `${testIdPrefix}-objective-${index}-criteria`;
            const dateId = `${testIdPrefix}-objective-${index}-date`;
            return (
              <li
                key={o.key}
                className="rounded-input border border-brand-slate-200 p-3"
                data-testid={`${testIdPrefix}-objective-${index}`}
              >
                <div className="flex items-start gap-3">
                  <div className="flex shrink-0 flex-col gap-0.5 pt-1" role="group" aria-label={`Reorder objective ${index + 1}`}>
                    <button
                      type="button"
                      className="rounded text-brand-slate-400 hover:text-brand-teal-600 disabled:opacity-30"
                      disabled={disabled || index === 0}
                      onClick={() => moveObjective(o.key, -1)}
                      aria-label={`Move objective ${index + 1} up`}
                      data-testid={`${testIdPrefix}-objective-${index}-up`}
                    >
                      <ChevronUp className="h-4 w-4" aria-hidden="true" />
                    </button>
                    <button
                      type="button"
                      className="rounded text-brand-slate-400 hover:text-brand-teal-600 disabled:opacity-30"
                      disabled={disabled || index === objectives.length - 1}
                      onClick={() => moveObjective(o.key, 1)}
                      aria-label={`Move objective ${index + 1} down`}
                      data-testid={`${testIdPrefix}-objective-${index}-down`}
                    >
                      <ChevronDown className="h-4 w-4" aria-hidden="true" />
                    </button>
                  </div>
                  <div className="min-w-0 flex-1 space-y-2">
                    <label htmlFor={descId} className="sr-only">
                      Objective {index + 1} description
                    </label>
                    <textarea
                      id={descId}
                      ref={(el) => {
                        if (el) descRefs.current.set(o.key, el);
                        else descRefs.current.delete(o.key);
                      }}
                      rows={2}
                      className={`${inputClass} min-h-[3rem] resize-none`}
                      value={o.cells.description}
                      disabled={disabled}
                      onChange={(e) => {
                        updateField(o.key, 'description', e.target.value);
                        autoGrow(e.target);
                      }}
                      onBlur={() => void flush()}
                      data-testid={descId}
                    />
                    <div className="grid gap-2 sm:grid-cols-2">
                      <label className="block text-xs font-medium text-brand-slate-600" htmlFor={criteriaId}>
                        Criteria
                        <input
                          id={criteriaId}
                          type="text"
                          className={`${inputClass} mt-1 font-normal`}
                          value={o.cells.criteria}
                          disabled={disabled}
                          onChange={(e) => updateField(o.key, 'criteria', e.target.value)}
                          onBlur={() => void flush()}
                          data-testid={criteriaId}
                        />
                      </label>
                      <label className="block text-xs font-medium text-brand-slate-600" htmlFor={dateId}>
                        Target date
                        <input
                          id={dateId}
                          type="text"
                          className={`${inputClass} mt-1 font-normal`}
                          value={o.cells.targetDate}
                          disabled={disabled}
                          onChange={(e) => updateField(o.key, 'targetDate', e.target.value)}
                          onBlur={() => void flush()}
                          data-testid={dateId}
                        />
                      </label>
                    </div>
                  </div>
                  <button
                    type="button"
                    className="shrink-0 text-brand-slate-400 hover:text-brand-danger-700 disabled:opacity-30"
                    disabled={disabled}
                    onClick={() => removeObjective(o.key)}
                    aria-label={`Remove objective ${index + 1}`}
                    data-testid={`${testIdPrefix}-objective-${index}-remove`}
                  >
                    <Trash2 className="h-4 w-4" aria-hidden="true" />
                  </button>
                </div>
              </li>
            );
          })}
        </ol>
      )}
    </div>
  );
}
