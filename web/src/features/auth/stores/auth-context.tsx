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

/**
 * Whether i18next has a `changeLanguage` call in flight right now
 * (`isLanguageChangingTo`, set synchronously at the start of the call and
 * cleared once it settles — see `node_modules/i18next/dist/cjs/i18next.js`).
 * Not part of i18next's public TS surface (`node_modules/i18next/index.d.ts`'s
 * `i18n` interface), so this reads it through a narrow, read-only cast
 * instead of widening to `any`. Used to decide whether a NEW
 * `changeLanguage` call is needed to supersede one already pending to a
 * different language — see `syncLanguagePreference` below (todos/248 P2).
 */
function pendingI18nLanguageChange(): string | undefined {
  return (i18n as unknown as { isLanguageChangingTo?: string }).isLanguageChangingTo;
}

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
  // an explicit `setLanguage` call, and the background sync below. Bumped
  // the instant any of those is settled: by `setLanguage` itself (before
  // anything async), by `logout` (so a slow PUT/backfill can't resurrect a
  // signed-in look after sign-out — todos/247), and by `persistSession` (so
  // a slow `setLanguage` call started while signed out can't write the
  // pre-login key, or anything else, once someone has since signed in).
  // Captured by every async flow that might later apply a language
  // (`syncLanguagePreference`'s callers below, each *before* starting the
  // fetch that will eventually call it) so a slower, now-superseded
  // operation can tell it lost the race: if `languageGenerationRef.current`
  // no longer matches the generation it captured, a newer bump happened
  // while it was in flight, and it must neither persist its own (possibly
  // stale) choice nor overwrite the newer one (see the plan's phase-1
  // review: language sync races). Call sites below carry only a one-line
  // reminder of which check this is; this is the single place that explains
  // why it exists.
  const languageGenerationRef = useRef(0);

  // The in-flight backfill PUT started by `syncLanguagePreference` below, if
  // any — so an explicit `setLanguage` call can await it (see there) and
  // guarantee the user's own choice is always the *last* write the server
  // sees, even if both requests happen to be in flight at once.
  const pendingBackfillRef = useRef<Promise<void> | null>(null);

  // An explicit pre-login choice (the switcher, or a `?lang=` visit, while
  // signed out), set SYNCHRONOUSLY by `setLanguage` below — before it even
  // calls `i18n.changeLanguage`, let alone awaits it. `setPreLoginLanguage`
  // only writes `localStorage`'s pre-login key once that `changeLanguage`
  // call actually resolves (which, for Spanish, waits on a lazy chunk), so a
  // brand-new account signing in on this same, still-signed-out browser
  // WHILE that chunk is still loading would otherwise see nothing there yet
  // and fall through to the browser's own language for its backfill — even
  // though this visitor had already, explicitly, chosen something else
  // (todos/248 P3). `syncLanguagePreference`'s backfill branch below reads
  // this ref first, ahead of `getPreLoginLanguage()`. Paired with a
  // `generation` so a call that finishes after being superseded by a
  // second, later switch clears only its OWN entry, never a newer one's.
  const pendingExplicitChoiceRef = useRef<{ language: SupportedLanguage; generation: number } | null>(null);

  // Applies the account's saved language once a user is known. When there is
  // no saved preference yet (a first sign-in, or an account created before
  // this field existed), this persists a resolved language so future
  // sign-ins, emails, and AI responses know it too. Fire-and-forget by
  // design: it must never block or delay setting the signed-in user, since
  // `ProtectedRoute`/`PublicRoute` key off that state synchronously.
  const syncLanguagePreference = useCallback((userData: User, generation: number) => {
    if (languageGenerationRef.current !== generation) return; // superseded — see languageGenerationRef

    if (isSupportedLanguage(userData.preferredLanguage)) {
      const target = userData.preferredLanguage;
      // Compare against `resolvedLanguage` (what's actually active), not
      // `language` (i18next sets `language` only once a `changeLanguage`
      // call's own load resolves — see `node_modules/i18next`'s
      // `changeLanguage`/`setLngProps` — so while a lazy Spanish chunk is
      // still loading, `language` still reads the OLD value even though a
      // switch is already under way). Also re-issue the call whenever one is
      // already pending (`isLanguageChangingTo`, i18next's own in-flight
      // marker — not in its public TS surface, hence the narrow cast below):
      // i18next only applies a `changeLanguage` call if ITS OWN target is
      // still the one in progress once its load resolves, so calling
      // `changeLanguage(target)` here always supersedes a pending call to a
      // DIFFERENT language, even while `resolvedLanguage` still reads the
      // old value too (todos/248 P2 — signing in while a pre-login Spanish
      // switch was still loading otherwise left the screen in Spanish
      // against an English account, because comparing only `i18n.language`
      // skipped this call entirely).
      if (i18n.resolvedLanguage !== target || pendingI18nLanguageChange()) {
        void i18n.changeLanguage(target);
      }
      return;
    }

    // The backfill value is deliberately NOT `i18n.language`: on a shared
    // device, the language currently active could be nothing more than the
    // previous user's sign-out carry-over (`getLastDisplayLanguage`, applied
    // for *display* only — see `lib/i18n/detect.ts`), which must never leak
    // into this account's saved preference. An explicit pre-login choice
    // still mid-flight (`pendingExplicitChoiceRef` — its own
    // `setPreLoginLanguage` write hasn't landed yet, e.g. its Spanish chunk
    // is still loading) counts as this visitor's own choice just as much as
    // one already in `localStorage`, and takes priority over it (todos/248
    // P3); otherwise fall back to the browser's own languages, then `en`.
    const resolved: SupportedLanguage =
      pendingExplicitChoiceRef.current?.language ?? getPreLoginLanguage() ?? detectBrowserLanguage();

    // Apply on screen too — not just persisted. Without this, a visitor whose
    // browser language differed from whatever i18next happened to initialize
    // with (e.g. the sign-out carry-over from a previous account on this
    // device) would have the right language saved to their new account but
    // see the wrong one until their next reload. Same `resolvedLanguage` +
    // pending-switch comparison as above, for the same reason.
    if (i18n.resolvedLanguage !== resolved || pendingI18nLanguageChange()) {
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

  // Applies a freshly fetched `/me` response (shared by `loadUser` and
  // `refreshUser` below — the two places a `/me` GET can resolve late). If a
  // newer language change (an explicit `setLanguage`, a backfill PUT, or
  // another `/me`) has landed since THIS request started —
  // `languageGenerationRef` has moved past the `generation` it captured
  // before its own fetch — this response's OWN `preferredLanguage` field is
  // therefore stale: the server hadn't seen, or this client hasn't yet
  // applied, that newer change when this response was produced. Every OTHER
  // field is still the latest (name, subscription status, etc. — nothing
  // else races this way), so keep those, but keep the CURRENT, newer
  // `preferredLanguage` rather than reverting it (todos/248 P3 — a slow
  // `/me` landing after a newer language PUT was overwriting the just-saved
  // preference back to its old value, even though `syncLanguagePreference`
  // already refused to re-apply it on SCREEN thanks to the same generation
  // check).
  const applyUserResponse = useCallback(
    (data: User, generation: number) => {
      if (languageGenerationRef.current !== generation) {
        setUser((prev) => {
          const merged = prev ? { ...data, preferredLanguage: prev.preferredLanguage } : data;
          setStoredUser(JSON.stringify(merged));
          return merged;
        });
        return;
      }
      setUser(data);
      setStoredUser(JSON.stringify(data));
      syncLanguagePreference(data, generation);
    },
    [syncLanguagePreference]
  );

  const loadUser = useCallback(async () => {
    // Captured before the `/me` request starts: if `logout()` runs while it
    // is in flight, the token is gone by the time the response lands, and
    // this stale response must never resurrect a signed-in-looking user with
    // no token (todos/247) by writing `setUser`/`setStoredUser` after the
    // fact. Checked again below, after the `await`, alongside the existing
    // `languageGenerationRef` check (which guards the LANGUAGE side of the
    // same race; this guards the USER/session side).
    const tokenAtStart = getToken();
    if (!tokenAtStart) {
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

      // Verify with API.
      const generation = languageGenerationRef.current; // captured before the request starts — see languageGenerationRef
      const response = await getCurrentUser();
      if (getToken() !== tokenAtStart) {
        // Signed out (or a different session started) while this request
        // was in flight — `logout()` already cleared the user and token;
        // applying this response now would undo that.
        return;
      }
      if (response.success && response.data) {
        applyUserResponse(response.data, generation);
      } else {
        removeToken();
        setUser(null);
      }
    } catch {
      if (getToken() !== tokenAtStart) return;
      removeToken();
      setUser(null);
    } finally {
      setIsLoading(false);
    }
  }, [applyUserResponse]);

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
    const generation = ++languageGenerationRef.current; // supersedes any pre-sign-in setLanguage call — see languageGenerationRef
    // The sign-out carry-over's job (showing the login page in the right
    // language) is done now that someone has actually signed in — clear it
    // so it can't later be mistaken for this or a future visitor's own
    // choice.
    clearLastDisplayLanguage();
    syncLanguagePreference(userData, generation);
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
    const generation = languageGenerationRef.current; // same latest-wins capture as loadUser — see languageGenerationRef
    // Same session-side guard as `loadUser`: if `logout()` runs before this
    // resolves, the token is gone and this response must not resurrect a
    // signed-in-looking user with no token (todos/247).
    const tokenAtStart = getToken();
    try {
      const response = await getCurrentUser();
      if (getToken() !== tokenAtStart) return;
      if (response.success && response.data) {
        applyUserResponse(response.data, generation);
      }
    } catch {
      // Keep the existing user on a transient failure.
    }
  };

  const logout = () => {
    languageGenerationRef.current++; // bump first, before anything else — see languageGenerationRef (todos/247)

    // Keep whatever language was active — it's still correct, it just has
    // nowhere to live once the account's saved preference is gone. Recorded
    // as the sign-out *display* carry-over (never the explicit pre-login
    // key — see `lib/i18n/detect.ts`) so the login page stays in that
    // language instead of falling back to the browser, without that choice
    // ever being mistaken for the next signed-in account's own preference on
    // a shared device.
    setLastDisplayLanguage(isSupportedLanguage(i18n.language) ? i18n.language : DEFAULT_LANGUAGE);
    // Defensive hygiene, not a normal write path (signing in already clears
    // this — see `persistSession` — and `useLanguageQueryParam` no longer
    // writes it while signed in): makes sure this account's departing
    // session never leaves a pre-login value behind for the NEXT, possibly
    // anonymous, visitor on a shared device to be backfilled with.
    clearPreLoginLanguage();
    removeToken();
    setUser(null);
    setMfaPendingToken(null);
  };

  const setLanguage = async (language: SupportedLanguage) => {
    const generation = ++languageGenerationRef.current; // claims this attempt — see languageGenerationRef
    // `getToken()`, not the closure `user` state: this function's `user`
    // is whatever was current when ITS OWN render captured it, which can
    // be stale by the time this runs — a caller that grabbed `setLanguage`
    // from context before a sign-in/sign-out re-render still holds that
    // older closure. `getToken()` reads the actual signed-in state live,
    // at the instant each check runs, so a sign-in/sign-out that happens
    // while `changeLanguage` below is in flight is never missed.
    const signedOutAtCallTime = !getToken();
    if (signedOutAtCallTime) {
      // Set synchronously, before `changeLanguage` below is even called —
      // see `pendingExplicitChoiceRef`'s declaration for why a backfill
      // racing this call needs to see the choice this early.
      pendingExplicitChoiceRef.current = { language, generation };
    }
    await i18n.changeLanguage(language);
    if (signedOutAtCallTime && pendingExplicitChoiceRef.current?.generation === generation) {
      // This attempt's own window has closed — `changeLanguage` has settled
      // (whether it actually took or reverted, checked next). Clear only
      // OUR entry: a second, later `setLanguage` call made before this one
      // finished already overwrote it with its own, newer choice, which
      // must survive this cleanup.
      pendingExplicitChoiceRef.current = null;
    }

    if (languageGenerationRef.current !== generation) {
      // superseded while `changeLanguage` was resolving — see languageGenerationRef
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

    if (!getToken()) {
      // Re-checked live (not the closure `user`, and not the
      // `signedOutAtCallTime` snapshot above) — a sign-in that completed
      // while `changeLanguage` was resolving must be written to the
      // account via the PUT below, not stranded in the pre-login key.
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
        // superseded while the PUT was in flight — see languageGenerationRef
        return { success: true };
      }
      if (response.success && response.data) {
        setUser(response.data);
        setStoredUser(JSON.stringify(response.data));
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
