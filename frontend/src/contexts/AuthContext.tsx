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

// ASP.NET's JWT handler may use either the short form or the full Microsoft claim URL
const MS_ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
const MS_NAME_CLAIM = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name';
const MS_SUB_CLAIM  = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';

function userFromPayload(payload: Record<string, unknown>): User {
  const roleClaim = payload['role'] ?? payload[MS_ROLE_CLAIM];
  const roles = Array.isArray(roleClaim)
    ? (roleClaim as string[])
    : typeof roleClaim === 'string'
    ? [roleClaim]
    : [];

  return {
    id: ((payload.sub ?? payload[MS_SUB_CLAIM]) as string) ?? '',
    username: ((payload.unique_name ?? payload[MS_NAME_CLAIM]) as string) ?? '',
    displayName: (payload.displayName as string) ?? ((payload.unique_name ?? payload[MS_NAME_CLAIM]) as string) ?? '',
    roles,
  };
}

function applyAuthResponse(data: AuthResponse) {
  apiClient.setAuthToken(data.token);
  apiClient.setRefreshToken(data.refreshToken);
}

function getInitialUser(): User | null {
  const accessToken = localStorage.getItem('authToken');
  if (accessToken && !isTokenExpired(accessToken)) {
    const payload = decodeJwtPayload(accessToken);
    if (payload) return userFromPayload(payload);
  }
  return null;
}

function needsRefresh(): boolean {
  const accessToken = localStorage.getItem('authToken');
  if (accessToken && !isTokenExpired(accessToken)) return false;
  return !!localStorage.getItem('refreshToken');
}

export const AuthProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<User | null>(getInitialUser);
  const [isLoading, setIsLoading] = useState(needsRefresh);

  useEffect(() => {
    if (!isLoading) return;

    const initAuth = async () => {
      const refreshToken = localStorage.getItem('refreshToken');
      if (refreshToken) {
        try {
          const response = await apiClient.post<ApiResponse<AuthResponse>>(
            '/auth/refresh',
            { refreshToken }
          );
          if (response.success && response.data) {
            applyAuthResponse(response.data);
            const newPayload = decodeJwtPayload(response.data.token);
            setUser(newPayload ? userFromPayload(newPayload) : response.data.user);
          } else {
            apiClient.clearTokens();
            setUser(null);
          }
        } catch {
          apiClient.clearTokens();
          setUser(null);
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
