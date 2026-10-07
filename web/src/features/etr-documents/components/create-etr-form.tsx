import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Input, Select } from '@/components/ui/input';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { create as createEtr } from '../api/etr-documents-api';
import { evaluationTypeLabel, documentStateLabel } from '../lib/document-labels';
import type { DocumentState, EvaluationType } from '../types';

const EVALUATION_TYPE_VALUES: EvaluationType[] = ['initial', 'reevaluation', 'transfer', 'other'];
const DOCUMENT_STATE_VALUES: DocumentState[] = ['draft', 'final'];

interface CreateEtrFormProps {
  childId: number;
  onCreated: () => void;
  onCancel: () => void;
}

const NOTES_MAX_LENGTH = 2000;

export function CreateEtrForm({ childId, onCreated, onCancel }: CreateEtrFormProps) {
  const { t } = useTranslation(['etr-documents', 'common']);
  const [evaluationDate, setEvaluationDate] = useState('');
  const [evaluationType, setEvaluationType] = useState<EvaluationType | ''>('');
  const [documentState, setDocumentState] = useState<DocumentState>('draft');
  const [notes, setNotes] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!evaluationDate || !evaluationType) return;
    if (isMarkdownOverLimit(notes, NOTES_MAX_LENGTH)) return;

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
        setError(response.message || t('createForm.createFailed'));
      }
    } catch {
      setError(t('createForm.createError'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      {error && <Notice variant="error" title={error} />}

      <p className="text-[12px] text-brand-slate-500">
        {t('createForm.description')}
      </p>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Input
          label={t('createForm.evaluationDate')}
          type="date"
          value={evaluationDate}
          onChange={(e) => setEvaluationDate(e.target.value)}
          required
          data-testid="etr-evaluation-date"
        />
        <Select
          label={t('createForm.evaluationType')}
          value={evaluationType}
          onChange={(e) => setEvaluationType(e.target.value as EvaluationType)}
          required
          data-testid="etr-evaluation-type"
        >
          <option value="">{t('createForm.selectType')}</option>
          {EVALUATION_TYPE_VALUES.map((value) => (
            <option key={value} value={value}>
              {evaluationTypeLabel(value)}
            </option>
          ))}
        </Select>
      </div>

      <div>
        <label className="block text-[13px] font-medium text-brand-slate-600 mb-1">
          {t('createForm.documentState')}
        </label>
        <div className="flex gap-2" role="radiogroup" aria-label={t('createForm.documentState')}>
          {DOCUMENT_STATE_VALUES.map((value) => {
            const isActive = documentState === value;
            return (
              <button
                key={value}
                type="button"
                role="radio"
                aria-checked={isActive}
                onClick={() => setDocumentState(value)}
                data-testid={`etr-document-state-${value}`}
                className={`px-3 py-1.5 text-[13px] font-medium rounded-button border transition-colors ${
                  isActive
                    ? 'bg-brand-teal-50 text-brand-teal-600 border-brand-teal-200'
                    : 'bg-white text-brand-slate-600 border-brand-slate-200 hover:border-brand-teal-200'
                }`}
              >
                {documentStateLabel(value)}
              </button>
            );
          })}
        </div>
      </div>

      <RichTextEditor
        label={t('createForm.notes')}
        placeholder={t('createForm.notesPlaceholder')}
        value={notes}
        onChange={setNotes}
        minRows={3}
        maxLength={NOTES_MAX_LENGTH}
        data-testid="etr-notes"
      />

      <div className="flex gap-2">
        <Button
          type="submit"
          loading={isSubmitting}
          disabled={!evaluationDate || !evaluationType || isMarkdownOverLimit(notes, NOTES_MAX_LENGTH)}
          data-testid="etr-create-submit"
        >
          {t('createForm.create')}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel} data-testid="etr-create-cancel">
          {t('common:ui.cancel')}
        </Button>
      </div>
    </form>
  );
}
