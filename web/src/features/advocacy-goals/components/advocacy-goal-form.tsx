import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Select } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';
import { ADVOCACY_GOAL_CATEGORIES } from '../lib/category-label';

const GOAL_TEXT_MAX_LENGTH = 500;

interface AdvocacyGoalFormProps {
  initialValues?: { goalText: string; category: string };
  onSubmit: (data: { goalText: string; category?: string }) => Promise<{ success: boolean; error?: string }>;
  onCancel?: () => void;
  /** Defaults to the "Add Goal" translation; pass a translated label to override (e.g. "Save Changes"). */
  submitLabel?: string;
}

export function AdvocacyGoalForm({
  initialValues,
  onSubmit,
  onCancel,
  submitLabel,
}: AdvocacyGoalFormProps) {
  const { t } = useTranslation(['advocacy-goals', 'common']);
  const [goalText, setGoalText] = useState(initialValues?.goalText ?? '');
  const [category, setCategory] = useState(initialValues?.category ?? '');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    const trimmed = goalText.trim();
    if (trimmed.length < 10) {
      setError(t('advocacy-goals:form.minLengthError'));
      return;
    }
    if (isMarkdownOverLimit(goalText, GOAL_TEXT_MAX_LENGTH)) {
      setError(t('advocacy-goals:form.maxLengthError', { max: GOAL_TEXT_MAX_LENGTH }));
      return;
    }

    setIsSubmitting(true);
    try {
      const result = await onSubmit({
        goalText: trimmed,
        category: category || undefined,
      });
      if (!result.success) {
        setError(result.error || t('advocacy-goals:form.saveFailed'));
      } else {
        setGoalText('');
        setCategory('');
      }
    } catch {
      setError(t('common:ui.genericError'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3" data-testid="goal-form">
      {error && <div data-testid="goal-form-error"><Notice variant="error" title={error} /></div>}

      <RichTextEditor
        label={t('advocacy-goals:form.label')}
        id="goal-text"
        value={goalText}
        onChange={setGoalText}
        placeholder={t('advocacy-goals:form.placeholder')}
        minRows={3}
        maxLength={GOAL_TEXT_MAX_LENGTH}
        data-testid="goal-text-input"
      />

      <div className="flex gap-3 items-end">
        <div className="flex-1">
          <Select
            label={t('advocacy-goals:form.categoryLabel')}
            id="goal-category"
            value={category}
            onChange={(e) => setCategory((e.target as HTMLSelectElement).value)}
            data-testid="goal-category-select"
          >
            <option value="">{t('advocacy-goals:category.none')}</option>
            {ADVOCACY_GOAL_CATEGORIES.map((c) => (
              <option key={c} value={c}>
                {t(`advocacy-goals:category.${c}`)}
              </option>
            ))}
          </Select>
        </div>

        <div className="flex gap-2">
          {onCancel && (
            <Button variant="ghost" type="button" onClick={onCancel} data-testid="goal-form-cancel">
              {t('common:ui.cancel')}
            </Button>
          )}
          <Button
            type="submit"
            loading={isSubmitting}
            disabled={goalText.trim().length < 10 || isMarkdownOverLimit(goalText, GOAL_TEXT_MAX_LENGTH)}
            data-testid="goal-form-submit"
          >
            {submitLabel ?? t('advocacy-goals:form.addGoal')}
          </Button>
        </div>
      </div>
    </form>
  );
}
