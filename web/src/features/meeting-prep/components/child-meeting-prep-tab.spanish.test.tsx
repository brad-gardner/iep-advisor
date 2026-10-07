import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom';
import type { ChildProfile } from '@/types/api';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

const meetingPrepApi = vi.hoisted(() => ({ getChecklistsByChild: vi.fn(), generateFromGoals: vi.fn() }));
vi.mock('../api/meeting-prep-api', () => meetingPrepApi);
const shareableEntriesApi = vi.hoisted(() => ({ getChildShareableEntries: vi.fn() }));
vi.mock('@/features/student/api/shareable-entries-api', () => shareableEntriesApi);
const prepQuestionsApi = vi.hoisted(() => ({
  listPrepQuestions: vi.fn(),
  createPrepQuestion: vi.fn(),
  updatePrepQuestion: vi.fn(),
  reorderPrepQuestions: vi.fn(),
  deletePrepQuestion: vi.fn(),
}));
vi.mock('../api/prep-questions-api', () => prepQuestionsApi);
vi.mock('@/components/ui/toast', () => ({ useToast: () => ({ show: vi.fn() }) }));

import { ChildMeetingPrepTab } from './child-meeting-prep-tab';

const child = (role: ChildProfile['role']): ChildProfile => ({
  id: 4,
  firstName: 'Jordan',
  lastName: 'Lee',
  dateOfBirth: null,
  gradeLevel: null,
  disabilityCategory: null,
  schoolDistrict: null,
  role,
  currentIepDocumentId: null,
  createdAt: '2026-01-01',
  updatedAt: '2026-01-01',
});

function renderTab() {
  const ctx = { child: child('owner'), childId: 4, reloadChild: () => Promise.resolve() };
  return renderInSpanish(
    <MemoryRouter initialEntries={['/children/4/meeting-prep']}>
      <Routes>
        <Route path="/children/:childId" element={<Outlet context={ctx} />}>
          <Route path="meeting-prep" element={<ChildMeetingPrepTab />} />
        </Route>
      </Routes>
    </MemoryRouter>
  );
}

describe('ChildMeetingPrepTab in Spanish', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    meetingPrepApi.getChecklistsByChild.mockResolvedValue({ success: true, data: [] });
    shareableEntriesApi.getChildShareableEntries.mockResolvedValue({ success: true, data: [] });
    prepQuestionsApi.listPrepQuestions.mockResolvedValue({ success: true, data: [] });
  });
  afterEach(() => resetTestLanguage());

  it('renders the date control, parent questions card and empty checklist state in Spanish', async () => {
    await renderTab();

    expect(await screen.findByLabelText('Fecha de la reunión (opcional)')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Generar' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Sus preguntas' })).toBeInTheDocument();
    expect(screen.getByText('Aún no hay nada. Agregue una pregunta abajo o acepte una que sugiera el asesor virtual.')).toBeInTheDocument();
    expect(await screen.findByText('Prepárese para su reunión')).toBeInTheDocument();
  });
});
