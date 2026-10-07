import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Link } from 'react-router-dom';
import { AxiosError } from 'axios';
import { Button } from '@/components/ui/button';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { useToast } from '@/components/ui/toast';
import type { ApiResponse } from '@/types/api';
import { finalizeDocument, listAuthoredVersions } from '../api/documents-api';
import { documentStatusLabel } from '../lib/document-status-label';
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
  // Flush every pending per-field autosave and close every open section so
  // the snapshot captures latest edits. Resolves false (instead of closing
  // anything) when the flush left an unresolved failure behind — a 409, a
  // save still pending, or a SECTION that still shows "Couldn't save" for one
  // of its fields — in which case the open section's own failure banner
  // already shows the educator what to fix, and finalize must not proceed.
  flushBeforeFinalize: () => Promise<boolean>;
  // Fresh save snapshot read AFTER the flush — gates finalize so we never
  // snapshot stale data when a field's last autosave silently failed.
  getSaveState?: () => { hasError: boolean; conflict: boolean; pending: boolean };
  // Called after a successful finalize (e.g. to refresh a versions list).
  onFinalized?: (version: AuthoredDocumentVersionSummaryDto) => void;
}

function mapFinalizeError(t: TFunction<'document-authoring'>, status: number | undefined, message?: string): string {
  if (status === 403) return t('finalizeSection.errorForbidden');
  if (status === 404) return t('finalizeSection.errorNotFound');
  // 409 → state conflict (e.g. already finalizing). Prefer the server message.
  if (status === 409) return message || t('finalizeSection.errorConflict');
  return message || t('finalizeSection.errorGeneric');
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
  const { t } = useTranslation('document-authoring');
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
      // Capture the latest edits before snapshotting; false means it refused
      // to close sections because something is still unresolved (see the
      // getSaveState() checks right below for which message applies).
      const canProceed = await flushBeforeFinalize();
      // Gate on the post-flush save state: never finalize (snapshot) stale data
      // when the latest edit failed to persist or a concurrent change latched.
      const saveState = getSaveState?.();
      if (saveState?.conflict) {
        setError(t('finalizeSection.conflictError'));
        return;
      }
      if (!canProceed || saveState?.hasError || saveState?.pending) {
        setError(t('finalizeSection.unsavedError'));
        return;
      }
      const res = await finalizeDocument(instanceId);
      if (res.success && res.data) {
        const version = res.data;
        setFinalized(version);
        setIsOpen(false);
        show({ message: t('finalizeSection.finalizedToast', { number: version.versionNumber }), variant: 'success' });
        setNextVersionNumber(version.versionNumber + 1);
        onFinalized?.(version);
      } else {
        // A non-throwing failure envelope (rare) — surface message/errors.
        setValidationErrors(res.errors ?? []);
        setError(res.errors?.length ? null : res.message ?? t('finalizeSection.errorGenericNoRetryHint'));
      }
    } catch (err) {
      if (err instanceof AxiosError) {
        const httpStatus = err.response?.status;
        const body = err.response?.data as ApiResponse<unknown> | undefined;
        // 422 → complete list of missing-required / invalid fields.
        if (httpStatus === 422 && body?.errors?.length) {
          setValidationErrors(body.errors);
        } else {
          setError(mapFinalizeError(t, httpStatus, body?.message));
        }
      } else {
        setError(t('finalizeSection.errorGeneric'));
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="space-y-3">
      <Modal
        open={isOpen}
        onClose={closeDialog} preventClose={isSubmitting}
        title={t('finalizeSection.dialogTitle', { documentType: documentTypeDisplayName })}
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
        <Notice variant="success" title={t('finalizeSection.finalizedNoticeTitle', { number: finalized.versionNumber })}>
          {t('finalizeSection.finalizedNoticeBody')}{' '}
          <Link
            to={`/educator/students/${studentId}/authored-versions/${finalized.id}`}
            className="text-brand-teal-500 hover:underline"
            data-testid="view-finalized-version"
          >
            {t('finalizeSection.viewVersion')}
          </Link>
          .{' '}
          <button
            type="button"
            onClick={() => setFinalized(null)}
            className="text-brand-slate-500 hover:underline"
            data-testid="dismiss-finalized"
          >
            {t('finalizeSection.dismiss')}
          </button>
        </Notice>
      )}

      <Button
        variant="primary"
        onClick={openDialog}
        disabled={!canFinalize}
        data-testid="finalize-button"
      >
        {t('finalizeSection.finalizeButton')}
      </Button>
      {!canFinalize && (
        <p className="text-sm text-brand-slate-500">
          {t('finalizeSection.cannotFinalizeNow', { status: documentStatusLabel(status).toLowerCase() })}
        </p>
      )}
    </div>
  );
}
