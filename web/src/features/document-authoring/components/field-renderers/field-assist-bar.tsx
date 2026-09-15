import { useCallback } from 'react';
import { assistField } from '../../api/document-assist-api';
import type { AssistKind } from '../../api/assist-types';
import { useDocumentEditorContext } from '../../hooks/document-editor-context';
import { AssistPopover } from '../assist/assist-popover';
import { PullFromStudentButton } from '../pull-from-student/pull-from-student-button';

interface FieldAssistBarProps {
  fieldKey: string;
  /** Row identity for Table rows; null for scalar fields. */
  rowId?: string | null;
  /** Kinds to offer (defaults to all three). */
  kinds?: AssistKind[];
  /** Apply the accepted suggestion / pulled entry to the field. */
  onApply: (text: string) => void;
  /** Offer "Pull from student" (narrative + goal targets only). */
  allowPull?: boolean;
  /** Awaited before an AI request so the pending autosave is persisted first. */
  beforeRequest?: () => Promise<void>;
  disabled?: boolean;
  testIdPrefix: string;
}

/**
 * The inline "AI help" + "Pull from student" affordances shared by scalar fields
 * and table rows. Renders nothing outside an editor context (previews) or when
 * the field is disabled, so read-only views never expose write affordances.
 */
export function FieldAssistBar({
  fieldKey,
  rowId = null,
  kinds,
  onApply,
  allowPull = false,
  beforeRequest,
  disabled,
  testIdPrefix,
}: FieldAssistBarProps) {
  const ctx = useDocumentEditorContext();
  const instanceId = ctx?.instanceId;
  const requestFn = useCallback(
    (kind: AssistKind) => assistField(instanceId ?? 0, fieldKey, rowId, kind),
    [instanceId, fieldKey, rowId]
  );
  if (!ctx || disabled) return null;

  return (
    <div className="mt-2 flex flex-wrap items-start gap-2" data-testid={`${testIdPrefix}-assist-bar`}>
      <AssistPopover
        requestFn={requestFn}
        kinds={kinds}
        onApply={onApply}
        beforeRequest={beforeRequest}
        testIdPrefix={`${testIdPrefix}-assist`}
      />
      {allowPull && (
        <PullFromStudentButton source={ctx.shareableEntries} onPick={onApply} testIdPrefix={`${testIdPrefix}-pull`} />
      )}
    </div>
  );
}
