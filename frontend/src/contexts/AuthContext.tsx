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

// Decode a JWT payload without verifying the signature.
// Verification still happens on the backend for every protected request.
function decodeJwtPayload(token: string): Record<string, unknown> | null {
  try {
    const base64Payload = token.split('.')[1];
    const json = atob(base64Payload.replace(/-/g, '+').replace(/_/g, '/'));
    return JSON.parse(json);
  } catch {
    return null;
  }
}

function userFromPayload(payload: Record<string, unknown>): User | null {
  const exp = typeof payload.exp === 'number' ? payload.exp : 0;
  if (exp * 1000 < Date.now()) return null;

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

export const AuthProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem('authToken');
    if (token) {
      const payload = decodeJwtPayload(token);
      const restored = payload ? userFromPayload(payload) : null;
      if (restored) {
        setUser(restored);
      } else {
        localStorage.removeItem('authToken');
      }
    }
    setIsLoading(false);
  }, []);

  const login = async (username: string, password: string) => {
    const response = await apiClient.post<ApiResponse<AuthResponse>>(
      '/auth/login',
      { username, password }
    );

    if (!response.success || !response.data) {
      throw new Error('Login failed');
    }

    apiClient.setAuthToken(response.data.token);
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

    apiClient.setAuthToken(response.data.token);
    setUser(response.data.user);
  };

  const logout = () => {
    apiClient.clearAuthToken();
    setUser(null);
  };

  const hasRole = (role: string) => user?.roles.includes(role) ?? false;

  return (
    <AuthContext.Provider
      value={{ user, isAuthenticated: !!user, isLoading, login, register, logout, hasRole }}
    >
      {children}
    </AuthContext.Provider>
  );
};
