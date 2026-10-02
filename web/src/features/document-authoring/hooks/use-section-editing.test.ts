import { act, renderHook } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { useSectionEditing } from './use-section-editing';

describe('useSectionEditing', () => {
  it('starts with nothing open', () => {
    const { result } = renderHook(() => useSectionEditing());
    expect(result.current.isOpen(1)).toBe(false);
    expect(result.current.anyOpen).toBe(false);
  });

  it('open/close track one section independently of another', () => {
    const { result } = renderHook(() => useSectionEditing());

    act(() => result.current.open(1));
    expect(result.current.isOpen(1)).toBe(true);
    expect(result.current.isOpen(2)).toBe(false);
    expect(result.current.anyOpen).toBe(true);

    act(() => result.current.open(2));
    expect(result.current.isOpen(1)).toBe(true);
    expect(result.current.isOpen(2)).toBe(true);

    act(() => result.current.close(1));
    expect(result.current.isOpen(1)).toBe(false);
    expect(result.current.isOpen(2)).toBe(true);
    expect(result.current.anyOpen).toBe(true);
  });

  it('toggle only ever opens (never silently closes)', () => {
    const { result } = renderHook(() => useSectionEditing());

    act(() => result.current.toggle(5));
    expect(result.current.isOpen(5)).toBe(true);

    // Calling it again while already open must not close it — Done/Discard
    // are the only way to close a section.
    act(() => result.current.toggle(5));
    expect(result.current.isOpen(5)).toBe(true);
  });

  it('closeAll closes every open section and anyOpen goes false', () => {
    const { result } = renderHook(() => useSectionEditing());

    act(() => {
      result.current.open(1);
      result.current.open(2);
    });
    expect(result.current.anyOpen).toBe(true);

    act(() => result.current.closeAll());
    expect(result.current.isOpen(1)).toBe(false);
    expect(result.current.isOpen(2)).toBe(false);
    expect(result.current.anyOpen).toBe(false);
  });
});
