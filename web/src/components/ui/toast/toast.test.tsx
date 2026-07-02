import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ToastProvider } from './toast-provider';
import { useToast } from './use-toast';

function Harness() {
  const { show } = useToast();
  return (
    <>
      <button type="button" onClick={() => show({ message: 'Saved!', variant: 'success' })}>
        show
      </button>
      <button type="button" onClick={() => show({ message: 'Different', variant: 'info' })}>
        show-other
      </button>
    </>
  );
}

function setup() {
  // `delay: null` stops user-event from scheduling its own inter-event timers,
  // which otherwise deadlock against vitest fake timers; `advanceTimers` flushes
  // any that remain.
  const user = userEvent.setup({ advanceTimers: (ms) => vi.advanceTimersByTime(ms), delay: null });
  render(
    <ToastProvider>
      <Harness />
    </ToastProvider>
  );
  return { user };
}

describe('ToastProvider / useToast', () => {
  beforeEach(() => {
    // `shouldAdvanceTime` lets real time trickle forward so user-event's own
    // internal awaits resolve, while explicit `advanceTimersByTime` still jumps
    // the toast's 5s auto-dismiss deterministically.
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    // Flush any pending auto-dismiss timers inside act so their state updates
    // don't warn, then restore real timers.
    act(() => {
      vi.runOnlyPendingTimers();
    });
    vi.useRealTimers();
  });

  it('exposes a polite live region that is always mounted', () => {
    setup();
    const region = screen.getByRole('status');
    expect(region).toHaveAttribute('aria-live', 'polite');
  });

  it('show() renders a toast with the message', async () => {
    const { user } = setup();
    await user.click(screen.getByRole('button', { name: 'show' }));
    expect(screen.getByTestId('toast')).toHaveTextContent('Saved!');
  });

  it('does not steal focus when a toast appears', async () => {
    const { user } = setup();
    const trigger = screen.getByRole('button', { name: 'show' });
    await user.click(trigger);
    // Focus stays on the element the user was interacting with.
    expect(document.activeElement).toBe(trigger);
  });

  it('auto-dismisses after the default duration', async () => {
    const { user } = setup();
    await user.click(screen.getByRole('button', { name: 'show' }));
    expect(screen.getByTestId('toast')).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(5000);
    });
    expect(screen.queryByTestId('toast')).not.toBeInTheDocument();
  });

  it('dismisses manually via the close button', async () => {
    const { user } = setup();
    await user.click(screen.getByRole('button', { name: 'show' }));
    expect(screen.getByTestId('toast')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Dismiss notification' }));
    expect(screen.queryByTestId('toast')).not.toBeInTheDocument();
  });

  it('dedupes identical concurrent messages', async () => {
    const { user } = setup();
    await user.click(screen.getByRole('button', { name: 'show' }));
    await user.click(screen.getByRole('button', { name: 'show' }));
    expect(screen.getAllByTestId('toast')).toHaveLength(1);
  });

  it('resets the auto-dismiss dwell when an identical message is re-shown', async () => {
    const { user } = setup();
    await user.click(screen.getByRole('button', { name: 'show' }));
    act(() => {
      vi.advanceTimersByTime(3000);
    });
    expect(screen.getByTestId('toast')).toBeInTheDocument();

    // Re-show the identical message: deduped to one card, but the dwell restarts.
    await user.click(screen.getByRole('button', { name: 'show' }));
    expect(screen.getAllByTestId('toast')).toHaveLength(1);

    // 3s past the re-show (6s past the first) — still visible because it reset.
    act(() => {
      vi.advanceTimersByTime(3000);
    });
    expect(screen.getByTestId('toast')).toBeInTheDocument();

    // Past the fresh 5s window from the re-show — now gone.
    act(() => {
      vi.advanceTimersByTime(2000);
    });
    expect(screen.queryByTestId('toast')).not.toBeInTheDocument();
  });

  it('stacks distinct messages', async () => {
    const { user } = setup();
    await user.click(screen.getByRole('button', { name: 'show' }));
    await user.click(screen.getByRole('button', { name: 'show-other' }));
    expect(screen.getAllByTestId('toast')).toHaveLength(2);
  });
});
