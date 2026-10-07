import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import i18n from '@/lib/i18n';
import type { AuthoredDocumentPdfStatusDto } from '../types';

const documentsApi = vi.hoisted(() => ({
  getAuthoredPdfStatus: vi.fn(),
  retryAuthoredPdf: vi.fn(),
}));
vi.mock('../api/documents-api', () => documentsApi);

import { useAuthoredPdfStatus } from './use-authored-pdf-status';

function status(overrides: Partial<AuthoredDocumentPdfStatusDto> = {}): AuthoredDocumentPdfStatusDto {
  return {
    versionId: 1,
    renderStatus: 'Rendered',
    renderedAt: '2026-01-01T00:00:00.000Z',
    errorMessage: null,
    ...overrides,
  };
}

describe('useAuthoredPdfStatus', () => {
  afterEach(async () => {
    vi.clearAllMocks();
    await i18n.changeLanguage('en');
  });

  it('re-fetches status and clears the previous language’s cached status on a language switch', async () => {
    // English row already Rendered. Left as the persistent default (not
    // `...Once`) so a later effect run this test doesn't explicitly control
    // (e.g. the language reverting to English in `afterEach`, below) still
    // resolves to something valid instead of `undefined`.
    documentsApi.getAuthoredPdfStatus.mockResolvedValue({
      success: true,
      data: status({ renderStatus: 'Rendered' }),
    });

    const { result, unmount } = renderHook(() => useAuthoredPdfStatus(1));

    await waitFor(() => expect(result.current.status).toBe('Rendered'));
    expect(documentsApi.getAuthoredPdfStatus).toHaveBeenCalledTimes(1);

    // The server hasn't created the Spanish row yet — it does so on this
    // poll and comes back Pending. Hold the promise open so the test can
    // observe the state immediately after the switch, before it resolves.
    let resolveSpanish!: (value: { success: true; data: AuthoredDocumentPdfStatusDto }) => void;
    documentsApi.getAuthoredPdfStatus.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveSpanish = resolve;
        })
    );

    await act(async () => {
      await i18n.changeLanguage('es');
    });

    // A new fetch for the new language was made...
    expect(documentsApi.getAuthoredPdfStatus).toHaveBeenCalledTimes(2);
    // ...and the OLD (English) Rendered status must not linger while the
    // new language's row is still in flight.
    expect(result.current.status).toBeNull();
    expect(result.current.isLoading).toBe(true);

    resolveSpanish({ success: true, data: status({ renderStatus: 'Pending' }) });
    await waitFor(() => expect(result.current.status).toBe('Pending'));

    unmount();
  });

  it('ignores a non-English seed status until the active language’s own row has loaded', async () => {
    documentsApi.getAuthoredPdfStatus.mockResolvedValue({
      success: true,
      data: status({ renderStatus: 'Pending' }),
    });
    await i18n.changeLanguage('es');

    // `initialStatus` reflects the ENGLISH row's status from list/detail
    // data; it must not be trusted while Spanish is active.
    const { result } = renderHook(() => useAuthoredPdfStatus(1, 'Rendered'));

    expect(result.current.status).toBeNull();

    await waitFor(() => expect(result.current.status).toBe('Pending'));
  });

  it('trusts the seed status while English is active', async () => {
    documentsApi.getAuthoredPdfStatus.mockResolvedValue({
      success: true,
      data: status({ renderStatus: 'Rendered' }),
    });

    const { result } = renderHook(() => useAuthoredPdfStatus(1, 'Rendered'));

    // Seeded immediately, before the first fetch resolves.
    expect(result.current.status).toBe('Rendered');

    await waitFor(() => expect(result.current.isLoading).toBe(false));
  });
});
