import { createContext, useCallback, useEffect, useState } from 'react';
import type { User, LoginRequest, RegisterRequest, UpdateProfileRequest } from '@/types/api';
import {
  login as loginApi,
  register as registerApi,
  getCurrentUser,
  updateProfile as updateProfileApi,
} from '../api/auth-api';
import { getToken, setToken, removeToken, setStoredUser, getStoredUser } from '@/lib/auth';

interface AuthContextType {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (data: LoginRequest) => Promise<{ success: boolean; error?: string }>;
  register: (data: RegisterRequest) => Promise<{ success: boolean; error?: string }>;
  updateProfile: (data: UpdateProfileRequest) => Promise<{ success: boolean; error?: string }>;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

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

  const login = async (data: LoginRequest) => {
    try {
      const response = await loginApi(data);
      if (response.success && response.data) {
        setToken(response.data.token);
        setUser(response.data.user);
        setStoredUser(JSON.stringify(response.data.user));
        return { success: true };
      }
      return { success: false, error: response.message || 'Login failed' };
    } catch (error) {
      return { success: false, error: 'An error occurred during login' };
    }
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
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: !!user,
        isLoading,
        login,
        register,
        updateProfile,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
