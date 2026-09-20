/**
 * Explicit allow-list for `text-brand-slate-400` in `src/**\/*.tsx`.
 *
 * `text-brand-slate-400` (#7F9292) is 3.27:1 on white and 3.04:1 on the
 * `slate-50` page — below the 4.5:1 AA floor for body text. The app-wide
 * contrast pass (2026-09-20) raised every *text* use to `text-brand-slate-500`
 * (5.33:1 on white, 4.96:1 on slate-50) and left this list of documented
 * exceptions, each of which is either non-text (a decorative glyph, subject
 * to the 3:1 non-text bar) or sits on a dark surface where slate-400 already
 * passes (4.53:1 on `slate-800`) and slate-500 would regress it (2.77:1).
 *
 * `contrast-guard.test.ts` fails the build if `text-brand-slate-400` appears
 * anywhere in `src/**\/*.tsx` at a `file:line` not listed here. To fix a
 * failure: if the new occurrence is real body/secondary text on a light
 * surface, change it to `text-brand-slate-500` instead of allow-listing it.
 * Only add an entry here for a genuinely dark-surface use or a non-text
 * decorative glyph, with a one-line reason.
 */
export interface ContrastAllowlistEntry {
  file: string;
  line: number;
  reason: string;
}

const DARK_SIDEBAR = 'Dark sidebar surface (bg-brand-slate-800): slate-400 measures 4.53:1 here — raising to slate-500 (2.77:1) would regress contrast.';
const ICON_ONLY_TRIGGER = 'Icon-only button trigger (wraps a single lucide icon, no visible text) — meets the 3:1 non-text bar; not body text.';
const STANDALONE_ICON = 'Standalone sized lucide icon (decorative glyph, aria-hidden where applicable) — the 3:1 non-text bar applies, not the 4.5:1 text bar.';

export const CONTRAST_ALLOWLIST: ContrastAllowlistEntry[] = [
  // Sidebar — every use lives on the dark rail (bg-brand-slate-800).
  { file: 'src/components/layouts/sidebar.tsx', line: 131, reason: DARK_SIDEBAR },
  { file: 'src/components/layouts/sidebar.tsx', line: 142, reason: DARK_SIDEBAR },
  { file: 'src/components/layouts/sidebar.tsx', line: 180, reason: DARK_SIDEBAR },
  { file: 'src/components/layouts/sidebar.tsx', line: 204, reason: DARK_SIDEBAR },
  { file: 'src/components/layouts/sidebar.tsx', line: 217, reason: DARK_SIDEBAR },
  { file: 'src/components/layouts/sidebar.tsx', line: 230, reason: DARK_SIDEBAR },
  { file: 'src/components/layouts/sidebar.tsx', line: 243, reason: DARK_SIDEBAR },
  { file: 'src/components/layouts/sidebar.tsx', line: 256, reason: DARK_SIDEBAR },
  { file: 'src/components/layouts/sidebar.tsx', line: 269, reason: DARK_SIDEBAR },
  { file: 'src/components/layouts/sidebar.tsx', line: 297, reason: DARK_SIDEBAR },
  { file: 'src/components/layouts/sidebar.tsx', line: 343, reason: DARK_SIDEBAR },

  // Bell trigger lives in the dark top bar (hover:bg-brand-slate-700), same
  // surface as the sidebar. The dropdown it opens is white — those two lines
  // (199, 201) were changed to slate-500 and are not in this list.
  {
    file: 'src/features/notifications/components/notification-bell.tsx',
    line: 170,
    reason: 'Bell trigger sits in the dark top bar (hover:bg-brand-slate-700), same surface as the sidebar — slate-400 is correct here.',
  },

  // Icon-only close/menu triggers on white — glyph, not text.
  { file: 'src/components/ui/drawer.tsx', line: 82, reason: ICON_ONLY_TRIGGER },
  { file: 'src/components/ui/modal.tsx', line: 106, reason: ICON_ONLY_TRIGGER },
  { file: 'src/components/ui/menu.tsx', line: 189, reason: ICON_ONLY_TRIGGER },

  // Logo tagline: variant="dark" renders on bg-brand-slate-800 (sidebar,
  // auth-layout); only that branch stays at slate-400. The default/light
  // branch on the same line was raised to slate-500.
  {
    file: 'src/components/ui/logo.tsx',
    line: 30,
    reason: "Tagline's dark-variant branch renders on bg-brand-slate-800 (sidebar/auth-layout) — the light branch on the same line was raised to slate-500.",
  },

  // Standalone decorative icons (sized, mostly aria-hidden) — non-text 3:1 bar.
  { file: 'src/features/iep-comparison/components/comparison-view.tsx', line: 75, reason: STANDALONE_ICON },
  { file: 'src/features/iep-documents/components/iep-upload.tsx', line: 107, reason: STANDALONE_ICON },
  { file: 'src/features/subscription/components/subscription-cancel-page.tsx', line: 15, reason: STANDALONE_ICON },
  { file: 'src/features/etr-documents/components/etr-section-card.tsx', line: 60, reason: STANDALONE_ICON },
  { file: 'src/features/etr-documents/components/etr-section-card.tsx', line: 62, reason: STANDALONE_ICON },
  { file: 'src/features/etr-documents/components/etr-upload.tsx', line: 116, reason: STANDALONE_ICON },
  { file: 'src/features/advocate/components/thread-list.tsx', line: 113, reason: STANDALONE_ICON },
  {
    file: 'src/features/admin/components/admin-dashboard-page.tsx',
    line: 395,
    reason: 'Decorative stat-row icon (lucide component via a dynamic `Icon` prop) next to a separately-colored text label — non-text 3:1 bar.',
  },
  { file: 'src/features/progress-reports/components/progress-report-upload.tsx', line: 101, reason: STANDALONE_ICON },
  {
    file: 'src/features/admin/components/admin-users-page.tsx',
    line: 124,
    reason: 'Decorative search icon positioned inside an input — non-text 3:1 bar, not a text node.',
  },
  {
    file: 'src/features/iep-comparison/components/goal-diff-card.tsx',
    line: 77,
    reason: 'aria-hidden decorative arrow glyph (&rarr;) between an old/new value pair — non-text 3:1 bar.',
  },

  // Icon-only buttons elsewhere in the app, same pattern as drawer/modal/menu.
  {
    file: 'src/features/advocate/components/state-hint.tsx',
    line: 57,
    reason: ICON_ONLY_TRIGGER,
  },
  {
    file: 'src/features/document-authoring/components/chat/chat-panel.tsx',
    line: 41,
    reason: ICON_ONLY_TRIGGER,
  },
  {
    file: 'src/features/meeting-prep/components/checklist-item-row.tsx',
    line: 50,
    reason: ICON_ONLY_TRIGGER,
  },
  {
    file: 'src/features/meeting-prep/components/parent-question-row.tsx',
    line: 40,
    reason: ICON_ONLY_TRIGGER,
  },

  // Diff-indicator swatch: the '=' (unchanged) case renders an empty string,
  // so no text ever passes through this class — it colors an empty badge.
  {
    file: 'src/features/iep-comparison/components/section-diff.tsx',
    line: 29,
    reason: "Diff indicator swatch for the '=' case renders an empty string (no visible text) — decorative badge color, not text.",
  },
];
