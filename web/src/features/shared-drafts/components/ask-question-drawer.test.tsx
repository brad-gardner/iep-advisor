import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AskQuestionDrawer } from './ask-question-drawer';
import { DraftReviewContext, type DraftReviewContextValue } from '../hooks/draft-review-context';
import type { DraftAnswerDto } from '../types';

const sharedDraftsApi = vi.hoisted(() => ({
  askDraftQuestion: vi.fn(),
}));
vi.mock('../api/shared-drafts-api', () => sharedDraftsApi);

function renderDrawer(contextOverrides: Partial<DraftReviewContextValue> = {}) {
  const ctx: DraftReviewContextValue = {
    revisionId: 1,
    canRespond: true,
    notes: [],
    addNote: vi.fn(),
    removeNote: vi.fn(async () => ({ ok: true })),
    responses: [],
    addResponse: vi.fn(),
    explanations: {
      ensureLoaded: vi.fn(),
      isLoading: false,
      error: null,
      getItemExplanation: () => null,
      getSectionExplanation: () => null,
      disclaimer: null,
    },
    ...contextOverrides,
  };
  return render(
    <DraftReviewContext.Provider value={ctx}>
      <AskQuestionDrawer
        open
        onClose={() => {}}
        revisionId={1}
        targetLabel="Reading goal"
        data-testid="ask-question-drawer"
      />
    </DraftReviewContext.Provider>
  );
}

const answer: DraftAnswerDto = {
  noteId: 1,
  question: 'Question A',
  answer: 'Here is the answer.',
  citations: [],
  answeredAt: '2026-01-01T00:00:00.000Z',
  disclaimer: 'AI-generated.',
};

describe('AskQuestionDrawer', () => {
  it('disables the composer while a question is in flight so a second question typed in the gap is never wiped', async () => {
    const user = userEvent.setup();
    let resolveAsk!: (value: { success: true; data: DraftAnswerDto }) => void;
    sharedDraftsApi.askDraftQuestion.mockReturnValue(
      new Promise((resolve) => {
        resolveAsk = resolve;
      })
    );

    renderDrawer();

    const input = screen.getByTestId('ask-question-drawer-input');
    await user.type(input, 'Question A');
    await user.click(screen.getByTestId('ask-question-drawer-submit'));

    // The composer must be locked for the whole async gap — otherwise a
    // second question typed here would be silently deleted when the first
    // answer's `setQuestion('')` lands (the regression this test guards).
    expect(input).toBeDisabled();

    await user.type(input, ' — more text while waiting');
    expect(input).toHaveValue('Question A');

    resolveAsk({ success: true, data: answer });

    await waitFor(() => expect(input).not.toBeDisabled());
    expect(input).toHaveValue('');
  });

  it('re-enables the composer and leaves it editable after the request settles', async () => {
    const user = userEvent.setup();
    sharedDraftsApi.askDraftQuestion.mockResolvedValue({ success: true, data: answer });

    renderDrawer();

    const input = screen.getByTestId('ask-question-drawer-input');
    await user.type(input, 'Question A');
    await user.click(screen.getByTestId('ask-question-drawer-submit'));

    await waitFor(() => expect(input).not.toBeDisabled());

    await user.type(input, 'Next question');
    expect(input).toHaveValue('Next question');
  });

  it('renders a stored question as markdown, matching how the answer renders', async () => {
    renderDrawer({
      notes: [
        {
          id: 7,
          revisionId: 1,
          question: 'Is **this** ambitious enough?',
          answer: 'Yes, it looks appropriate.',
          targetFieldKey: null,
          targetRowId: null,
          citations: [],
          createdAt: '2026-01-01T00:00:00.000Z',
        },
      ],
    });

    const question = await screen.findByTestId('note-question-7');
    expect(question.querySelector('strong')).toHaveTextContent('this');
  });
});
