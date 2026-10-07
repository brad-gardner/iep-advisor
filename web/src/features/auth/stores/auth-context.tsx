import { createContext, useCallback, useEffect, useRef, useState } from 'react';
import * as Sentry from '@sentry/react';
import { useTranslation } from 'react-i18next';
import type {
  User,
  LoginRequest,
  RegisterRequest,
  RegisterDistrictRequest,
  UpdateProfileRequest,
} from '@/types/api';
import {
  login as loginApi,
  register as registerApi,
  registerDistrict as registerDistrictApi,
  getCurrentUser,
  updateProfile as updateProfileApi,
  completeOnboarding as completeOnboardingApi,
} from '../api/auth-api';
import { getToken, setToken, removeToken, setStoredUser, getStoredUser } from '@/lib/auth';
import i18n from '@/lib/i18n';
import {
  isSupportedLanguage,
  getPreLoginLanguage,
  setPreLoginLanguage,
  clearPreLoginLanguage,
  setLastDisplayLanguage,
  clearLastDisplayLanguage,
  detectBrowserLanguage,
  DEFAULT_LANGUAGE,
  type SupportedLanguage,
} from '@/lib/i18n/detect';

interface LoginResult {
  success: boolean;
  error?: string;
  requiresMfa?: boolean;
  mfaPendingToken?: string;
}

interface AuthContextType {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  mfaPendingToken: string | null;
  login: (data: LoginRequest) => Promise<LoginResult>;
  register: (data: RegisterRequest) => Promise<{ success: boolean; error?: string }>;
  registerDistrict: (data: RegisterDistrictRequest) => Promise<{ success: boolean; error?: string }>;
  updateProfile: (data: UpdateProfileRequest) => Promise<{ success: boolean; error?: string }>;
  completeOnboarding: () => Promise<{ success: boolean; error?: string }>;
  completeMfaLogin: (token: string, user: User) => void;
  applySession: (token: string, user: User) => void;
  refreshUser: () => Promise<void>;
  logout: () => void;
  /**
   * Switches the active UI language immediately, then persists the choice:
   * to the account (`PUT /api/auth/me`) when signed in, or to `localStorage`
   * as the pre-login choice otherwise. Used by every `LanguageSwitcher` and
   * by the Profile page's Language field.
   */
  setLanguage: (language: SupportedLanguage) => Promise<{ success: boolean; error?: string }>;
}

