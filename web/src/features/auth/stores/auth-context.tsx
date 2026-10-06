import { createContext, useCallback, useEffect, useState } from 'react';
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
import { isSupportedLanguage, setPreLoginLanguage, DEFAULT_LANGUAGE, type SupportedLanguage } from '@/lib/i18n/detect';

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

  // Applies the account's saved language once a user is known. When there is
  // no saved preference yet (a first sign-in, or an account created before
  // this field existed), this persists whatever language the session
  // resolved to (the pre-login choice or browser detection) so future
  // sign-ins, emails, and AI responses know it too. Fire-and-forget by
  // design: it must never block or delay setting the signed-in user, since
  // `ProtectedRoute`/`PublicRoute` key off that state synchronously.
  const syncLanguagePreference = useCallback((userData: User) => {
    if (isSupportedLanguage(userData.preferredLanguage)) {
      if (i18n.language !== userData.preferredLanguage) {
        void i18n.changeLanguage(userData.preferredLanguage);
      }
      return;
    }

    const resolved: SupportedLanguage = isSupportedLanguage(i18n.language)
      ? i18n.language
      : DEFAULT_LANGUAGE;

    void updateProfileApi({ preferredLanguage: resolved })
      .then((response) => {
        if (!response.success || !response.data) return;
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
      });
  }, []);

  const loadUser = useCallback(async () => {
    const token = getToken();
    if (!token) {
      setIsLoading(false);
      return;
    }

    try {
      // Try to get user from storage first
      const storedUser = getStoredUser();
      if (storedUser) {
        setUser(JSON.parse(storedUser));
      }

      // Verify with API
      const response = await getCurrentUser();
      if (response.success && response.data) {
        setUser(response.data);
        setStoredUser(JSON.stringify(response.data));
        syncLanguagePreference(response.data);
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
    syncLanguagePreference(userData);
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
    try {
      const response = await getCurrentUser();
      if (response.success && response.data) {
        setUser(response.data);
        setStoredUser(JSON.stringify(response.data));
      }
    } catch {
      // Keep the existing user on a transient failure.
    }
  };

  const logout = () => {
    removeToken();
    setUser(null);
    setMfaPendingToken(null);
  };

  const setLanguage = async (language: SupportedLanguage) => {
    await i18n.changeLanguage(language);

    if (!user) {
      // No account to save it to yet — remembered for this browser until sign-in.
      setPreLoginLanguage(language);
      return { success: true };
    }

    try {
      const response = await updateProfileApi({ preferredLanguage: language });
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
