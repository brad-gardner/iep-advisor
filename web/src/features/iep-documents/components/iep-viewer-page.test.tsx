import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { GoalAnalysis, IepAnalysis, IepDocument, SmartCriterion } from '@/types/api';

const api = vi.hoisted(() => ({
  getIepDocument: vi.fn(),
  getIepSections: vi.fn(),
  getIepDocuments: vi.fn(),
  getDownloadUrl: vi.fn(),
  reprocessIep: vi.fn(),
  getAnalysis: vi.fn(),
  triggerAnalysis: vi.fn(),
}));
vi.mock('../api/iep-documents-api', () => api);
const childrenApi = vi.hoisted(() => ({ getChild: vi.fn(), setCurrentIep: vi.fn() }));
vi.mock('@/features/children/api/children-api', () => childrenApi);
vi.mock('@/features/advocacy-goals/hooks/use-advocacy-goals', () => ({
  useAdvocacyGoals: () => ({ goals: [], isLoading: false, reload: vi.fn() }),
}));
vi.mock('@/components/ui/pdf-viewer', () => ({ PdfViewer: () => <div data-testid="pdf-viewer" /> }));
vi.mock('@/features/progress-reports/components/progress-reports-tab', () => ({ ProgressReportsTab: () => null }));

import { IepViewerPage } from './iep-viewer-page';

const iep: IepDocument = {
  id: 12,
  childProfileId: 4,
  fileName: 'spring-iep.pdf',
  uploadDate: '2026-03-04T00:00:00Z',
  iepDate: '2026-03-03',
  status: 'parsed',
  fileSizeBytes: 1,
  meetingType: 'annual_review',
  attendees: null,
  notes: null,
  createdAt: '2026-03-04T00:00:00Z',
};

const ok: SmartCriterion = { rating: 'green', explanation: 'fine' };
const goal = (goalId: number, domain: string): GoalAnalysis => ({
  goalId,
  goalText: `${domain} goal text`,
  domain,
  smartAnalysis: { specific: ok, measurable: ok, achievable: ok, relevant: ok, timeBound: ok },
  overallRating: 'green',
  plainLanguageSummary: 'Looks fine.',
  strengths: [],
  concerns: [],
  suggestedImprovements: [],
});

const analysis: IepAnalysis = {
  id: 1,
  iepDocumentId: 12,
  status: 'completed',
  overallSummary: 'Summary',
  sectionAnalyses: [],
  goalAnalyses: [goal(340, 'Reading'), goal(341, 'Math')],
  overallRedFlags: [],
  advocacyGapAnalysis: null,
  parentGoalsSnapshot: null,
  errorMessage: null,
  createdAt: '2026-03-05T00:00:00Z',
};

function renderPage(url: string) {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/children/:childId/ieps/:id" element={<IepViewerPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('IepViewerPage — advocate launchers and goal deep links', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getIepDocument.mockResolvedValue({ success: true, data: iep });
    api.getIepSections.mockResolvedValue({ success: true, data: [{ id: 1, sectionType: 'annual_goals', rawText: null, parsedContent: null, displayOrder: 0, goals: [] }] });
    api.getIepDocuments.mockResolvedValue({ success: true, data: [iep] });
    api.getAnalysis.mockResolvedValue({ success: true, data: analysis });
    childrenApi.getChild.mockResolvedValue({ success: true, data: { id: 4, role: 'owner', currentIepDocumentId: 12 } });
  });

  it('offers "Ask the advocate" about the IEP from the header', async () => {
    renderPage('/children/4/ieps/12');
    const launcher = await screen.findByTestId('iep-ask-advocate');
    expect(launcher).toHaveAttribute('href', '/children/4/advocate?about=iep%3A12');
    expect(launcher).toHaveTextContent('Ask the advocate');
    expect(screen.getByTestId('pdf-viewer')).toBeInTheDocument();
  });

  it('opens the goal list and scrolls to the goal named in #goal-…, each goal carrying its own launcher', async () => {
    const scrollIntoView = vi.fn();
    Element.prototype.scrollIntoView = scrollIntoView;
    renderPage('/children/4/ieps/12#goal-341');
    const card = await screen.findByTestId('analysis-goal-341');
    expect(card).toHaveAttribute('id', 'goal-341');
    expect(screen.queryByTestId('pdf-viewer')).not.toBeInTheDocument();
    await waitFor(() => expect(scrollIntoView).toHaveBeenCalled());
    expect(scrollIntoView.mock.instances[0]).toBe(card);
    const launcher = within(card).getByRole('link', { name: 'Ask the advocate about this Math goal' });
    expect(launcher).toHaveAttribute('href', '/children/4/advocate?about=goal%3A341');
  });

  it('hides the launchers from a viewer', async () => {
    childrenApi.getChild.mockResolvedValue({ success: true, data: { id: 4, role: 'viewer', currentIepDocumentId: 12 } });
    renderPage('/children/4/ieps/12#goal-340');
    await screen.findByTestId('analysis-goal-340');
    await waitFor(() => expect(screen.queryByTestId('iep-ask-advocate')).not.toBeInTheDocument());
    expect(screen.queryByRole('link', { name: /Ask the advocate about/ })).not.toBeInTheDocument();
  });
});
