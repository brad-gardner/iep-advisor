import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { User } from '@/types/api';
import { AccountSetupNotices } from './account-setup-notices';
import { hasAccountSetupNotices } from '../lib/account-setup';

function makeUser(overrides: Partial<User> = {}): User {
  return {
    id: 1,
    email: 'priya@example.com',
    firstName: 'Priya',
    lastName: 'Parent',
    state: 'OH',
    role: 'Parent',
    fullName: 'Priya Parent',
    onboardingCompleted: true,
    subscriptionStatus: 'active',
    ...overrides,
  };
}

function renderNotices(user: User | null) {
  return render(
    <MemoryRouter>
      <AccountSetupNotices user={user} />
    </MemoryRouter>
  );
}

describe('AccountSetupNotices', () => {
  it('shows the onboarding banner for an incomplete onboarding', () => {
    renderNotices(makeUser({ onboardingCompleted: false }));
    expect(screen.getByTestId('onboarding-banner')).toBeInTheDocument();
    expect(screen.getByTestId('onboarding-get-started').closest('a')).toHaveAttribute('href', '/onboarding');
  });

  it('shows the "set your state" notice once onboarding is complete with no state', () => {
    renderNotices(makeUser({ onboardingCompleted: true, state: null }));
    expect(screen.getByText('Set your state for better guidance')).toBeInTheDocument();
    expect(screen.queryByTestId('onboarding-banner')).not.toBeInTheDocument();
  });

  it('shows nothing once onboarding is complete and a state is set', () => {
    renderNotices(makeUser({ onboardingCompleted: true, state: 'OH' }));
    expect(screen.queryByTestId('onboarding-banner')).not.toBeInTheDocument();
    expect(screen.queryByText('Set your state for better guidance')).not.toBeInTheDocument();
  });

  it('hasAccountSetupNotices agrees with what actually renders', () => {
    expect(hasAccountSetupNotices(makeUser({ onboardingCompleted: false }))).toBe(true);
    expect(hasAccountSetupNotices(makeUser({ onboardingCompleted: true, state: null }))).toBe(true);
    expect(hasAccountSetupNotices(makeUser({ onboardingCompleted: true, state: 'OH' }))).toBe(false);
    expect(hasAccountSetupNotices(null)).toBe(false);
  });
});
