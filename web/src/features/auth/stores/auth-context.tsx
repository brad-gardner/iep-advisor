import { createContext, useCallback, useEffect, useState } from 'react';
import type { User, LoginRequest, RegisterRequest, UpdateProfileRequest } from '@/types/api';
import {
  login as loginApi,
  register as registerApi,
  getCurrentUser,
  updateProfile as updateProfileApi,
} from '../api/auth-api';
import { getToken, setToken, removeToken, setStoredUser, getStoredUser } from '@/lib/auth';

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
  updateProfile: (data: UpdateProfileRequest) => Promise<{ success: boolean; error?: string }>;
  completeMfaLogin: (token: string, user: User) => void;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [mfaPendingToken, setMfaPendingToken] = useState<string | null>(null);

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
  }, []);

  useEffect(() => {
    loadUser();
  }, [loadUser]);

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
          setToken(response.data.token);
          setUser(response.data.user);
          setStoredUser(JSON.stringify(response.data.user));
          return { success: true };
        }
      }
      return { success: false, error: response.message || 'Login failed' };
    } catch (error) {
      return { success: false, error: 'An error occurred during login' };
    }
  };

  const completeMfaLogin = (token: string, userData: User) => {
    setToken(token);
    setUser(userData);
    setStoredUser(JSON.stringify(userData));
    setMfaPendingToken(null);
  };

  const register = async (data: RegisterRequest) => {
    try {
      const response = await registerApi(data);
      if (response.success) {
        return { success: true };
      }
      return { success: false, error: response.message || 'Registration failed' };
    } catch (error) {
      return { success: false, error: 'An error occurred during registration' };
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
      return { success: false, error: response.message || 'Update failed' };
    } catch {
      return { success: false, error: 'An error occurred updating profile' };
    }
  };

  const logout = () => {
    removeToken();
    setUser(null);
    setMfaPendingToken(null);
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
        updateProfile,
        completeMfaLogin,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
