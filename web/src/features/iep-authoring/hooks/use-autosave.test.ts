import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useAutosave } from './use-autosave';

// A deferred promise so a test can hold a save "in flight" and resolve it on cue.
function deferred<T = void>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

describe('useAutosave', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('flush() awaits an in-flight save instead of resolving early', async () => {
    // Regression: previously flush() returned an already-resolved promise while a
    // save was on the wire, so the finalize gate could read an unconfirmed state
    // and wrongly report "edits could not be saved".
    const gate = deferred();
    let resolved = 0;
    const saveFn = vi.fn(async () => {
      await gate.promise;
      resolved += 1;
    });

    const { result } = renderHook(() => useAutosave<string>(saveFn));

    // Queue an edit and let the debounce fire so a save is genuinely in flight.
    act(() => result.current.save('a'));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(700);
    });
    expect(saveFn).toHaveBeenCalledTimes(1);
    expect(result.current.status).toBe('saving');

    // flush() while the save is in flight must NOT resolve before the save settles.
    let flushSettled = false;
    const flushPromise = result.current.flush().then(() => {
      flushSettled = true;
    });
    await Promise.resolve();
    expect(flushSettled).toBe(false);

    // Once the underlying save completes, flush() resolves — and only then.
    await act(async () => {
      gate.resolve();
      await flushPromise;
    });
    expect(flushSettled).toBe(true);
    expect(resolved).toBe(1);
    expect(result.current.status).toBe('saved');
  });

  it('flush() persists a still-debounced edit (no wait for the timer)', async () => {
    const saveFn = vi.fn(async () => {});
    const { result } = renderHook(() => useAutosave<string>(saveFn));

    act(() => result.current.save('x'));
    // Do NOT advance the debounce timer; flush should drain it immediately.
    await act(async () => {
      await result.current.flush();
    });

    expect(saveFn).toHaveBeenCalledTimes(1);
    expect(saveFn).toHaveBeenCalledWith('x');
  });
});
