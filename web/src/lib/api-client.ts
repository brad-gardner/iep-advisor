import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';
import * as Sentry from '@sentry/react';
import { getToken, removeToken } from './auth';
import i18n from './i18n';

export const apiClient = axios.create({
  baseURL: '',
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor to add auth token
apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = getToken();
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    // Lets RequestLocalization pick the active UI language for this request
    // (e.g. a localized validation message) even before the account's saved
    // preference is persisted — the server still prefers a signed-in user's
    // saved preference over this header (design doc, Architecture → API).
    config.headers['Accept-Language'] = i18n.language || 'en';
    return config;
  },
  (error) => Promise.reject(error)
);

// Response interceptor to handle auth errors and log to Sentry
apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    if (error.response?.status === 401) {
      removeToken();
      window.location.href = '/login';
    }

    // Log 5xx server errors to Sentry (not 4xx — those are expected validation errors)
    if (error.response && error.response.status >= 500) {
      Sentry.captureException(error, {
        extra: {
          url: error.config?.url,
          method: error.config?.method,
          status: error.response.status,
        },
      });
    }

    return Promise.reject(error);
  }
);
