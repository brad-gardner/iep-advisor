import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import i18n from '@/lib/i18n';

const childrenApi = vi.hoisted(() => ({ getChildren: vi.fn() }));
vi.mock('../api/children-api', () => childrenApi);

import { useChildren } from './use-children';

describe('useChildren', () => {
  afterEach(async () => {
    vi.clearAllMocks();
    await i18n.changeLanguage('en');
  });

  it('returns the loaded children on success', async () => {
    childrenApi.getChildren.mockResolvedValue({ success: true, data: [{ id: 1 }] });
    const { result } = renderHook(() => useChildren());

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.children).toEqual([{ id: 1 }]);
    expect(result.current.error).toBeNull();
  });

  it('translates a load failure immediately into the active language on a later switch, without refetching', async () => {
    childrenApi.getChildren.mockRejectedValue(new Error('network down'));
    const { result } = renderHook(() => useChildren());

    await waitFor(() => expect(result.current.error).toBe('Failed to load children'));
    expect(childrenApi.getChildren).toHaveBeenCalledTimes(1);

    // A language switch alone (no `reload()`) must update the error text —
    // it's translated at render from a stored flag, not a load-time
    // snapshot — and must NOT trigger another fetch (phase 2 review: `t`
    // was removed from the load effect's dependencies for exactly this).
    await act(async () => {
      await i18n.changeLanguage('es');
    });

    await waitFor(() => expect(result.current.error).toBe('No se pudieron cargar los hijos'));
    expect(childrenApi.getChildren).toHaveBeenCalledTimes(1);
  });
});
