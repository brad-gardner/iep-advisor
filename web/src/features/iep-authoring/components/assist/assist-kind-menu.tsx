import { ASSIST_KIND_LABELS } from '../../api/iep-assist-types';
import type { AssistKind } from '../../api/iep-assist-types';

interface AssistKindMenuProps {
  // Which kinds to offer; lets a row hide kinds that don't apply to it.
  kinds: AssistKind[];
  onPick: (kind: AssistKind) => void;
  testIdPrefix: string;
}

// A small popdown of the available assist actions.
export function AssistKindMenu({ kinds, onPick, testIdPrefix }: AssistKindMenuProps) {
  return (
    <div
      className="absolute right-0 z-10 mt-1 w-48 rounded-card border border-brand-slate-200 bg-white p-1 shadow-md"
      role="menu"
    >
      {kinds.map((kind) => (
        <button
          key={kind}
          type="button"
          role="menuitem"
          onClick={() => onPick(kind)}
          className="block w-full rounded-input px-3 py-2 text-left text-[13px] text-brand-slate-700 hover:bg-brand-teal-50"
          data-testid={`${testIdPrefix}-kind-${kind}`}
        >
          {ASSIST_KIND_LABELS[kind]}
        </button>
      ))}
    </div>
  );
}
