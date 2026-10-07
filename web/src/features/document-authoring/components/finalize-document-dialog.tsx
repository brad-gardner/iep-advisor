import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';

interface FinalizeDocumentDialogProps {
  documentTypeDisplayName: string;
  // The version number this finalize will create, e.g. 1, 2, … (best-effort hint).
  nextVersionNumber?: number;
  isSubmitting: boolean;
  // A single state-conflict / generic message (409, 403, network, …).
  error: string | null;
  // Complete list of missing-required / invalid fields (422) to fix first.
  validationErrors: string[];
  onConfirm: () => void;
  onCancel: () => void;
}

// Body of the finalize confirmation Modal. Confirms the irreversible snapshot,
// and — when the server rejects with a 422 — renders the complete list of
// fields to fix. Submit state stays parent-lifted in FinalizeDocumentSection.
export function FinalizeDocumentDialog({
  documentTypeDisplayName,
  nextVersionNumber,
  isSubmitting,
  error,
  validationErrors,
  onConfirm,
  onCancel,
}: FinalizeDocumentDialogProps) {
  const { t } = useTranslation('document-authoring');
  const versionLabel = nextVersionNumber ? `v${nextVersionNumber}` : t('finalizeDialog.nextVersionFallback');
  const hasValidationErrors = validationErrors.length > 0;

  return (
    <div>
      <p className="mb-3 text-sm text-brand-slate-600">
        {t('finalizeDialog.body', { version: versionLabel, documentType: documentTypeDisplayName })}
      </p>

      {hasValidationErrors && (
        <div className="mb-3" role="alert">
          <Notice variant="error" title={t('finalizeDialog.fixBeforeFinalizing')}>
            <ul className="ml-4 list-disc space-y-1" data-testid="finalize-validation-errors">
              {validationErrors.map((msg, i) => (
                <li key={`${i}-${msg}`}>{msg}</li>
              ))}
            </ul>
          </Notice>
        </div>
      )}

      {error && !hasValidationErrors && (
        <div className="mb-3" role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      <div className="flex justify-end gap-2 pt-1">
        <Button variant="ghost" type="button" onClick={onCancel} disabled={isSubmitting}>
          {t('finalizeDialog.cancel')}
        </Button>
        <Button
          type="button"
          loading={isSubmitting}
          onClick={onConfirm}
          data-testid="finalize-confirm"
        >
          {t('finalizeDialog.confirm')}
        </Button>
      </div>
    </div>
  );
}
