import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
// `document-authoring` is a staff-only namespace (plan phase 5) — see
// `@/app/lazy-routes/staff-locales`'s doc comment and `docs/i18n/README.md`'s "Staff and
// admin namespaces". This component renders directly here (not through the
// lazy route), so its English must be registered the same way the real
// route chunk does.
import '@/app/lazy-routes/staff-locales';
import { AssistSuggestionPanel } from './assist-suggestion-panel';

describe('AssistSuggestionPanel', () => {
  it('shows the rationale and cited sources, and Accept applies', () => {
    const onAccept = vi.fn();
    render(
      <AssistSuggestionPanel
        suggestion="By May, Jordan will read 70 wpm."
        rationale="Anchored to the ETR baseline."
        citations={[{ evidenceId: 'E2', sourceLabel: 'ETR v1 — Team summary', excerpt: 'CBM reading 38 wpm' }]}
        onAccept={onAccept}
        onDismiss={() => {}}
        testIdPrefix="t"
      />
    );
    expect(screen.getByTestId('t-rationale')).toHaveTextContent('Anchored to the ETR baseline.');
    expect(screen.getByTestId('t-citations')).toHaveTextContent('E2');
    expect(screen.getByTestId('t-citations')).toHaveTextContent('ETR v1 — Team summary — CBM reading 38 wpm');
    expect(screen.queryByTestId('t-missing-baseline')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('t-accept'));
    expect(onAccept).toHaveBeenCalled();
  });

  it('labels an ungrounded suggestion and surfaces the missing-baseline warning', () => {
    render(<AssistSuggestionPanel suggestion="Try harder." missingBaseline onDismiss={() => {}} testIdPrefix="t" />);
    expect(screen.getByTestId('t-citations')).toHaveTextContent('Not grounded in evidence on record.');
    expect(screen.getByTestId('t-missing-baseline')).toHaveTextContent(/No baseline on record/);
    expect(screen.queryByTestId('t-accept')).not.toBeInTheDocument();
  });
});