export const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const { t } = useTranslation('auth');
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [mfaPendingToken, setMfaPendingToken] = useState<string | null>(null);

  // Latest-wins guard for everything that can decide the active language —
  // an explicit `setLanguage` call, and the background sync below. Bumped by
  // `setLanguage` the instant it's invoked (before anything async), and
  // captured by every async flow that might later apply a language
  // (`syncLanguagePreference`'s callers below, each *before* starting the
  // fetch that will eventually call it) so a slower, now-superseded
  // operation can tell it lost the race: if `languageGenerationRef.current`
  // no longer matches the generation it captured, a newer explicit switch
  // happened while it was in flight, and it must neither persist its own
  // (possibly stale) choice nor overwrite the newer one (see the plan's
  // phase-1 review: language sync races). Call sites below carry only a
  // one-line reminder of which check this is; this is the single place that
  // explains why it exists.
  const languageGenerationRef = useRef(0);

  // The in-flight backfill PUT started by `syncLanguagePreference` below, if
  // any — so an explicit `setLanguage` call can await it (see there) and
  // guarantee the user's own choice is always the *last* write the server
  // sees, even if both requests happen to be in flight at once.
  const pendingBackfillRef = useRef<Promise<void> | null>(null);

  // Applies the account's saved language once a user is known. When there is
  // no saved preference yet (a first sign-in, or an account created before
  // this field existed), this persists a resolved language so future
  // sign-ins, emails, and AI responses know it too. Fire-and-forget by
  // design: it must never block or delay setting the signed-in user, since
  // `ProtectedRoute`/`PublicRoute` key off that state synchronously.
  const syncLanguagePreference = useCallback((userData: User, generation: number) => {
    if (languageGenerationRef.current !== generation) return; // superseded — see languageGenerationRef

    if (isSupportedLanguage(userData.preferredLanguage)) {
      if (i18n.language !== userData.preferredLanguage) {
        void i18n.changeLanguage(userData.preferredLanguage);
      }
      return;
    }

    // The backfill value is deliberately NOT `i18n.language`: on a shared
    // device, the language currently active could be nothing more than the
    // previous user's sign-out carry-over (`getLastDisplayLanguage`, applied
    // for *display* only — see `lib/i18n/detect.ts`), which must never leak
    // into this account's saved preference. Only an explicit pre-login
    // choice counts as this visitor's own; otherwise fall back to the
    // browser's own languages, then `en`.
    const resolved: SupportedLanguage = getPreLoginLanguage() ?? detectBrowserLanguage();

    // Apply on screen too — not just persisted. Without this, a visitor whose
    // browser language differed from whatever i18next happened to initialize
    // with (e.g. the sign-out carry-over from a previous account on this
    // device) would have the right language saved to their new account but
    // see the wrong one until their next reload.
    if (i18n.language !== resolved) {
      void i18n.changeLanguage(resolved);
    }

    const backfillPromise = updateProfileApi({ preferredLanguage: resolved })
      .then((response) => {
        if (!response.success || !response.data) return;
        if (languageGenerationRef.current !== generation) return; // superseded — see languageGenerationRef
        const savedLanguage = response.data.preferredLanguage;
        setUser((prev) => {
          if (!prev) return prev;
          const merged = { ...prev, preferredLanguage: savedLanguage };
          setStoredUser(JSON.stringify(merged));
          return merged;
        });
      })
      .catch(() => {
        // Non-fatal — retried the next time a user loads (loadUser/refreshUser).
      })
      .finally(() => {
        if (pendingBackfillRef.current === backfillPromise) pendingBackfillRef.current = null;
      });
    pendingBackfillRef.current = backfillPromise;
  }, []);

  const loadUser = useCallback(async () => {
    const token = getToken();
    if (!token) {
      setIsLoading(false);
      return;
    }

    try {
      // Try to get user from storage first — don't wait on the network
      // round-trip below just to show a page the user has already visited
      // once. Its language was already applied, synchronously, by
      // `detectInitialLanguage` at i18next init time (`lib/i18n/index.ts`
      // reads the very same stored user); re-applying it here would be a
      // redundant `changeLanguage` call for the already-active language.
      const storedUser = getStoredUser();
      if (storedUser) {
        setUser(JSON.parse(storedUser) as User);
      }

      // Verify with API. Captured before the request starts (see
      // `languageGenerationRef`'s doc comment) so a switch that happens
      // while this is in flight is never overwritten by its stale response.
      const generation = languageGenerationRef.current;
      const response = await getCurrentUser();
      if (response.success && response.data) {
        setUser(response.data);
        setStoredUser(JSON.stringify(response.data));
        syncLanguagePreference(response.data, generation);
      } else {
        removeToken();
        setUser(null);
      }
    } catch {
      removeToken();
      setUser(null);
    } finally {
      setIsLoading(false);
    }
  }, [syncLanguagePreference]);

  useEffect(() => {
    loadUser();
  }, [loadUser]);

  // Sync Sentry user context for error tracking
  useEffect(() => {
    if (user) {
      Sentry.setUser({ id: String(user.id), email: user.email });
    } else {
      Sentry.setUser(null);
    }
  }, [user]);

  // Single source of truth for persisting an authenticated session
  // (token + user) to storage and state. Reused by login, MFA completion,
  // and district signup so storage logic lives in exactly one place.
  const persistSession = useCallback((token: string, userData: User) => {
    setToken(token);
    setUser(userData);
    setStoredUser(JSON.stringify(userData));
    // The sign-out carry-over's job (showing the login page in the right
    // language) is done now that someone has actually signed in — clear it
    // so it can't later be mistaken for this or a future visitor's own
    // choice.
    clearLastDisplayLanguage();
    syncLanguagePreference(userData, languageGenerationRef.current);
    // The explicit pre-login choice's job is also done: `syncLanguagePreference`
    // above already read it synchronously (before this call returns) if this
    // account had no saved preference yet. Clearing it AFTER that call — never
    // before — means a future account signing in on this same device (a
    // shared/kiosk browser) never inherits a choice that belonged to whoever
    // was sitting at this device before *this* sign-in (todos/247).
    clearPreLoginLanguage();
  }, [syncLanguagePreference]);

  const login = async (data: LoginRequest): Promise<LoginResult> => {
    try {
      const response = await loginApi(data);
      if (response.success && response.data) {
        // Check if MFA is required
        if (response.data.requiresMfa && response.data.mfaPendingToken) {
          setMfaPendingToken(response.data.mfaPendingToken);
          return {
            success: false,
            requiresMfa: true,
            mfaPendingToken: response.data.mfaPendingToken,
          };
        }

        // Normal login (no MFA)
        if (response.data.token && response.data.user) {
          persistSession(response.data.token, response.data.user);
          return { success: true };
        }
      }
      return { success: false, error: response.message || t('context.loginFailed') };
    } catch (error) {
      return { success: false, error: t('context.loginError') };
    }
  };

  const completeMfaLogin = (token: string, userData: User) => {
    persistSession(token, userData);
    setMfaPendingToken(null);
  };

  // Public entry point for flows that already hold a minted JWT + user from the
  // backend (e.g. staff invite acceptance) and just need to persist the session
  // through the single source of truth.
  const applySession = (token: string, userData: User) => {
    persistSession(token, userData);
  };

  const registerDistrict = async (data: RegisterDistrictRequest) => {
    try {
      const response = await registerDistrictApi(data);
      // Backend returns the same shape as login (JWT + user); auto-login.
      if (response.success && response.data?.token && response.data.user) {
        persistSession(response.data.token, response.data.user);
        return { success: true };
      }
      return { success: false, error: response.message || t('context.registrationFailed') };
    } catch {
      return { success: false, error: t('context.registrationError') };
    }
  };

  const register = async (data: RegisterRequest) => {
    try {
      const response = await registerApi(data);
      if (response.success) {
        return { success: true };
      }
      return { success: false, error: response.message || t('context.registrationFailed') };
    } catch (error) {
      return { success: false, error: t('context.registrationError') };
    }
  };

  const updateProfile = async (data: UpdateProfileRequest) => {
    try {
      const response = await updateProfileApi(data);
      if (response.success && response.data) {
        setUser(response.data);
        setStoredUser(JSON.stringify(response.data));
        return { success: true };
      }
      return { success: false, error: response.message || t('context.updateFailed') };
    } catch {
      return { success: false, error: t('context.updateError') };
    }
  };

  const completeOnboarding = async () => {
    try {
      const response = await completeOnboardingApi();
      if (response.success) {
        setUser((prev) => {
          if (!prev) return prev;
          const updated = { ...prev, onboardingCompleted: true };
          setStoredUser(JSON.stringify(updated));
          return updated;
        });
        return { success: true };
      }
      return { success: false, error: response.message || t('context.onboardingFailed') };
    } catch {
      return { success: false, error: t('context.onboardingError') };
    }
  };

  const refreshUser = async () => {
    // Same latest-wins capture as `loadUser` — a refresh triggered mid-flow
    // (e.g. after accepting an invite) must not let its response, once it's
    // stale, re-decide the language out from under a switch the user made
    // while it was in flight.
    const generation = languageGenerationRef.current;
    try {
      const response = await getCurrentUser();
      if (response.success && response.data) {
        setUser(response.data);
        setStoredUser(JSON.stringify(response.data));
        syncLanguagePreference(response.data, generation);
      }
    } catch {
      // Keep the existing user on a transient failure.
    }
  };

  const logout = () => {
    // Bump first, before anything else: invalidates any in-flight
    // `setLanguage` PUT or backfill (`syncLanguagePreference`) so its
    // response — landing after sign-out — can't `setUser`/`setStoredUser`
    // and leave the app looking signed-in with no token (todos/247).
    languageGenerationRef.current++;

    // Keep whatever language was active — it's still correct, it just has
    // nowhere to live once the account's saved preference is gone. Recorded
    // as the sign-out *display* carry-over (never the explicit pre-login
    // key — see `lib/i18n/detect.ts`) so the login page stays in that
    // language instead of falling back to the browser, without that choice
    // ever being mistaken for the next signed-in account's own preference on
    // a shared device.
    setLastDisplayLanguage(isSupportedLanguage(i18n.language) ? i18n.language : DEFAULT_LANGUAGE);
    removeToken();
    setUser(null);
    setMfaPendingToken(null);
  };

  const setLanguage = async (language: SupportedLanguage) => {
    const generation = ++languageGenerationRef.current;
    await i18n.changeLanguage(language);

    if (languageGenerationRef.current !== generation) {
      // A newer explicit switch started before this one's `changeLanguage`
      // resolved (e.g. this call is for a lazily-loaded Spanish chunk that
      // took longer than a quick follow-up switch back to English). i18next
      // itself already keeps the *active* language correct in that case —
      // this just stops the superseded call from also persisting its now-
      // wrong choice to the account or `localStorage`.
      return { success: true };
    }

    if (i18n.resolvedLanguage !== language) {
      // `changeLanguage` resolved, but not to the requested language — e.g.
      // the lazy Spanish chunk failed to load and `lib/i18n/index.ts`'s
      // `failedLoading` handler reverted to English. The active language is
      // now, correctly, whatever actually loaded; never persist the one
      // that didn't.
      return { success: false, error: t('context.languageUpdateError') };
    }

    if (!user) {
      // No account to save it to yet — remembered for this browser until sign-in.
      setPreLoginLanguage(language);
      return { success: true };
    }

    try {
      // A backfill PUT from `syncLanguagePreference` may already be in
      // flight (a first sign-in with no saved preference yet). Let it
      // finish first — ignoring whatever it resolves to — so this explicit
      // choice is always the last write the server sees, never clobbered by
      // an older, already-superseded backfill landing after it.
      if (pendingBackfillRef.current) {
        await pendingBackfillRef.current.catch(() => undefined);
      }
      const response = await updateProfileApi({ preferredLanguage: language });
      if (languageGenerationRef.current !== generation) {
        // Superseded while the PUT was in flight — ignore this response so
        // it can't overwrite a newer switch's state.
        return { success: true };
      }
      if (response.success && response.data) {
        // Narrowed into its own `const` so the closure below keeps the
        // non-undefined type — TS doesn't carry a property-access narrowing
        // (`response.data`) through a nested function the way it does a
        // plain variable.
        const data = response.data;
        // Functional form, not a bare `setUser(data)`: if `logout()` ran
        // while this PUT was in flight, `user` is already `null` and must
        // stay that way — a slow response arriving after sign-out must never
        // resurrect a signed-in-looking user with no token (todos/247). The
        // generation check above already covers "superseded by a newer
        // language switch"; this covers "superseded by sign-out" too, since
        // logout doesn't change what `generation` captured at the top of this
        // function. The updater runs synchronously, so `wasSignedIn` is
        // correct by the time `setUser` returns — gating the `setStoredUser`
        // write below on the *current* state, not the stale `user` closure.
        let wasSignedIn = false;
        setUser((prev) => {
          if (prev) wasSignedIn = true;
          return prev ? data : prev;
        });
        if (wasSignedIn) setStoredUser(JSON.stringify(data));
        return { success: true };
      }
      return { success: false, error: response.message || t('context.updateFailed') };
    } catch {
      return { success: false, error: t('context.languageUpdateError') };
    }
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: !!user,
        isLoading,
        mfaPendingToken,
        login,
        register,
        registerDistrict,
        updateProfile,
        completeOnboarding,
        completeMfaLogin,
        applySession,
        refreshUser,
        logout,
        setLanguage,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
