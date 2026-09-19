import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Outlet, Route, Routes, useLocation } from 'react-router-dom';
import type { ChildProfile } from '@/types/api';

const meetingPrepApi = vi.hoisted(() => ({ getChecklistsByChild: vi.fn(), generateFromGoals: vi.fn() }));
vi.mock('../api/meeting-prep-api', () => meetingPrepApi);
const shareableEntriesApi = vi.hoisted(() => ({ getChildShareableEntries: vi.fn() }));
vi.mock('@/features/student/api/shareable-entries-api', () => shareableEntriesApi);
const toast = vi.hoisted(() => ({ show: vi.fn() }));
vi.mock('@/components/ui/toast', () => ({ useToast: () => toast }));
const auth = vi.hoisted(() => ({ user: { id: 9 } as { id: number } | null }));
vi.mock('@/features/auth/hooks/use-auth', () => ({ useAuth: () => ({ user: auth.user }) }));

import { ChildMeetingPrepTab, QUESTION_ADDED_TOAST } from './child-meeting-prep-tab';

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

function LocationProbe() {
  const location = useLocation();
  return <output data-testid="location">{location.pathname + location.search}</output>;
}

function renderTab(url: string, role: ChildProfile['role'] = 'owner') {
  const ctx = { child: child(role), childId: 4, reloadChild: () => Promise.resolve() };
  return render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/children/:childId" element={<Outlet context={ctx} />}>
          <Route
            path="meeting-prep"
            element={
              <>
                <ChildMeetingPrepTab />
                <LocationProbe />
              </>
            }
          />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

const QUESTION = 'What baseline was used for the reading goal?';

describe('ChildMeetingPrepTab — parent questions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    auth.user = { id: 9 };
    meetingPrepApi.getChecklistsByChild.mockResolvedValue({ success: true, data: [] });
    shareableEntriesApi.getChildShareableEntries.mockResolvedValue({ success: true, data: [] });
  });

  it('consumes ?addQuestion= once: adds the question, toasts, and clears the param', async () => {
    renderTab(`/children/4/meeting-prep?addQuestion=${encodeURIComponent(QUESTION)}`);
    const list = await screen.findByTestId('parent-questions-list');
    expect(within(list).getAllByTestId('parent-question')).toHaveLength(1);
    expect(list).toHaveTextContent(QUESTION);
    expect(toast.show).toHaveBeenCalledWith({ message: QUESTION_ADDED_TOAST, variant: 'success' });
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/children/4/meeting-prep'));
    expect(screen.getByTestId('location')).not.toHaveTextContent('addQuestion');
    expect(toast.show).toHaveBeenCalledTimes(1);
  });

  it('keeps the questions for this parent and child across visits and lets them be checked and removed', async () => {
    const first = renderTab(`/children/4/meeting-prep?addQuestion=${encodeURIComponent(QUESTION)}`);
    await screen.findByTestId('parent-questions-list');
    first.unmount();

    renderTab('/children/4/meeting-prep');
    const list = await screen.findByTestId('parent-questions-list');
    expect(list).toHaveTextContent(QUESTION);

    fireEvent.click(within(list).getByRole('checkbox', { name: QUESTION }));
    expect(within(list).getByRole('checkbox', { name: QUESTION })).toBeChecked();
    expect(screen.getByTestId('parent-questions')).toHaveTextContent('1 of 1 asked');

    fireEvent.click(within(list).getByRole('button', { name: `Remove question: ${QUESTION}` }));
    expect(screen.getByTestId('parent-questions-empty')).toBeInTheDocument();
    expect(localStorage.getItem('iep-advisor:meeting-prep:my-questions:9:4')).toBeNull();
  });

  it('adds a typed question and refuses an exact repeat', async () => {
    renderTab('/children/4/meeting-prep');
    const form = await screen.findByTestId('parent-questions-form');
    const input = within(form).getByLabelText('Add a question');
    fireEvent.change(input, { target: { value: '  Who collects the progress data? ' } });
    fireEvent.submit(form);
    expect(screen.getByTestId('parent-questions-list')).toHaveTextContent('Who collects the progress data?');
    expect(input).toHaveValue('');

    fireEvent.change(input, { target: { value: 'who collects the progress data?' } });
    fireEvent.submit(form);
    expect(screen.getByRole('alert')).toHaveTextContent('That question is already on your list.');
    expect(screen.getAllByTestId('parent-question')).toHaveLength(1);
  });

  it('only cleans the URL for a viewer — nothing is added for them', async () => {
    renderTab(`/children/4/meeting-prep?addQuestion=${encodeURIComponent(QUESTION)}`, 'viewer');
    await waitFor(() => expect(screen.getByTestId('location')).not.toHaveTextContent('addQuestion'));
    expect(screen.getByTestId('parent-questions-empty')).toBeInTheDocument();
    expect(screen.queryByTestId('parent-questions-form')).not.toBeInTheDocument();
    expect(toast.show).not.toHaveBeenCalled();
  });
});
