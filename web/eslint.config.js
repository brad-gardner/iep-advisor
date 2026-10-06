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
  // i18n lint ratchet (multilingual plan, phase 1): `no-literal-string` is
  // turned on per converted folder, not globally, so the baseline stays
  // green while later phases convert the rest of `src`. It becomes global
  // in the plan's last phase. Test files are excluded everywhere — they
  // stay English by design (see `test/setup.ts`).
  {
    files: [
      'src/features/auth/**/*.{ts,tsx}',
      'src/components/layouts/**/*.{ts,tsx}',
      'src/components/ui/**/*.{ts,tsx}',
      'src/lib/i18n/**/*.{ts,tsx}',
      'src/features/staff-invites/pages/staff-accept-invite-page.tsx',
      'src/features/staff-invites/components/accept-invite-form.tsx',
      'src/features/student/components/student-accept-invite-page.tsx',
    ],
    ignores: ['**/*.test.{ts,tsx}'],
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
