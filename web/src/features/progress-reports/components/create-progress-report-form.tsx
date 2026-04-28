import { useState } from "react";
import { Input, Textarea } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Notice } from "@/components/ui/notice";
import { create } from "../api/progress-reports-api";

interface CreateProgressReportFormProps {
  iepId: number;
  onCreated: () => void;
  onCancel: () => void;
}

export function CreateProgressReportForm({
  iepId,
  onCreated,
  onCancel,
}: CreateProgressReportFormProps) {
  const [periodStart, setPeriodStart] = useState("");
  const [periodEnd, setPeriodEnd] = useState("");
  const [notes, setNotes] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError(null);

    try {
      const response = await create(iepId, {
        reportingPeriodStart: periodStart || null,
        reportingPeriodEnd: periodEnd || null,
        notes: notes.trim() || undefined,
      });
      if (response.success) {
        onCreated();
      } else {
        setError(response.message || "Failed to create progress report");
      }
    } catch {
      setError("An error occurred while creating the progress report");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form
      onSubmit={handleSubmit}
      className="space-y-4"
      data-testid="create-progress-report-form"
    >
      {error && <Notice variant="error" title={error} />}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Input
          label="Reporting Period Start"
          type="date"
          value={periodStart}
          onChange={(e) => setPeriodStart(e.target.value)}
          data-testid="pr-period-start"
        />
        <Input
          label="Reporting Period End"
          type="date"
          value={periodEnd}
          onChange={(e) => setPeriodEnd(e.target.value)}
          data-testid="pr-period-end"
        />
      </div>

      <Textarea
        label="Notes (optional)"
        value={notes}
        onChange={(e) => setNotes(e.target.value)}
        rows={3}
        placeholder="Anything worth flagging about this report..."
        data-testid="pr-notes"
      />

      <div className="flex gap-2 justify-end">
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" disabled={isSubmitting} data-testid="pr-submit">
          {isSubmitting ? "Creating..." : "Create"}
        </Button>
      </div>
    </form>
  );
}
