import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const setLanguage = vi.hoisted(() => vi.fn());
vi.mock('@/features/auth/hooks/use-auth', () => ({
  useAuth: () => ({ setLanguage }),
}));

import { LanguageSwitcher } from './language-switcher';

describe('LanguageSwitcher', () => {
  beforeEach(() => {
    setLanguage.mockReset();
    setLanguage.mockResolvedValue({ success: true });
  });

  it('shows the bilingual label and both languages by their own names (no flags)', () => {
    render(<LanguageSwitcher data-testid="lang" />);

    expect(screen.getByText('Idioma / Language')).toBeInTheDocument();
    const select = screen.getByTestId('lang');
    expect(select).toHaveTextContent('English');
    expect(select).toHaveTextContent('Español');
  });

  it('is labeled accessibly and keyboard-operable (a native select)', () => {
    render(<LanguageSwitcher data-testid="lang" />);
    expect(screen.getByLabelText('Idioma / Language')).toBe(screen.getByTestId('lang'));
  });

  it('calls setLanguage with the chosen language on change', async () => {
    const user = userEvent.setup();
    render(<LanguageSwitcher data-testid="lang" />);

    await user.selectOptions(screen.getByTestId('lang'), 'es');

    expect(setLanguage).toHaveBeenCalledWith('es');
  });

  it('shows an inline error if persisting the choice fails, without blocking the switch itself', async () => {
    setLanguage.mockResolvedValue({ success: false, error: 'nope' });
    const user = userEvent.setup();
    render(<LanguageSwitcher data-testid="lang" />);

    await user.selectOptions(screen.getByTestId('lang'), 'es');

    expect(await screen.findByRole('alert')).toHaveTextContent("Couldn't save your language preference");
  });
});
