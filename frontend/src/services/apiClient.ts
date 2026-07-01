import axios, { type AxiosInstance, type AxiosResponse, type InternalAxiosRequestConfig } from 'axios';

const API_BASE_URL = (() => {
  const url = import.meta.env.VITE_API_BASE_URL;
  if (!url) {
    if (import.meta.env.PROD) {
      throw new Error('VITE_API_BASE_URL is not set. This environment variable is required for production builds.');
    }
    return 'http://localhost:5000/api/v1';
  }
  return url;
})();

interface RefreshResponse {
  success: boolean;
  data?: { token: string };
}

class ApiClient {
  private client: AxiosInstance;
  private isRefreshing = false;
  private refreshSubscribers: Array<(token: string) => void> = [];
  private failSubscribers: Array<() => void> = [];
  // Access token lives only in memory — never written to localStorage.
  // Survives navigation but not page refresh (handled by refresh token on startup).
  private accessToken: string | null = null;

  constructor() {
    this.client = axios.create({
      baseURL: API_BASE_URL,
      headers: {
        'Content-Type': 'application/json',
      },
      timeout: 30000,
      withCredentials: true,
    });

    // Request interceptor to add auth token
    this.client.interceptors.request.use(
      (config) => {
        if (this.accessToken) {
          config.headers.Authorization = `Bearer ${this.accessToken}`;
        }
        return config;
      },
      (error) => Promise.reject(error)
    );

    // Response interceptor: silently refresh on 401, retry, redirect only if refresh fails
    this.client.interceptors.response.use(
      (response) => response,
      async (error) => {
        const originalRequest: InternalAxiosRequestConfig & { _retry?: boolean } = error.config;

        // Don't retry refresh or login endpoints to avoid infinite loops
        const isAuthEndpoint =
          originalRequest?.url?.includes('/auth/refresh') ||
          originalRequest?.url?.includes('/auth/login') ||
          originalRequest?.url?.includes('/auth/register');

        if (error.response?.status === 401 && !originalRequest._retry && !isAuthEndpoint) {
          originalRequest._retry = true;

          if (!localStorage.getItem('hasSession')) {
            this.redirectToLogin();
            return Promise.reject(error);
          }

          if (this.isRefreshing) {
            // Queue this request until the ongoing refresh completes
            return new Promise((resolve, reject) => {
              this.refreshSubscribers.push((newToken) => {
                originalRequest.headers.Authorization = `Bearer ${newToken}`;
                resolve(this.client(originalRequest));
              });
              this.failSubscribers.push(() => reject(error));
            });
          }

          this.isRefreshing = true;

          try {
            // Refresh token is sent automatically via HttpOnly cookie
            const response = await axios.post<RefreshResponse>(
              `${API_BASE_URL}/auth/refresh`,
              {},
              { headers: { 'Content-Type': 'application/json' }, withCredentials: true }
            );

            const data = response.data?.data;
            if (!data?.token) throw new Error('No token in refresh response');

            this.accessToken = data.token;

            this.refreshSubscribers.forEach((cb) => cb(data.token));
            this.refreshSubscribers = [];
            this.failSubscribers = [];
            this.isRefreshing = false;

            originalRequest.headers.Authorization = `Bearer ${data.token}`;
            return this.client(originalRequest);
          } catch {
            this.failSubscribers.forEach((cb) => cb());
            this.refreshSubscribers = [];
            this.failSubscribers = [];
            this.isRefreshing = false;
            this.redirectToLogin();
            return Promise.reject(error);
          }
        }

        return Promise.reject(error);
      }
    );
  }

  private redirectToLogin() {
    this.accessToken = null;
    localStorage.removeItem('hasSession');
    window.location.href = '/login';
  }

  async get<T>(url: string, config?: any): Promise<T> {
    const response: AxiosResponse<T> = await this.client.get(url, config);
    return response.data;
  }

  async post<T>(url: string, data?: unknown, config?: any): Promise<T> {
    const response: AxiosResponse<T> = await this.client.post(url, data, config);
    return response.data;
  }

  async put<T>(url: string, data?: unknown, config?: any): Promise<T> {
    const response: AxiosResponse<T> = await this.client.put(url, data, config);
    return response.data;
  }

  async delete<T>(url: string, config?: any): Promise<T> {
    const response: AxiosResponse<T> = await this.client.delete(url, config);
    return response.data;
  }

  /** Absolute API base URL — for building direct links to public asset endpoints (e.g. quote images). */
  get baseUrl(): string {
    return API_BASE_URL;
  }

  setAuthToken(token: string): void {
    this.accessToken = token;
    localStorage.setItem('hasSession', '1');
  }

  clearTokens(): void {
    this.accessToken = null;
    localStorage.removeItem('hasSession');
  }
}

export const apiClient = new ApiClient();
export default apiClient;
