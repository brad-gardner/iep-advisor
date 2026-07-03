import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Notice } from "@/components/ui/notice";

interface FinalizeDialogProps {
  // The version number this finalize will create, e.g. 1, 2, … for the copy.
  nextVersionNumber?: number;
  isSubmitting: boolean;
  error: string | null;
  onConfirm: (effectiveDate: string | null) => void;
  onCancel: () => void;
}

// Inline confirmation panel for finalizing a draft into an immutable version.
// Captures an optional effective date.
export function FinalizeDialog({
  nextVersionNumber,
  isSubmitting,
  error,
  onConfirm,
  onCancel,
}: FinalizeDialogProps) {
  const [effectiveDate, setEffectiveDate] = useState("");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onConfirm(effectiveDate.trim() ? effectiveDate : null);
  };

  const versionLabel = nextVersionNumber
    ? `v${nextVersionNumber}`
    : "a new version";

  // Rendered as the body of a Modal (which supplies the title/panel). State
  // (isSubmitting/error) stays parent-lifted in FinalizeSection.
  return (
    <div>
      <p className="text-sm text-brand-slate-600 mb-3">
        This creates an immutable version ({versionLabel}) of the IEP that
        parents can view. The draft stays editable.
      </p>

      {error && (
        <div className="mb-3" role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-3">
        <Input
          label="Effective date (optional)"
          type="date"
          value={effectiveDate}
          onChange={(e) => setEffectiveDate(e.target.value)}
          data-testid="finalize-effective-date"
        />

        <div className="flex justify-end gap-2 pt-1">
          <Button
            variant="ghost"
            type="button"
            onClick={onCancel}
            disabled={isSubmitting}
          >
            Cancel
          </Button>
          <Button
            type="submit"
            loading={isSubmitting}
            data-testid="finalize-confirm"
          >
            Finalize
          </Button>
        </div>
      </form>
    </div>
  );
}
