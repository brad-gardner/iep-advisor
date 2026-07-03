import { useState } from 'react';
import { Sparkles } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import type { StudentWorkspaceEntryKind } from '../types';

interface AiInterviewHelperProps {
  // Returns the AI suggestion text, or null on failure. NOT persisted.
  onInterview: (prompt: string) => Promise<string | null>;
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
  const [prompt, setPrompt] = useState('');
  const [phase, setPhase] = useState<Phase>('idle');
  const [suggestion, setSuggestion] = useState('');
  const [saving, setSaving] = useState(false);

  const trimmed = prompt.trim();

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
    if (saving) return;
    setSaving(true);
    try {
      const ok = await onSave(suggestion, entryKind);
      if (ok) {
        setSuggestion('');
        setPrompt('');
        setPhase('idle');
      }
    } finally {
      setSaving(false);
    }
  };

  const handleDismiss = () => {
    setSuggestion('');
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
          <h2 className="font-serif text-lg">AI Interview</h2>
          <p className="text-sm text-brand-slate-400">
            Tell the assistant what you want help saying. It will draft something
            you can save or change.
          </p>
        </div>
      </div>

      <Textarea
        rows={3}
        value={prompt}
        onChange={(e) => setPrompt(e.target.value)}
        placeholder="I want to tell my team that…"
        aria-label="What do you want help saying?"
        data-testid="ai-interview-prompt"
      />
      <Button
        onClick={() => void handleAsk()}
        disabled={!trimmed}
        loading={phase === 'loading'}
        data-testid="ai-interview-ask"
      >
        Ask the assistant
      </Button>

      {phase === 'error' && (
        <div data-testid="ai-interview-error">
          <Notice variant="error" title="The assistant could not help right now">
            Please try again in a moment.
          </Notice>
        </div>
      )}

      {phase === 'suggested' && suggestion && (
        <div
          className="space-y-3 rounded-card border-[0.5px] border-brand-teal-100 bg-brand-teal-50 p-4"
          data-testid="ai-interview-suggestion"
        >
          <p className="whitespace-pre-wrap text-sm text-brand-slate-800">
            {suggestion}
          </p>
          <div className="flex flex-wrap items-center gap-2">
            <Button
              onClick={() => void handleSave('MeetingStatement')}
              loading={saving}
              data-testid="ai-interview-save-statement"
            >
              Save as meeting statement
            </Button>
            <Button
              variant="secondary"
              onClick={() => void handleSave('AiInterviewAnswer')}
              disabled={saving}
              data-testid="ai-interview-save-answer"
            >
              Save as interview answer
            </Button>
            <Button
              variant="ghost"
              onClick={handleDismiss}
              disabled={saving}
              data-testid="ai-interview-dismiss"
            >
              Dismiss
            </Button>
          </div>
          <p className="text-xs text-brand-slate-400">
            Saved entries are private until you choose to share them.
          </p>
        </div>
      )}
    </Card>
  );
}
