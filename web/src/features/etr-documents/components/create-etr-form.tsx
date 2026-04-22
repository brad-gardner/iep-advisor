import { useState } from 'react';
import { Input, Select, Textarea } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { create as createEtr } from '../api/etr-documents-api';
import type { DocumentState, EvaluationType } from '../types';

const EVALUATION_TYPES: { value: EvaluationType; label: string }[] = [
  { value: 'initial', label: 'Initial Evaluation' },
  { value: 'reevaluation', label: 'Reevaluation' },
  { value: 'transfer', label: 'Transfer' },
  { value: 'other', label: 'Other' },
];

const DOCUMENT_STATES: { value: DocumentState; label: string }[] = [
  { value: 'draft', label: 'Draft' },
  { value: 'final', label: 'Final' },
];

interface CreateEtrFormProps {
  childId: number;
  onCreated: () => void;
  onCancel: () => void;
}

export function CreateEtrForm({ childId, onCreated, onCancel }: CreateEtrFormProps) {
  const [evaluationDate, setEvaluationDate] = useState('');
  const [evaluationType, setEvaluationType] = useState<EvaluationType | ''>('');
  const [documentState, setDocumentState] = useState<DocumentState>('draft');
  const [notes, setNotes] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!evaluationDate || !evaluationType) return;

    setIsSubmitting(true);
    setError(null);

    try {
      const response = await createEtr(childId, {
        evaluationDate,
        evaluationType,
        documentState,
        notes: notes.trim() || undefined,
      });
      if (response.success) {
        onCreated();
      } else {
        setError(response.message || 'Failed to create ETR');
      }
    } catch {
      setError('An error occurred while creating the ETR');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      {error && <Notice variant="error" title={error} />}

      <p className="text-[12px] text-brand-slate-400">
        An ETR (Evaluation Team Report) documents the team&rsquo;s assessment findings
        and eligibility determination for special education.
      </p>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Input
          label="Evaluation Date"
          type="date"
          value={evaluationDate}
          onChange={(e) => setEvaluationDate(e.target.value)}
          required
          data-testid="etr-evaluation-date"
        />
        <Select
          label="Evaluation Type"
          value={evaluationType}
          onChange={(e) => setEvaluationType(e.target.value as EvaluationType)}
          required
          data-testid="etr-evaluation-type"
        >
          <option value="">Select type...</option>
          {EVALUATION_TYPES.map((t) => (
            <option key={t.value} value={t.value}>
              {t.label}
            </option>
          ))}
        </Select>
      </div>

      <div>
        <label className="block text-[13px] font-medium text-brand-slate-600 mb-1">
          Document State
        </label>
        <div className="flex gap-2" role="radiogroup" aria-label="Document state">
          {DOCUMENT_STATES.map((s) => {
            const isActive = documentState === s.value;
            return (
              <button
                key={s.value}
                type="button"
                role="radio"
                aria-checked={isActive}
                onClick={() => setDocumentState(s.value)}
                data-testid={`etr-document-state-${s.value}`}
                className={`px-3 py-1.5 text-[13px] font-medium rounded-button border transition-colors ${
                  isActive
                    ? 'bg-brand-teal-50 text-brand-teal-600 border-brand-teal-200'
                    : 'bg-white text-brand-slate-600 border-brand-slate-200 hover:border-brand-teal-200'
                }`}
              >
                {s.label}
              </button>
            );
          })}
        </div>
      </div>

      <div>
        <Textarea
          label="Notes"
          placeholder="Any notes about this evaluation..."
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          rows={3}
          maxLength={2000}
          data-testid="etr-notes"
        />
        <p className="text-[11px] text-brand-slate-300 mt-1">{notes.length}/2000 characters</p>
      </div>

      <div className="flex gap-2">
        <Button
          type="submit"
          disabled={isSubmitting || !evaluationDate || !evaluationType}
          data-testid="etr-create-submit"
        >
          {isSubmitting ? 'Creating...' : 'Create ETR'}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel} data-testid="etr-create-cancel">
          Cancel
        </Button>
      </div>
    </form>
  );
}
