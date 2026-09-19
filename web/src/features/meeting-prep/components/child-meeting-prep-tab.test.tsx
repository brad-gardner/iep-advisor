import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Outlet, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import { AxiosError, AxiosHeaders } from 'axios';
import type { ChildProfile } from '@/types/api';
import type { ParentPrepQuestionDto } from '../api/prep-questions-api';

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
const toast = vi.hoisted(() => ({ show: vi.fn() }));
vi.mock('@/components/ui/toast', () => ({ useToast: () => toast }));

import { ChildMeetingPrepTab, QUESTION_ADDED_TOAST, QUESTION_EXISTS_TOAST } from './child-meeting-prep-tab';
import { QUESTIONS_FORBIDDEN_MESSAGE } from '../hooks/use-parent-questions';

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

const question = (id: number, text: string, extra: Partial<ParentPrepQuestionDto> = {}): ParentPrepQuestionDto => ({
  id,
  childProfileId: 4,
  text,
  isChecked: false,
  displayOrder: id,
  source: 'parent',
  createdAt: '2026-09-01T00:00:00Z',
  updatedAt: '2026-09-01T00:00:00Z',
  ...extra,
});

const forbidden = () =>
  new AxiosError('Forbidden', '403', undefined, undefined, {
    status: 403,
    statusText: 'Forbidden',
    data: { success: false },
    headers: {},
    config: { headers: new AxiosHeaders() },
  });

function LocationProbe() {
  const location = useLocation();
  return <output data-testid="location">{location.pathname + location.search}</output>;
}

/** Stands in for a second advocate hand-off landing while the tab stays mounted. */
function Relauncher({ to }: { to: string }) {
  const navigate = useNavigate();
  return (
    <button type="button" onClick={() => navigate(to)} data-testid="relaunch">
      relaunch
    </button>
  );
}

