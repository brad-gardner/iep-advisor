import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import i18n from '@/lib/i18n';
import type { IepVersionPdfStatusDto } from '../types';

const iepVersionsApi = vi.hoisted(() => ({
  getPdfStatus: vi.fn(),
  retryPdf: vi.fn(),
}));
vi.mock('../api/iep-versions-api', () => iepVersionsApi);

import { usePdfStatus } from './use-pdf-status';

function status(overrides: Partial<IepVersionPdfStatusDto> = {}): IepVersionPdfStatusDto {
  return {
    versionId: 1,
    renderStatus: 'Rendered',
    url: 'https://files.example.com/en/version-1.pdf',
    renderedAt: '2026-01-01T00:00:00.000Z',
    errorMessage: null,
    ...overrides,
  };
}

describe('usePdfStatus', () => {
  afterEach(async () => {
    vi.clearAllMocks();
    await i18n.changeLanguage('en');
  });

  it('re-fetches status and clears the previous language’s cached status/url on a language switch', async () => {
    // English row already Rendered, with a cached download url. Left as the
    // persistent default (not `...Once`) so a later effect run this test
    // doesn't explicitly control (e.g. the language reverting to English in
    // `afterEach`, below) still resolves to something valid instead of
    // `undefined`.
    iepVersionsApi.getPdfStatus.mockResolvedValue({
      success: true,
      data: status({ renderStatus: 'Rendered', url: 'https://files.example.com/en/version-1.pdf' }),
    });

    const { result, unmount } = renderHook(() => usePdfStatus(1));

    await waitFor(() => expect(result.current.status).toBe('Rendered'));
    expect(result.current.url).toBe('https://files.example.com/en/version-1.pdf');
    expect(iepVersionsApi.getPdfStatus).toHaveBeenCalledTimes(1);

    // The server hasn't created the Spanish row yet — it does so on this
    // poll and comes back Pending. Hold the promise open so the test can
    // observe the state immediately after the switch, before it resolves.
    let resolveSpanish!: (value: { success: true; data: IepVersionPdfStatusDto }) => void;
    iepVersionsApi.getPdfStatus.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveSpanish = resolve;
        })
    );

    await act(async () => {
      await i18n.changeLanguage('es');
    });

    // A new fetch for the new language was made...
    expect(iepVersionsApi.getPdfStatus).toHaveBeenCalledTimes(2);
    // ...and the OLD (English) Rendered status + url must not linger — a
    // silent download of the wrong language's cached PDF is exactly the bug
    // being fixed here.
    expect(result.current.status).toBeNull();
    expect(result.current.url).toBeNull();
    expect(result.current.isLoading).toBe(true);

    resolveSpanish({ success: true, data: status({ renderStatus: 'Pending', url: null }) });
    await waitFor(() => expect(result.current.status).toBe('Pending'));
    expect(result.current.url).toBeNull();

    unmount();
  });

  it('ignores a non-English seed status until the active language’s own row has loaded', async () => {
    iepVersionsApi.getPdfStatus.mockResolvedValue({
      success: true,
      data: status({ renderStatus: 'Pending', url: null }),
    });
    await i18n.changeLanguage('es');

    // `initialStatus` reflects the ENGLISH row's status from list/detail
    // data; it must not be trusted (and no stale url served) while Spanish
    // is active.
    const { result } = renderHook(() => usePdfStatus(1, 'Rendered'));

    expect(result.current.status).toBeNull();
    expect(result.current.url).toBeNull();

    await waitFor(() => expect(result.current.status).toBe('Pending'));
  });

  it('trusts the seed status while English is active', async () => {
    iepVersionsApi.getPdfStatus.mockResolvedValue({
      success: true,
      data: status({ renderStatus: 'Rendered' }),
    });

    const { result } = renderHook(() => usePdfStatus(1, 'Rendered'));

    // Seeded immediately, before the first fetch resolves.
    expect(result.current.status).toBe('Rendered');

    await waitFor(() => expect(result.current.isLoading).toBe(false));
  });

  it('drops an English request that resolves after the switch to Spanish', async () => {
    iepVersionsApi.getPdfStatus.mockResolvedValue({
      success: true,
      data: status({ renderStatus: 'Error', url: null, errorMessage: 'boom' }),
    });
    const { result } = renderHook(() => usePdfStatus(1));
    await waitFor(() => expect(result.current.status).toBe('Error'));

    // Retry sent while English is active, still in flight across the switch.
    let resolveEnglishRetry!: (value: { success: true; data: IepVersionPdfStatusDto }) => void;
    iepVersionsApi.retryPdf.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveEnglishRetry = resolve;
        })
    );
    let retryDone!: Promise<void>;
    act(() => {
      retryDone = result.current.retry();
    });

    iepVersionsApi.getPdfStatus.mockResolvedValue({
      success: true,
      data: status({ renderStatus: 'Pending', url: null }),
    });
    await act(async () => {
      await i18n.changeLanguage('es');
    });
    await waitFor(() => expect(result.current.status).toBe('Pending'));

    // The English answer lands last; it must not overwrite the Spanish state.
    await act(async () => {
      resolveEnglishRetry({ success: true, data: status({ renderStatus: 'Rendered' }) });
      await retryDone;
    });
    expect(result.current.status).toBe('Pending');
    expect(result.current.url).toBeNull();
  });
});
