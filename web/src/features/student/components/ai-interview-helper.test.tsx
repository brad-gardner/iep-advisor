import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AiInterviewHelper } from './ai-interview-helper';

describe('AiInterviewHelper', () => {
  it('asks the assistant and shows the suggestion', async () => {
    const user = userEvent.setup();
    const onInterview = vi.fn().mockResolvedValue({ suggestion: 'I want more time on tests.' });
    const onSave = vi.fn().mockResolvedValue(true);
    render(<AiInterviewHelper onInterview={onInterview} onSave={onSave} />);

    await user.type(screen.getByTestId('ai-interview-prompt'), 'extra time');
    await user.click(screen.getByTestId('ai-interview-ask'));

    expect(onInterview).toHaveBeenCalledWith('extra time');
    expect(await screen.findByTestId('ai-interview-suggestion')).toHaveTextContent('I want more time on tests.');
  });

  it('saves the suggestion as a meeting statement and resets', async () => {
    const user = userEvent.setup();
    const onInterview = vi.fn().mockResolvedValue({ suggestion: 'I want more time.' });
    const onSave = vi.fn().mockResolvedValue(true);
    render(<AiInterviewHelper onInterview={onInterview} onSave={onSave} />);

    await user.type(screen.getByTestId('ai-interview-prompt'), 'extra time');
    await user.click(screen.getByTestId('ai-interview-ask'));
    await screen.findByTestId('ai-interview-suggestion');

    await user.click(screen.getByTestId('ai-interview-save-statement'));
    expect(onSave).toHaveBeenCalledWith('I want more time.', 'MeetingStatement');
    expect(screen.queryByTestId('ai-interview-suggestion')).not.toBeInTheDocument();
  });

  it('shows an error when the assistant fails', async () => {
    const user = userEvent.setup();
    const onInterview = vi.fn().mockResolvedValue(null);
    render(<AiInterviewHelper onInterview={onInterview} onSave={vi.fn()} />);

    await user.type(screen.getByTestId('ai-interview-prompt'), 'extra time');
    await user.click(screen.getByTestId('ai-interview-ask'));

    expect(await screen.findByTestId('ai-interview-error')).toHaveTextContent('The assistant could not help right now');
  });
});
