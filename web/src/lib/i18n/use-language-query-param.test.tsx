import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { removeToken, setToken } from '@/lib/auth';
import { clearPreLoginLanguage, getPreLoginLanguage } from './detect';
import i18n from './index';
import { useLanguageQueryParam } from './use-language-query-param';

function renderWithQuery(search: string) {
  return renderHook(() => useLanguageQueryParam(), {
    wrapper: ({ children }) => <MemoryRouter initialEntries={[`/anything${search}`]}>{children}</MemoryRouter>,
  });
}

describe('useLanguageQueryParam', () => {
  beforeEach(() => {
    removeToken();
    clearPreLoginLanguage();
  });

  afterEach(async () => {
    removeToken();
    clearPreLoginLanguage();
    await i18n.changeLanguage('en');
  });

  it('switches the display language and remembers it as the pre-login choice while signed out', async () => {
    renderWithQuery('?lang=es');

    await waitFor(() => expect(i18n.language).toBe('es'));
    expect(getPreLoginLanguage()).toBe('es');
  });

  it('switches the display language but does NOT write the pre-login key while signed in', async () => {
    setToken('a-jwt');

    renderWithQuery('?lang=es');

    await waitFor(() => expect(i18n.language).toBe('es'));
    // Never persisted: the pre-login key may only ever hold a NOT-YET-
    // signed-in visitor's own choice (see the module doc comment on
    // `detect.ts`) — a signed-in account's incidental link click must never
    // silently decide a later, possibly anonymous, visitor's backfill on a
    // shared device.
    expect(getPreLoginLanguage()).toBeNull();
  });
});
