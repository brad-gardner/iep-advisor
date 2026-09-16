import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RsvpButtonGroup } from './rsvp-button-group';

describe('RsvpButtonGroup', () => {
  it('calls onRespond with the pressed status', async () => {
    const user = userEvent.setup();
    const onRespond = vi.fn();
    render(<RsvpButtonGroup onRespond={onRespond} pending={null} testIdPrefix="rsvp" />);

    await user.click(screen.getByTestId('rsvp-accept'));
    expect(onRespond).toHaveBeenCalledWith('Accepted');

    await user.click(screen.getByTestId('rsvp-tentative'));
    expect(onRespond).toHaveBeenCalledWith('Tentative');

    await user.click(screen.getByTestId('rsvp-decline'));
    expect(onRespond).toHaveBeenCalledWith('Declined');
  });

  it('disables the whole group while one response is pending, showing that button as loading', () => {
    render(<RsvpButtonGroup onRespond={vi.fn()} pending="Accepted" testIdPrefix="rsvp" />);

    expect(screen.getByTestId('rsvp-accept')).toHaveAttribute('aria-busy', 'true');
    expect(screen.getByTestId('rsvp-tentative')).toBeDisabled();
    expect(screen.getByTestId('rsvp-decline')).toBeDisabled();
  });

  it('enables every button when idle', () => {
    render(<RsvpButtonGroup onRespond={vi.fn()} pending={null} testIdPrefix="rsvp" />);
    expect(screen.getByTestId('rsvp-accept')).toBeEnabled();
    expect(screen.getByTestId('rsvp-tentative')).toBeEnabled();
    expect(screen.getByTestId('rsvp-decline')).toBeEnabled();
  });
});
