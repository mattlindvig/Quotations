import axios, { type AxiosInstance, type AxiosResponse, type InternalAxiosRequestConfig } from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api/v1';

interface RefreshResponse {
  success: boolean;
  data?: { token: string; refreshToken: string };
}

class ApiClient {
  private client: AxiosInstance;
  private isRefreshing = false;
  private refreshSubscribers: Array<(token: string) => void> = [];
  private failSubscribers: Array<() => void> = [];

  constructor() {
    this.client = axios.create({
      baseURL: API_BASE_URL,
      headers: {
        'Content-Type': 'application/json',
      },
      timeout: 30000,
    });

    // Request interceptor to add auth token
    this.client.interceptors.request.use(
      (config) => {
        const token = localStorage.getItem('authToken');
        if (token) {
          config.headers.Authorization = `Bearer ${token}`;
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

          const refreshToken = localStorage.getItem('refreshToken');
          if (!refreshToken) {
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
            const response = await axios.post<RefreshResponse>(
              `${API_BASE_URL}/auth/refresh`,
              { refreshToken },
              { headers: { 'Content-Type': 'application/json' } }
            );

            const data = response.data?.data;
            if (!data?.token) throw new Error('No token in refresh response');

            localStorage.setItem('authToken', data.token);
            localStorage.setItem('refreshToken', data.refreshToken);

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
    localStorage.removeItem('authToken');
    localStorage.removeItem('refreshToken');
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

  setAuthToken(token: string): void {
    localStorage.setItem('authToken', token);
  }

  setRefreshToken(token: string): void {
    localStorage.setItem('refreshToken', token);
  }

  clearTokens(): void {
    localStorage.removeItem('authToken');
    localStorage.removeItem('refreshToken');
  }

  /** @deprecated Use clearTokens() */
  clearAuthToken(): void {
    this.clearTokens();
  }
}

export const apiClient = new ApiClient();
export default apiClient;
