import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { usePolling } from './use-polling';

// A deferred promise so a test can hold a poll "in flight" and resolve it on cue.
function deferred<T = void>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((res) => {
    resolve = res;
  });
  return { promise, resolve };
}

describe('usePolling', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('schedules the next poll only after the previous one settles', async () => {
    const fn = vi.fn().mockResolvedValue(undefined);
    renderHook(() => usePolling(fn, 1000, true, 10));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });
    expect(fn).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });
    expect(fn).toHaveBeenCalledTimes(2);
  });

  it('does not schedule another poll after unmount, even once an in-flight call resolves later', async () => {
    const gate = deferred();
    const fn = vi.fn(() => gate.promise);

    const { unmount } = renderHook(() => usePolling(fn, 1000, true, 10));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });
    expect(fn).toHaveBeenCalledTimes(1);

    // Unmount while the poll callback is still awaiting fn().
    unmount();

    await act(async () => {
      gate.resolve();
      // Give the in-flight await a chance to settle, then run well past
      // another interval — if the cleanup didn't stop the loop, a second
      // (orphaned) call would show up here.
      await vi.advanceTimersByTimeAsync(10000);
    });

    expect(fn).toHaveBeenCalledTimes(1);
  });

  it('stops scheduling once disabled, without waiting for an in-flight call', async () => {
    const fn = vi.fn().mockResolvedValue(undefined);
    const { rerender } = renderHook(({ enabled }) => usePolling(fn, 1000, enabled, 10), {
      initialProps: { enabled: true },
    });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });
    expect(fn).toHaveBeenCalledTimes(1);

    rerender({ enabled: false });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(5000);
    });
    expect(fn).toHaveBeenCalledTimes(1);
  });
});
