/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_SENTRY_DSN?: string;
  /** Full URL of the public trust & privacy page (defaults to the marketing site). */
  readonly VITE_MARKETING_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
