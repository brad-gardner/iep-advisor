import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import i18next from 'eslint-plugin-i18next'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
    },
  },
  // i18n lint rule (multilingual plan, phase 7 — final): `no-literal-string`
  // is GLOBAL across non-test `web/src` now, replacing the phase-by-phase
  // per-folder list that used to live here (see `docs/i18n/README.md`'s "The
  // lint rule" for the history). Test files are excluded everywhere — they
  // stay English by design (see `test/setup.ts`) — along with `src/test/**`,
  // whose helpers intentionally hold literal English strings (fixtures,
  // test-only copy, not app-facing UI text).
  {
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['**/*.test.{ts,tsx}', 'src/test/**'],
    plugins: { i18next },
    rules: {
      'i18next/no-literal-string': [
        'error',
        {
          mode: 'jsx-only',
          'jsx-attributes': {
            include: ['aria-label', 'title', 'placeholder', 'label', 'alt'],
          },
        },
      ],
    },
  },
])
