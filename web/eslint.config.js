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
      'src/features/children/**/*.{ts,tsx}',
      'src/features/home/**/*.{ts,tsx}',
      'src/features/onboarding/**/*.{ts,tsx}',
      'src/features/notifications/**/*.{ts,tsx}',
      'src/features/subscription/**/*.{ts,tsx}',
      'src/features/child-links/**/*.{ts,tsx}',
      'src/features/sharing/**/*.{ts,tsx}',
      'src/features/knowledge-base/**/*.{ts,tsx}',
      // Not a phase-2 feature (meetings converts in phase 3) — only this one
      // component is in scope, because the parent-home "next meeting"/
      // "upcoming meeting" cards (phase 2) render it (phase 2 review).
      'src/features/meetings/**/*.{ts,tsx}',
      // Phase 3 — parent documents and AI.
      'src/features/iep-documents/**/*.{ts,tsx}',
      'src/features/etr-documents/**/*.{ts,tsx}',
      'src/features/analysis/**/*.{ts,tsx}',
      'src/features/iep-comparison/**/*.{ts,tsx}',
      'src/features/iep-versions/**/*.{ts,tsx}',
      'src/features/progress-reports/**/*.{ts,tsx}',
      'src/features/goals/**/*.{ts,tsx}',
      'src/features/advocacy-goals/**/*.{ts,tsx}',
      'src/features/meeting-prep/**/*.{ts,tsx}',
      'src/features/journal/**/*.{ts,tsx}',
      'src/features/advocate/**/*.{ts,tsx}',
      'src/features/shared-drafts/**/*.{ts,tsx}',
      'src/features/draft-sharing/**/*.{ts,tsx}',
      'src/features/student/**/*.{ts,tsx}',

            'src/lib/section-type-label.ts',
      'src/lib/document-status-label.ts',
      'src/lib/meeting-labels.ts',
      // Phase 5 — staff and school.
      'src/features/educator/**/*.{ts,tsx}',
      'src/features/calendar/**/*.{ts,tsx}',
      'src/features/meeting-brief/**/*.{ts,tsx}',
      'src/features/document-authoring/**/*.{ts,tsx}',
      'src/features/evaluation/**/*.{ts,tsx}',
      'src/features/obligations/**/*.{ts,tsx}',
      'src/features/family-contact/**/*.{ts,tsx}',
      'src/features/contributions/**/*.{ts,tsx}',
      'src/lib/evaluation-case-label.ts',
      'src/lib/obligation-label.ts',
      'src/lib/family-contact-label.ts',
      'src/lib/contribution-label.ts',
      'src/app/lazy-routes/**/*.{ts,tsx}',
    ],
    ignores: [
      '**/*.test.{ts,tsx}',
      // Out of scope for this phase — iep-documents/etr-documents convert
      // these in Phase 3, so they stay English (and un-ratcheted) for now.
      'src/features/children/components/child-ieps-tab.tsx',
      'src/features/children/components/child-etrs-tab.tsx',
    ],
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