function renderTab(url: string, role: ChildProfile['role'] = 'owner', relaunchTo?: string) {
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
                {relaunchTo && <Relauncher to={relaunchTo} />}
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
    meetingPrepApi.getChecklistsByChild.mockResolvedValue({ success: true, data: [] });
    shareableEntriesApi.getChildShareableEntries.mockResolvedValue({ success: true, data: [] });
    prepQuestionsApi.listPrepQuestions.mockResolvedValue({ success: true, data: [] });
    prepQuestionsApi.createPrepQuestion.mockImplementation((_childId: number, body: { text: string; source?: string }) =>
      Promise.resolve({ success: true, data: question(31, body.text, { source: body.source === 'advocate' ? 'advocate' : 'parent' }) }),
    );
    prepQuestionsApi.updatePrepQuestion.mockImplementation((id: number, body: { text?: string; isChecked?: boolean }) =>
      Promise.resolve({ success: true, data: question(id, body.text ?? 'updated', { isChecked: body.isChecked ?? false }) }),
    );
    prepQuestionsApi.reorderPrepQuestions.mockResolvedValue({ success: true });
    prepQuestionsApi.deletePrepQuestion.mockResolvedValue({ success: true });
  });

  it('consumes ?addQuestion= once: posts it as an advocate question, toasts, and clears the param', async () => {
    renderTab(`/children/4/meeting-prep?addQuestion=${encodeURIComponent(QUESTION)}`);
    const list = await screen.findByTestId('parent-questions-list');
    expect(within(list).getAllByTestId('parent-question')).toHaveLength(1);
    expect(list).toHaveTextContent(QUESTION);
    expect(prepQuestionsApi.createPrepQuestion).toHaveBeenCalledTimes(1);
    expect(prepQuestionsApi.createPrepQuestion).toHaveBeenCalledWith(4, { text: QUESTION, source: 'advocate' });
    expect(toast.show).toHaveBeenCalledWith({ message: QUESTION_ADDED_TOAST, variant: 'success' });
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/children/4/meeting-prep'));
    expect(screen.getByTestId('location')).not.toHaveTextContent('addQuestion');
    expect(toast.show).toHaveBeenCalledTimes(1);
  });

  it('says so when the server already had the handed-off question, and shows the existing one', async () => {
    prepQuestionsApi.createPrepQuestion.mockResolvedValue({
      success: true,
      data: question(7, QUESTION, { alreadyExisted: true }),
    });
    renderTab(`/children/4/meeting-prep?addQuestion=${encodeURIComponent(QUESTION)}`);
    await waitFor(() => expect(toast.show).toHaveBeenCalledWith({ message: QUESTION_EXISTS_TOAST, variant: 'info' }));
    expect(within(screen.getByTestId('parent-questions-list')).getAllByTestId('parent-question')).toHaveLength(1);
    expect(toast.show).toHaveBeenCalledTimes(1);
  });

  it('does not round-trip a handed-off question already on the loaded list', async () => {
    prepQuestionsApi.listPrepQuestions.mockResolvedValue({ success: true, data: [question(7, QUESTION.toUpperCase())] });
    renderTab(`/children/4/meeting-prep?addQuestion=${encodeURIComponent(QUESTION)}`);
    await waitFor(() => expect(toast.show).toHaveBeenCalledWith({ message: QUESTION_EXISTS_TOAST, variant: 'info' }));
    expect(prepQuestionsApi.createPrepQuestion).not.toHaveBeenCalled();
    await waitFor(() => expect(screen.getByTestId('location')).not.toHaveTextContent('addQuestion'));
  });

  it('accepts a second hand-off of the same question once the first has been consumed and cleared from the URL', async () => {
    const target = `/children/4/meeting-prep?${new URLSearchParams({ addQuestion: QUESTION }).toString()}`;
    renderTab(`/children/4/meeting-prep?addQuestion=${encodeURIComponent(QUESTION)}`, 'owner', target);
    await waitFor(() => expect(prepQuestionsApi.createPrepQuestion).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.getByTestId('location')).not.toHaveTextContent('addQuestion'));

    // A second, later hand-off carrying the exact same text lands while the
    // tab is still mounted. The question is already on the list, so this is
    // correctly reported as a duplicate — the point being that it is
    // reported at all, rather than the stale consumedRef silently eating it
    // (which would also leave `?addQuestion=` stuck in the URL).
    fireEvent.click(screen.getByTestId('relaunch'));
    await waitFor(() => expect(toast.show).toHaveBeenCalledWith({ message: QUESTION_EXISTS_TOAST, variant: 'info' }));
    await waitFor(() => expect(screen.getByTestId('location')).not.toHaveTextContent('addQuestion'));
    expect(prepQuestionsApi.createPrepQuestion).toHaveBeenCalledTimes(1);
  });

  it('lists what the API returns and lets a question be checked off', async () => {
    prepQuestionsApi.listPrepQuestions.mockResolvedValue({ success: true, data: [question(1, QUESTION)] });
    renderTab('/children/4/meeting-prep');
    const list = await screen.findByTestId('parent-questions-list');
    expect(list).toHaveTextContent(QUESTION);

    fireEvent.click(within(list).getByRole('checkbox', { name: QUESTION }));
    expect(within(list).getByRole('checkbox', { name: QUESTION })).toBeChecked();
    expect(screen.getByTestId('parent-questions')).toHaveTextContent('1 of 1 asked');
    await waitFor(() => expect(prepQuestionsApi.updatePrepQuestion).toHaveBeenCalledWith(1, { isChecked: true }));
  });

  it('disables the checkbox while its PUT is in flight, so a double-click cannot race two overlapping writes', async () => {
    prepQuestionsApi.listPrepQuestions.mockResolvedValue({ success: true, data: [question(1, QUESTION)] });
    let resolveUpdate: (value: { success: true; data: ParentPrepQuestionDto }) => void = () => {};
    prepQuestionsApi.updatePrepQuestion.mockImplementation(
      () =>
        new Promise<{ success: true; data: ParentPrepQuestionDto }>((resolve) => {
          resolveUpdate = resolve;
        }),
    );
    renderTab('/children/4/meeting-prep');
    const list = await screen.findByTestId('parent-questions-list');
    const checkbox = within(list).getByRole('checkbox', { name: QUESTION });

    fireEvent.click(checkbox);
    expect(checkbox).toBeChecked();
    // Advisory only — never `disabled`, which would blur a keyboard user mid-toggle.
    expect(checkbox).not.toBeDisabled();
    expect(checkbox).toHaveAttribute('aria-disabled', 'true');

    // A second click while the first PUT is still in flight must not fire another one.
    fireEvent.click(checkbox);
    expect(prepQuestionsApi.updatePrepQuestion).toHaveBeenCalledTimes(1);

    act(() => resolveUpdate({ success: true, data: question(1, QUESTION, { isChecked: true }) }));
    await waitFor(() => expect(checkbox).not.toHaveAttribute('aria-disabled'));
    expect(checkbox).toBeChecked();
  });

  it('applies the server’s isChecked from the response rather than assuming the optimistic value stuck', async () => {
    prepQuestionsApi.listPrepQuestions.mockResolvedValue({ success: true, data: [question(1, QUESTION)] });
    // The server disagrees with the optimistic value (e.g. a concurrent change elsewhere).
    prepQuestionsApi.updatePrepQuestion.mockResolvedValue({ success: true, data: question(1, QUESTION, { isChecked: false }) });
    renderTab('/children/4/meeting-prep');
    const list = await screen.findByTestId('parent-questions-list');
    const checkbox = within(list).getByRole('checkbox', { name: QUESTION });

    fireEvent.click(checkbox);
    expect(checkbox).toBeChecked();
    await waitFor(() => expect(checkbox).not.toBeChecked());
  });

  it('reverts a tick the server refused', async () => {
    prepQuestionsApi.listPrepQuestions.mockResolvedValue({ success: true, data: [question(1, QUESTION)] });
    prepQuestionsApi.updatePrepQuestion.mockRejectedValue(new Error('network'));
    renderTab('/children/4/meeting-prep');
    const list = await screen.findByTestId('parent-questions-list');
    fireEvent.click(within(list).getByRole('checkbox', { name: QUESTION }));
    await waitFor(() => expect(within(list).getByRole('checkbox', { name: QUESTION })).not.toBeChecked());
    expect(toast.show).toHaveBeenCalledWith(expect.objectContaining({ variant: 'error' }));
  });

  it('adds a typed question as the parent and refuses an exact repeat without a request', async () => {
    renderTab('/children/4/meeting-prep');
    const form = await screen.findByTestId('parent-questions-form');
    const input = within(form).getByLabelText('Add a question');
    fireEvent.change(input, { target: { value: '  Who collects the progress data? ' } });
    fireEvent.submit(form);
    await waitFor(() => expect(screen.getByTestId('parent-questions-list')).toHaveTextContent('Who collects the progress data?'));
    expect(prepQuestionsApi.createPrepQuestion).toHaveBeenCalledWith(4, { text: 'Who collects the progress data?', source: 'parent' });
    expect(input).toHaveValue('');
    expect(toast.show).toHaveBeenCalledWith({ message: QUESTION_ADDED_TOAST, variant: 'success' });

    fireEvent.change(input, { target: { value: 'who collects the progress data?' } });
    fireEvent.submit(form);
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('That question is already on your list.'));
    expect(prepQuestionsApi.createPrepQuestion).toHaveBeenCalledTimes(1);
    expect(screen.getAllByTestId('parent-question')).toHaveLength(1);
  });

  it('edits a question in place', async () => {
    prepQuestionsApi.listPrepQuestions.mockResolvedValue({ success: true, data: [question(1, QUESTION)] });
    renderTab('/children/4/meeting-prep');
    const list = await screen.findByTestId('parent-questions-list');
    fireEvent.click(within(list).getByRole('button', { name: `Edit question: ${QUESTION}` }));
    const input = within(list).getByLabelText('Edit question');
    fireEvent.change(input, { target: { value: 'What baseline was used?' } });
    fireEvent.submit(screen.getByTestId('parent-question-edit-form'));
    await waitFor(() => expect(prepQuestionsApi.updatePrepQuestion).toHaveBeenCalledWith(1, { text: 'What baseline was used?' }));
    await waitFor(() => expect(list).toHaveTextContent('What baseline was used?'));
    expect(within(list).queryByLabelText('Edit question')).not.toBeInTheDocument();
  });

  it('removes a question only after confirmation, keeping the dialog open on failure', async () => {
    prepQuestionsApi.listPrepQuestions.mockResolvedValue({ success: true, data: [question(1, QUESTION)] });
    prepQuestionsApi.deletePrepQuestion.mockRejectedValueOnce(new Error('network')).mockResolvedValueOnce({ success: true });
    renderTab('/children/4/meeting-prep');
    const list = await screen.findByTestId('parent-questions-list');

    fireEvent.click(within(list).getByRole('button', { name: `Remove question: ${QUESTION}` }));
    expect(screen.getByRole('alertdialog')).toHaveTextContent(QUESTION);
    expect(prepQuestionsApi.deletePrepQuestion).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: 'Remove question' }));
    await waitFor(() => expect(screen.getByRole('alertdialog')).toHaveTextContent('Could not remove this question.'));
    expect(list).toHaveTextContent(QUESTION);

    fireEvent.click(screen.getByRole('button', { name: 'Remove question' }));
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    expect(prepQuestionsApi.deletePrepQuestion).toHaveBeenCalledWith(1);
    expect(screen.getByTestId('parent-questions-empty')).toBeInTheDocument();
  });

  it('moves a question with the arrows and saves the whole order', async () => {
    prepQuestionsApi.listPrepQuestions.mockResolvedValue({
      success: true,
      data: [question(1, 'First'), question(2, 'Second'), question(3, 'Third')],
    });
    renderTab('/children/4/meeting-prep');
    const list = await screen.findByTestId('parent-questions-list');
    expect(within(list).getByRole('button', { name: 'Move up: First' })).toBeDisabled();
    expect(within(list).getByRole('button', { name: 'Move down: Third' })).toBeDisabled();

    fireEvent.click(within(list).getByRole('button', { name: 'Move down: First' }));
    await waitFor(() => expect(prepQuestionsApi.reorderPrepQuestions).toHaveBeenCalledWith(4, [2, 1, 3]));
    const rows = within(list).getAllByTestId('parent-question');
    expect(rows[0]).toHaveTextContent('Second');
    expect(rows[1]).toHaveTextContent('First');
    await waitFor(() => expect(within(list).getByRole('button', { name: 'Move up: First' })).toBeEnabled());
  });

  it('puts the order back when the server refuses the move', async () => {
    prepQuestionsApi.listPrepQuestions.mockResolvedValue({ success: true, data: [question(1, 'First'), question(2, 'Second')] });
    prepQuestionsApi.reorderPrepQuestions.mockRejectedValue(new Error('network'));
    renderTab('/children/4/meeting-prep');
    const list = await screen.findByTestId('parent-questions-list');
    fireEvent.click(within(list).getByRole('button', { name: 'Move up: Second' }));
    await waitFor(() => expect(toast.show).toHaveBeenCalledWith(expect.objectContaining({ variant: 'error' })));
    const rows = within(list).getAllByTestId('parent-question');
    expect(rows[0]).toHaveTextContent('First');
  });

  it('keeps a check toggled during an in-flight reorder even after that reorder is reverted', async () => {
    prepQuestionsApi.listPrepQuestions.mockResolvedValue({ success: true, data: [question(1, 'First'), question(2, 'Second')] });
    let resolveReorder: (value: { success: boolean }) => void = () => {};
    prepQuestionsApi.reorderPrepQuestions.mockImplementation(
      () => new Promise<{ success: boolean }>((resolve) => { resolveReorder = resolve; }),
    );
    prepQuestionsApi.updatePrepQuestion.mockResolvedValue({ success: true, data: question(1, 'First', { isChecked: true }) });
    renderTab('/children/4/meeting-prep');
    const list = await screen.findByTestId('parent-questions-list');

    fireEvent.click(within(list).getByRole('button', { name: 'Move down: First' }));
    // While that reorder PUT is still in flight, check off "First" (checks are not blocked during a reorder).
    fireEvent.click(within(list).getByRole('checkbox', { name: 'First' }));
    await waitFor(() => expect(prepQuestionsApi.updatePrepQuestion).toHaveBeenCalledWith(1, { isChecked: true }));

    act(() => resolveReorder({ success: false }));
    await waitFor(() => expect(toast.show).toHaveBeenCalledWith(expect.objectContaining({ variant: 'error' })));

    // The reorder is reverted (First is back on top)...
    const rows = within(list).getAllByTestId('parent-question');
    expect(rows[0]).toHaveTextContent('First');
    // ...but the check applied while it was in flight is not undone with it.
    expect(within(list).getByRole('checkbox', { name: 'First' })).toBeChecked();
  });

  it('only cleans the URL for a viewer — nothing is posted for them and no controls show', async () => {
    prepQuestionsApi.listPrepQuestions.mockResolvedValue({ success: true, data: [question(1, QUESTION)] });
    renderTab(`/children/4/meeting-prep?addQuestion=${encodeURIComponent(QUESTION)}`, 'viewer');
    await waitFor(() => expect(screen.getByTestId('location')).not.toHaveTextContent('addQuestion'));
    const list = await screen.findByTestId('parent-questions-list');
    expect(list).toHaveTextContent(QUESTION);
    expect(within(list).getByRole('checkbox', { name: QUESTION })).toBeDisabled();
    expect(within(list).queryByRole('button')).not.toBeInTheDocument();
    expect(screen.queryByTestId('parent-questions-form')).not.toBeInTheDocument();
    expect(prepQuestionsApi.createPrepQuestion).not.toHaveBeenCalled();
    expect(toast.show).not.toHaveBeenCalled();
  });

  it('hides the write controls when the API answers 403', async () => {
    prepQuestionsApi.listPrepQuestions.mockResolvedValue({ success: true, data: [question(1, QUESTION)] });
    prepQuestionsApi.updatePrepQuestion.mockRejectedValue(forbidden());
    renderTab('/children/4/meeting-prep');
    const list = await screen.findByTestId('parent-questions-list');
    fireEvent.click(within(list).getByRole('checkbox', { name: QUESTION }));
    await waitFor(() => expect(toast.show).toHaveBeenCalledWith({ message: QUESTIONS_FORBIDDEN_MESSAGE, variant: 'error' }));
    await waitFor(() => expect(screen.queryByTestId('parent-questions-form')).not.toBeInTheDocument());
    expect(within(list).queryByRole('button')).not.toBeInTheDocument();
    expect(within(list).getByRole('checkbox', { name: QUESTION })).not.toBeChecked();
  });

  it('shows the load failure instead of an empty list', async () => {
    prepQuestionsApi.listPrepQuestions.mockRejectedValue(new Error('network'));
    renderTab('/children/4/meeting-prep');
    await screen.findByTestId('parent-questions-error');
    expect(screen.queryByTestId('parent-questions-empty')).not.toBeInTheDocument();
  });
});
