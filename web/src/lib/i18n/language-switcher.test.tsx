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

    // The label's accessible name (which `getByLabelText` below also relies
    // on) is "Idioma / Language", but its text is split across two `lang`-
    // tagged spans — `getByText`'s default matcher only reads a node's own
    // direct text-node children, so check the label's full `textContent`
    // directly rather than via a plain string `getByText` match.
    const label = screen.getByText('Idioma', { selector: 'span[lang="es"]' }).closest('label');
    expect(label).toHaveTextContent('Idioma / Language');

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

  it('marks each option with its own language, for correct screen-reader pronunciation', () => {
    render(<LanguageSwitcher data-testid="lang" />);

    expect(screen.getByRole('option', { name: 'English' })).toHaveAttribute('lang', 'en');
    expect(screen.getByRole('option', { name: 'Español' })).toHaveAttribute('lang', 'es');
  });

  it('renders no hint by default, and an associated one when given', () => {
    const { rerender } = render(<LanguageSwitcher data-testid="lang" />);
    expect(screen.getByTestId('lang')).not.toHaveAttribute('aria-describedby');

    rerender(<LanguageSwitcher data-testid="lang" hint="Changes the language of the app." />);
    const select = screen.getByTestId('lang');
    const hint = screen.getByText('Changes the language of the app.');
    expect(select).toHaveAttribute('aria-describedby', hint.id);
  });
});
