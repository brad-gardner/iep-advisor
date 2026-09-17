import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { AdvocacyGoal } from '@/types/api';
import { AdvocacyGoalCard } from './advocacy-goal-card';

function makeGoal(overrides: Partial<AdvocacyGoal> = {}): AdvocacyGoal {
  return {
    id: 1,
    childProfileId: 3,
    goalText: 'Improve reading fluency to grade level',
    category: null,
    displayOrder: 1,
    createdAt: '2026-01-01T00:00:00.000Z',
    updatedAt: '2026-01-01T00:00:00.000Z',
    ...overrides,
  };
}

describe('AdvocacyGoalCard', () => {
  it('renders a stored markdown goal text as formatted HTML', () => {
    render(<AdvocacyGoalCard goal={makeGoal({ goalText: 'Increase **independent** reading time.' })} isFirst isLast />);

    const strong = screen.getByText('independent');
    expect(strong.tagName).toBe('STRONG');
  });
});
