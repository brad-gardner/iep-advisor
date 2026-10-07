import { afterEach, describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import type { EvaluationCaseDto } from '../types';
// `evaluation` is a staff-only namespace (plan phase 5) — its English isn't
// bundled in `resources` (see `lib/i18n/index.ts`), only registered by this
// side-effect import, exactly as the real lazy route chunk
// (`app/lazy-routes/staff-routes.tsx`) registers it before the page that
// hosts `EvaluationCard` (the educator student detail page) can render.
import '../staff-locales';

const evaluationApi = vi.hoisted(() => ({
  getEvaluationCase: vi.fn(),
  createEvaluationCase: vi.fn(),
  requestConsent: vi.fn(),
  receiveConsent: vi.fn(),
  getConsentDownloadUrl: vi.fn(),
  overrideDueDate: vi.fn(),
  addEvaluatorAssignment: vi.fn(),
  updateEvaluatorAssignment: vi.fn(),
  removeEvaluatorAssignment: vi.fn(),
  determineEvaluation: vi.fn(),
  closeEvaluation: vi.fn(),
  createIepFromEtr: vi.fn(),
}));
vi.mock('../api/evaluation-api', () => evaluationApi);

const educatorApi = vi.hoisted(() => ({
  getEligibleTeamStaff: vi.fn().mockResolvedValue({ success: true, data: [] }),
}));
vi.mock('@/features/educator/api/educator-api', () => educatorApi);

const documentsApi = vi.hoisted(() => ({
  listAuthoredVersions: vi.fn().mockResolvedValue({ success: true, data: [] }),
}));
vi.mock('@/features/document-authoring/api/documents-api', () => documentsApi);

import { EvaluationCard } from './evaluation-card';

function baseCase(overrides: Partial<EvaluationCaseDto> = {}): EvaluationCaseDto {
  return {
    id: 1,
    studentId: 5,
    kind: 'Initial',
    status: 'Open',
    referralDate: '2026-01-01T00:00:00.000Z',
    referralSource: 'Teacher referral',
    consentRequestedAt: null,
    consentReceivedAt: null,
    hasConsentDocument: false,
    consentFileName: null,
    determinationDueDate: null,
    dueDateOverrideReason: null,
    eligibilityOutcome: null,
    determinationDate: null,
    determinationRationale: null,
    etrAuthoredVersionId: null,
    closedAt: null,
    createdByName: 'Case Manager',
    assignments: [],
    timeline: [{ at: '2026-01-01T00:00:00.000Z', label: 'Referral' }],
    obligation: null,
    ...overrides,
  };
}

function renderCard() {
  return render(
    <ToastProvider>
      <MemoryRouter initialEntries={['/educator/students/5']}>
        <Routes>
          <Route path="/educator/students/:id" element={<EvaluationCard studentId={5} />} />
          <Route path="/educator/documents/:instanceId" element={<p>Document editor</p>} />
        </Routes>
      </MemoryRouter>
    </ToastProvider>
  );
}

describe('EvaluationCard lifecycle', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    educatorApi.getEligibleTeamStaff.mockResolvedValue({ success: true, data: [] });
    documentsApi.listAuthoredVersions.mockResolvedValue({ success: true, data: [] });
  });

  afterEach(() => resetTestLanguage());

  it('start → consent → determine → create IEP navigation', async () => {
    // 1. No case yet.
    evaluationApi.getEvaluationCase.mockResolvedValueOnce({ success: true, data: null });
    renderCard();
    await screen.findByTestId('start-evaluation-form');

    // 2. Start the evaluation.
    fireEvent.change(screen.getByTestId('start-evaluation-referral-date'), { target: { value: '2026-01-01' } });
    const opened = baseCase();
    evaluationApi.createEvaluationCase.mockResolvedValue({ success: true, data: opened });
    fireEvent.click(screen.getByTestId('start-evaluation-submit'));
    await screen.findByTestId('evaluation-consent-request');
    expect(evaluationApi.createEvaluationCase).toHaveBeenCalledWith(5, {
      kind: 'Initial',
      referralDate: '2026-01-01',
      referralSource: undefined,
    });

    // 3. Request consent.
    const consentPending = baseCase({ status: 'ConsentPending', consentRequestedAt: '2026-01-05T00:00:00.000Z' });
    evaluationApi.requestConsent.mockResolvedValue({ success: true, data: consentPending });
    fireEvent.click(screen.getByTestId('evaluation-consent-request'));
    await screen.findByTestId('evaluation-consent-received-date');

    // 4. Receive consent.
    const inProgress = baseCase({
      status: 'InProgress',
      consentRequestedAt: consentPending.consentRequestedAt,
      consentReceivedAt: '2026-01-10T00:00:00.000Z',
      determinationDueDate: '2026-03-11T00:00:00.000Z',
    });
    evaluationApi.receiveConsent.mockResolvedValue({ success: true, data: inProgress });
    fireEvent.change(screen.getByTestId('evaluation-consent-received-date'), { target: { value: '2026-01-10' } });
    fireEvent.click(screen.getByTestId('evaluation-consent-receive-submit'));
    await waitFor(() => expect(evaluationApi.receiveConsent).toHaveBeenCalledWith(5, '2026-01-10', undefined));
    await screen.findByTestId('evaluation-determine-open');

    // 5. Determine — eligible.
    fireEvent.click(screen.getByTestId('evaluation-determine-open'));
    const determined = baseCase({
      status: 'Determined',
      consentRequestedAt: consentPending.consentRequestedAt,
      consentReceivedAt: inProgress.consentReceivedAt,
      determinationDueDate: inProgress.determinationDueDate,
      eligibilityOutcome: 'Eligible',
      determinationDate: '2026-02-01T00:00:00.000Z',
      determinationRationale: 'Meets criteria under IDEA.',
    });
    evaluationApi.determineEvaluation.mockResolvedValue({ success: true, data: determined });
    fireEvent.change(screen.getByTestId('determine-dialog-date'), { target: { value: '2026-02-01' } });
    fireEvent.change(screen.getByTestId('determine-dialog-rationale'), { target: { value: 'Meets criteria under IDEA.' } });
    fireEvent.click(screen.getByTestId('determine-dialog-submit'));
    await screen.findByTestId('evaluation-create-iep');
    expect(evaluationApi.determineEvaluation).toHaveBeenCalledWith(5, {
      outcome: 'Eligible',
      determinationDate: '2026-02-01',
      rationale: 'Meets criteria under IDEA.',
      etrAuthoredVersionId: undefined,
    });

    // 6. Create IEP from ETR → navigates to the new draft.
    evaluationApi.createIepFromEtr.mockResolvedValue({ success: true, data: { instanceId: 42 } });
    fireEvent.click(screen.getByTestId('evaluation-create-iep'));
    await screen.findByText('Document editor');
    expect(evaluationApi.createIepFromEtr).toHaveBeenCalledWith(5);
  });

  it('renders a stored markdown determination rationale as formatted HTML', async () => {
    evaluationApi.getEvaluationCase.mockResolvedValue({
      success: true,
      data: baseCase({
        status: 'Determined',
        eligibilityOutcome: 'Eligible',
        determinationDate: '2026-02-01T00:00:00.000Z',
        determinationRationale: 'Meets criteria **under IDEA**.',
      }),
    });

    renderCard();

    await screen.findByTestId('evaluation-determination-summary');
    const strong = screen.getByText('under IDEA');
    expect(strong.tagName).toBe('STRONG');
  });

  // `renderInSpanish` needs the staff namespace named explicitly via `ns`
  // (it's not in `featureNamespaces`, which only lists the eager/parent
  // namespaces) — see `docs/i18n/README.md`'s "Staff and admin namespaces".
  it('renders the heading and status/kind/outcome labels in Spanish', async () => {
    evaluationApi.getEvaluationCase.mockResolvedValue({
      success: true,
      data: baseCase({ status: 'Determined', eligibilityOutcome: 'Eligible', determinationDate: '2026-02-01T00:00:00.000Z' }),
    });

    await renderInSpanish(
      <ToastProvider>
        <MemoryRouter initialEntries={['/educator/students/5']}>
          <Routes>
            <Route path="/educator/students/:id" element={<EvaluationCard studentId={5} />} />
          </Routes>
        </MemoryRouter>
      </ToastProvider>,
      { ns: 'evaluation' }
    );

    expect(await screen.findByText('Evaluación')).toBeInTheDocument();
    expect(screen.getByText('Determinado')).toBeInTheDocument();
    expect(screen.getByText('Evaluación inicial')).toBeInTheDocument();
    expect(screen.getByTestId('evaluation-determination-summary')).toHaveTextContent('Elegible');
  });
});
