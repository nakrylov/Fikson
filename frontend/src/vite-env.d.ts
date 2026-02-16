/// <reference types="vite/client" />

interface ImportMetaEnv {
  /**
   * Optional API base URL.
   * Examples:
   * - http://localhost:5198
   * - https://api.example.com
   *
   * If omitted, the app calls relative `/api/*` from the current origin.
   */
  readonly VITE_API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

