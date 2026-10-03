import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { CompletenessSummary } from '../lib/completeness';

const sectionDom = vi.hoisted(() => ({ jumpToField: vi.fn() }));
vi.mock('../lib/section-dom', () => sectionDom);

import { CompletenessStrip } from './completeness-strip';

function summary(overrides: Partial<CompletenessSummary> = {}): CompletenessSummary {
  return { items: [], percent: 100, filledCount: 1, totalCount: 1, ...overrides };
}

describe('CompletenessStrip', () => {
  it('shows "Nothing flagged" when there are no items', () => {
    render(<CompletenessStrip summary={summary()} updating={false} />);
    expect(screen.getByText('Nothing flagged')).toBeInTheDocument();
    expect(screen.queryByTestId('completeness-show-items')).not.toBeInTheDocument();
  });

  it('shows the percent, required and advisory counts', () => {
    render(
      <CompletenessStrip
        summary={summary({
          percent: 64,
          items: [
            { key: 'r1', severity: 'required', message: 'Eligibility is empty', fieldKey: 'f1', fieldId: 1, sectionId: 1 },
            { key: 'a1', severity: 'advisory', message: 'Goal 1 has no owner', fieldKey: 'f2', fieldId: 2, sectionId: 2 },
            { key: 'a2', severity: 'advisory', message: 'Goal 1 has no objectives', fieldKey: 'f2', fieldId: 2, sectionId: 2 },
          ],
        })}
        updating={false}
      />
    );
    expect(screen.getByTestId('completeness-percent')).toHaveTextContent('64%');
    expect(screen.getByText('1 required')).toBeInTheDocument();
    expect(screen.getByText('2 advisory')).toBeInTheDocument();
  });

  it('Show items expands the list, and clicking an item jumps to its field', async () => {
    const user = userEvent.setup();
    render(
      <CompletenessStrip
        summary={summary({
          percent: 50,
          items: [{ key: 'r1', severity: 'required', message: 'Eligibility is empty', fieldKey: 'f1', fieldId: 9, sectionId: 3 }],
        })}
        updating={false}
      />
    );

    expect(screen.queryByTestId('completeness-items')).not.toBeInTheDocument();
    const toggle = screen.getByTestId('completeness-show-items');
    expect(toggle).toHaveAttribute('aria-expanded', 'false');

    await user.click(toggle);
    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    const item = screen.getByTestId('completeness-item-r1');
    expect(item).toHaveTextContent('Required · Eligibility is empty');

    await user.click(item);
    expect(sectionDom.jumpToField).toHaveBeenCalledWith('document-field-9', 3);
  });

  it('shows an "updating…" indicator while an open section is saving', () => {
    render(<CompletenessStrip summary={summary()} updating />);
    expect(screen.getByTestId('completeness-updating')).toHaveTextContent('updating…');
  });

  it('omits the updating indicator when nothing is saving', () => {
    render(<CompletenessStrip summary={summary()} updating={false} />);
    expect(screen.queryByTestId('completeness-updating')).not.toBeInTheDocument();
  });
});
