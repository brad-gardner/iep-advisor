import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Sparkles } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { GeneratedLanguageNotice } from '@/lib/i18n/generated-language-notice';
import type { InterviewSuggestionDto, StudentWorkspaceEntryKind } from '../types';

interface AiInterviewHelperProps {
  // Returns the AI suggestion (with its generated language), or null on
  // failure. NOT persisted.
  onInterview: (prompt: string) => Promise<InterviewSuggestionDto | null>;
  // Saves the suggestion as an entry (private by default). Returns success.
  onSave: (
    content: string,
    entryKind: StudentWorkspaceEntryKind
  ) => Promise<boolean>;
}

type Phase = 'idle' | 'loading' | 'suggested' | 'error';

// Prompt → AI suggestion → the student chooses to save it as an entry or
// dismiss it. The suggestion is never auto-saved.
export function AiInterviewHelper({ onInterview, onSave }: AiInterviewHelperProps) {
  const { t } = useTranslation('student');
  const [prompt, setPrompt] = useState('');
  const [phase, setPhase] = useState<Phase>('idle');
  const [suggestion, setSuggestion] = useState<InterviewSuggestionDto | null>(null);
  const [saving, setSaving] = useState(false);

  const trimmed = prompt.trim();

  // Click-triggered (never a mount effect), so no `t`-dependency concern —
  // see `AcknowledgeControl` (shared-drafts) for the same reasoning.
  const handleAsk = async () => {
    if (!trimmed || phase === 'loading') return;
    setPhase('loading');
    const result = await onInterview(trimmed);
    if (result) {
      setSuggestion(result);
      setPhase('suggested');
    } else {
      setPhase('error');
    }
  };

  const handleSave = async (entryKind: StudentWorkspaceEntryKind) => {
    if (saving || !suggestion) return;
    setSaving(true);
    try {
      const ok = await onSave(suggestion.suggestion, entryKind);
      if (ok) {
        setSuggestion(null);
        setPrompt('');
        setPhase('idle');
      }
    } finally {
      setSaving(false);
    }
  };

  const handleDismiss = () => {
    setSuggestion(null);
    setPhase('idle');
  };

  return (
    <Card className="space-y-3" data-testid="ai-interview-helper">
      <div className="flex items-start gap-2">
        <Sparkles
          className="mt-0.5 h-5 w-5 shrink-0 text-brand-teal-500"
          strokeWidth={1.8}
          aria-hidden="true"
        />
        <div>
          <h2 className="font-serif text-lg">{t('aiInterview.heading')}</h2>
          <p className="text-sm text-brand-slate-500">{t('aiInterview.description')}</p>
        </div>
      </div>

      <Textarea
        rows={3}
        value={prompt}
        onChange={(e) => setPrompt(e.target.value)}
        placeholder={t('aiInterview.promptPlaceholder')}
        aria-label={t('aiInterview.promptAriaLabel')}
        data-testid="ai-interview-prompt"
      />
      <Button
        onClick={() => void handleAsk()}
        disabled={!trimmed}
        loading={phase === 'loading'}
        data-testid="ai-interview-ask"
      >
        {t('aiInterview.askButton')}
      </Button>

      {phase === 'error' && (
        <div data-testid="ai-interview-error">
          <Notice variant="error" title={t('aiInterview.errorTitle')}>
            {t('aiInterview.errorBody')}
          </Notice>
        </div>
      )}

      {phase === 'suggested' && suggestion && (
        <div
          className="space-y-3 rounded-card border border-brand-teal-100 bg-brand-teal-50 p-4"
          data-testid="ai-interview-suggestion"
        >
          <GeneratedLanguageNotice generatedLanguage={suggestion.generatedLanguage} />
          <p className="whitespace-pre-wrap text-sm text-brand-slate-800">
            {suggestion.suggestion}
          </p>
          <div className="flex flex-wrap items-center gap-2">
            <Button
              onClick={() => void handleSave('MeetingStatement')}
              loading={saving}
              data-testid="ai-interview-save-statement"
            >
              {t('aiInterview.saveAsStatement')}
            </Button>
            <Button
              variant="secondary"
              onClick={() => void handleSave('AiInterviewAnswer')}
              disabled={saving}
              data-testid="ai-interview-save-answer"
            >
              {t('aiInterview.saveAsAnswer')}
            </Button>
            <Button
              variant="ghost"
              onClick={handleDismiss}
              disabled={saving}
              data-testid="ai-interview-dismiss"
            >
              {t('aiInterview.dismiss')}
            </Button>
          </div>
          <p className="text-xs text-brand-slate-500">{t('aiInterview.privacyNote')}</p>
        </div>
      )}
    </Card>
  );
}
