import React, { createContext, useContext, useState, useEffect } from 'react';
import type { ReactNode } from 'react';
import { apiClient } from '../services/apiClient';
import type { ApiResponse } from '../types/quotation';

interface User {
  id: string;
  username: string;
  displayName: string;
  roles: string[];
}

interface AuthResponse {
  token: string;
  refreshToken: string;
  user: User;
}

interface AuthContextType {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (username: string, password: string) => Promise<void>;
  register: (username: string, email: string, password: string, displayName?: string) => Promise<void>;
  logout: () => void;
  hasRole: (role: string) => boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within an AuthProvider');
  return context;
};

function decodeJwtPayload(token: string): Record<string, unknown> | null {
  try {
    const base64Payload = token.split('.')[1];
    const json = atob(base64Payload.replace(/-/g, '+').replace(/_/g, '/'));
    return JSON.parse(json);
  } catch {
    return null;
  }
}

function isTokenExpired(token: string): boolean {
  const payload = decodeJwtPayload(token);
  if (!payload) return true;
  const exp = typeof payload.exp === 'number' ? payload.exp : 0;
  return exp * 1000 < Date.now();
}

function userFromPayload(payload: Record<string, unknown>): User {
  const roles = Array.isArray(payload.role)
    ? (payload.role as string[])
    : typeof payload.role === 'string'
    ? [payload.role]
    : [];

  return {
    id: (payload.sub as string) ?? '',
    username: (payload.unique_name as string) ?? '',
    displayName: (payload.displayName as string) ?? (payload.unique_name as string) ?? '',
    roles,
  };
}

function applyAuthResponse(data: AuthResponse) {
  apiClient.setAuthToken(data.token);
  apiClient.setRefreshToken(data.refreshToken);
}

export const AuthProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const initAuth = async () => {
      const accessToken = localStorage.getItem('authToken');
      const refreshToken = localStorage.getItem('refreshToken');

      if (accessToken && !isTokenExpired(accessToken)) {
        // Access token is still valid — restore user from it
        const payload = decodeJwtPayload(accessToken);
        if (payload) setUser(userFromPayload(payload));
      } else if (refreshToken) {
        // Access token missing or expired — silently refresh
        try {
          const response = await apiClient.post<ApiResponse<AuthResponse>>(
            '/auth/refresh',
            { refreshToken }
          );
          if (response.success && response.data) {
            applyAuthResponse(response.data);
            const payload = decodeJwtPayload(response.data.token);
            if (payload) setUser(userFromPayload(payload));
          } else {
            apiClient.clearTokens();
          }
        } catch {
          apiClient.clearTokens();
        }
      }

      setIsLoading(false);
    };

    initAuth();
  }, []);

  const login = async (username: string, password: string) => {
    const response = await apiClient.post<ApiResponse<AuthResponse>>(
      '/auth/login',
      { username, password }
    );

    if (!response.success || !response.data) {
      throw new Error('Login failed');
    }

    applyAuthResponse(response.data);
    setUser(response.data.user);
  };

  const register = async (
    username: string,
    email: string,
    password: string,
    displayName?: string
  ) => {
    const response = await apiClient.post<ApiResponse<AuthResponse>>(
      '/auth/register',
      { username, email, password, displayName }
    );

    if (!response.success || !response.data) {
      throw new Error('Registration failed');
    }

    applyAuthResponse(response.data);
    setUser(response.data.user);
  };

  const logout = () => {
    const refreshToken = localStorage.getItem('refreshToken');
    if (refreshToken) {
      // Fire-and-forget: revoke the refresh token server-side
      apiClient.post('/auth/logout', { refreshToken }).catch(() => {});
    }
    apiClient.clearTokens();
    setUser(null);
  };

  const hasRole = (role: string) =>
    user?.roles.some((r) => r.toLowerCase() === role.toLowerCase()) ?? false;

  return (
    <AuthContext.Provider
      value={{ user, isAuthenticated: !!user, isLoading, login, register, logout, hasRole }}
    >
      {children}
    </AuthContext.Provider>
  );
};
