import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { AxiosError } from 'axios';
import { Button } from '@/components/ui/button';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { useToast } from '@/components/ui/toast';
import type { ApiResponse } from '@/types/api';
import { finalizeDocument, listAuthoredVersions } from '../api/documents-api';
import type {
  AuthoredDocumentVersionSummaryDto,
  DocumentInstanceStatus,
} from '../types';
import { FinalizeDocumentDialog } from './finalize-document-dialog';

interface FinalizeDocumentSectionProps {
  instanceId: number;
  studentId: number;
  documentTypeId: number;
  documentTypeDisplayName: string;
  status: DocumentInstanceStatus;
  // Flush every pending per-field autosave so the snapshot captures latest edits.
  flushBeforeFinalize: () => Promise<void>;
  // Fresh save snapshot read AFTER the flush — gates finalize so we never
  // snapshot stale data when a field's last autosave silently failed.
  getSaveState?: () => { hasError: boolean; conflict: boolean; pending: boolean };
  // Called after a successful finalize (e.g. to refresh a versions list).
  onFinalized?: (version: AuthoredDocumentVersionSummaryDto) => void;
}

function mapFinalizeError(status: number | undefined, message?: string): string {
  if (status === 403) return "You don't have permission to finalize this document.";
  if (status === 404) return 'This document no longer exists.';
  // 409 → state conflict (e.g. already finalizing). Prefer the server message.
  if (status === 409) return message || 'This document is already being finalized.';
  return message || 'Could not finalize the document. Please try again.';
}

// Owns the educator finalize flow for one document instance: open the confirm
// dialog, flush pending saves, POST finalize, then surface a 422 field list, a
// 409 state-conflict message, or a success Notice linking to the finalized
// versions. Mirrors iep-versions/finalize-section for the authored surface.
export function FinalizeDocumentSection({
  instanceId,
  studentId,
  documentTypeId,
  documentTypeDisplayName,
  status,
  flushBeforeFinalize,
  getSaveState,
  onFinalized,
}: FinalizeDocumentSectionProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<string[]>([]);
  const [finalized, setFinalized] = useState<AuthoredDocumentVersionSummaryDto | null>(null);
  const [nextVersionNumber, setNextVersionNumber] = useState<number | undefined>(undefined);
  const { show } = useToast();

  const canFinalize = status === 'Draft';

  // Derive the next version number for THIS document type (numbering is per
  // (student, docType)) — a best-effort hint only.
  useEffect(() => {
    let active = true;
    listAuthoredVersions(studentId)
      .then((res) => {
        if (!active || !res.success || !res.data) return;
        const max = res.data
          .filter((v) => v.documentTypeId === documentTypeId)
          .reduce((m, v) => Math.max(m, v.versionNumber), 0);
        setNextVersionNumber(max + 1);
      })
      .catch(() => {
        // Hint only; the dialog falls back to generic copy.
      });
    return () => {
      active = false;
    };
  }, [studentId, documentTypeId]);

  const openDialog = () => {
    setError(null);
    setValidationErrors([]);
    setIsOpen(true);
  };

  const closeDialog = () => {
    // Don't let Esc/backdrop dismiss mid-finalize — wait for the submit to settle.
    if (isSubmitting) return;
    setIsOpen(false);
    setError(null);
    setValidationErrors([]);
  };

  const handleConfirm = async () => {
    setIsSubmitting(true);
    setError(null);
    setValidationErrors([]);
    try {
      // Capture the latest edits before snapshotting.
      await flushBeforeFinalize();
      // Gate on the post-flush save state: never finalize (snapshot) stale data
      // when the latest edit failed to persist or a concurrent change latched.
      const saveState = getSaveState?.();
      if (saveState?.conflict) {
        setError(
          'This document changed elsewhere. Reload to get the latest values before finalizing.'
        );
        return;
      }
      if (saveState?.hasError || saveState?.pending) {
        setError('Your most recent edits could not be saved. Please retry them before finalizing.');
        return;
      }
      const res = await finalizeDocument(instanceId);
      if (res.success && res.data) {
        const version = res.data;
        setFinalized(version);
        setIsOpen(false);
        show({ message: `Finalized v${version.versionNumber}`, variant: 'success' });
        setNextVersionNumber(version.versionNumber + 1);
        onFinalized?.(version);
      } else {
        // A non-throwing failure envelope (rare) — surface message/errors.
        setValidationErrors(res.errors ?? []);
        setError(res.errors?.length ? null : res.message ?? 'Could not finalize the document.');
      }
    } catch (err) {
      if (err instanceof AxiosError) {
        const httpStatus = err.response?.status;
        const body = err.response?.data as ApiResponse<unknown> | undefined;
        // 422 → complete list of missing-required / invalid fields.
        if (httpStatus === 422 && body?.errors?.length) {
          setValidationErrors(body.errors);
        } else {
          setError(mapFinalizeError(httpStatus, body?.message));
        }
      } else {
        setError('Could not finalize the document. Please try again.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="space-y-3">
      <Modal
        open={isOpen}
        onClose={closeDialog}
        title={`Finalize this ${documentTypeDisplayName}`}
        size="md"
        data-testid="finalize-document-dialog"
      >
        <FinalizeDocumentDialog
          documentTypeDisplayName={documentTypeDisplayName}
          nextVersionNumber={nextVersionNumber}
          isSubmitting={isSubmitting}
          error={error}
          validationErrors={validationErrors}
          onConfirm={handleConfirm}
          onCancel={closeDialog}
        />
      </Modal>

      {finalized && (
        <Notice variant="success" title={`Finalized v${finalized.versionNumber}`}>
          An immutable version was created and the PDF is generating.{' '}
          <Link
            to={`/educator/students/${studentId}/authored-versions/${finalized.id}`}
            className="text-brand-teal-500 hover:underline"
            data-testid="view-finalized-version"
          >
            View version
          </Link>
          .{' '}
          <button
            type="button"
            onClick={() => setFinalized(null)}
            className="text-brand-slate-400 hover:underline"
            data-testid="dismiss-finalized"
          >
            Dismiss
          </button>
        </Notice>
      )}

      <Button
        variant="primary"
        onClick={openDialog}
        disabled={!canFinalize}
        data-testid="finalize-button"
      >
        Finalize
      </Button>
      {!canFinalize && (
        <p className="text-sm text-brand-slate-500">
          This document is {status.toLowerCase()} and cannot be finalized right now.
        </p>
      )}
    </div>
  );
}
