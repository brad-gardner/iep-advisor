import { useState } from 'react';
import type { ImportPreview, ImportResult } from '../types';

export type WizardStep = 'template' | 'upload' | 'preview' | 'result';

// Ordinal positions for the progress indicator. "Commit" (index 3) is the
// confirm dialog over the preview step, so it is surfaced by the page while the
// dialog is open rather than being a step the wizard can rest on.
export const WIZARD_STEP_LABELS = ['Template', 'Upload', 'Preview', 'Commit', 'Result'];
export const WIZARD_STEP_INDEX: Record<WizardStep, number> = {
  template: 0,
  upload: 1,
  preview: 2,
  result: 4,
};

interface WizardState {
  step: WizardStep;
  preview: ImportPreview | null;
  result: ImportResult | null;
}

const INITIAL: WizardState = { step: 'template', preview: null, result: null };

// Linear state for the import wizard: template → upload → preview → result.
// Every transition runs from an event handler (never an effect).
export function useImportWizard() {
  const [state, setState] = useState<WizardState>(INITIAL);

  return {
    ...state,
    goToUpload: () => setState((prev) => ({ ...prev, step: 'upload' })),
    showPreview: (preview: ImportPreview) => setState({ step: 'preview', preview, result: null }),
    showResult: (result: ImportResult) => setState((prev) => ({ ...prev, step: 'result', result })),
    reset: () => setState(INITIAL),
  };
}
