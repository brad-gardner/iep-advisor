import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { AxiosError } from 'axios';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { finalizeDraft, listVersionsForStudent } from '../api/iep-versions-api';
import type { IepVersionSummaryDto } from '../types';
import { FinalizeDialog } from './finalize-dialog';

interface FinalizeSectionProps {
  draftId: number;
  studentId: number;
  // Flush in-flight autosaves so the version captures the latest edits.
  flushBeforeFinalize: () => Promise<void>;
}

function mapFinalizeError(status: number | undefined, message?: string): string {
  if (status === 403) return "You don't have permission to finalize this IEP.";
  if (status === 400) return message || 'This draft cannot be finalized.';
  return message || 'Could not finalize the IEP. Please try again.';
}

// Owns the educator finalize flow: open the confirm dialog, flush pending saves,
// POST finalize, then surface a success Notice linking to the new version.
export function FinalizeSection({ draftId, studentId, flushBeforeFinalize }: FinalizeSectionProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [finalized, setFinalized] = useState<IepVersionSummaryDto | null>(null);
  const [nextVersionNumber, setNextVersionNumber] = useState<number | undefined>(undefined);

  // Derive the next version number from existing versions (best-effort hint only).
  useEffect(() => {
    let active = true;
    listVersionsForStudent(studentId)
      .then((res) => {
        if (!active || !res.success || !res.data) return;
        const max = res.data.reduce((m, v) => Math.max(m, v.versionNumber), 0);
        setNextVersionNumber(max + 1);
      })
      .catch(() => {
        // Hint only; the dialog falls back to generic copy.
      });
    return () => {
      active = false;
    };
  }, [studentId]);

  const handleConfirm = async (effectiveDate: string | null) => {
    setIsSubmitting(true);
    setError(null);
    try {
      // Capture the latest edits before snapshotting.
      await flushBeforeFinalize();
      const res = await finalizeDraft(draftId, { effectiveDate });
      if (res.success && res.data) {
        setFinalized(res.data);
        setIsOpen(false);
        // The draft stays editable — re-finalize creates the next version.
        setNextVersionNumber(res.data.versionNumber + 1);
      } else {
        setError(res.message || 'Could not finalize the IEP.');
      }
    } catch (err) {
      const status = err instanceof AxiosError ? err.response?.status : undefined;
      const message =
        err instanceof AxiosError
          ? (err.response?.data as { message?: string } | undefined)?.message
          : undefined;
      setError(mapFinalizeError(status, message));
    } finally {
      setIsSubmitting(false);
    }
  };

  if (isOpen) {
    return (
      <FinalizeDialog
        nextVersionNumber={nextVersionNumber}
        isSubmitting={isSubmitting}
        error={error}
        onConfirm={handleConfirm}
        onCancel={() => {
          setIsOpen(false);
          setError(null);
        }}
      />
    );
  }

  // The success Notice coexists with the Finalize button (the draft stays editable,
  // so the educator can finalize again to create the next version).
  return (
    <div className="space-y-3">
      {finalized && (
        <Notice variant="success" title={`Finalized v${finalized.versionNumber}`}>
          An immutable version was created. The PDF is generating.{' '}
          <Link
            to={`/educator/students/${studentId}/iep-versions/${finalized.id}`}
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
      <Button variant="primary" onClick={() => setIsOpen(true)} data-testid="finalize-button">
        Finalize
      </Button>
    </div>
  );
}
