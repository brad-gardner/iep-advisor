/**
 * Explicit allow-list for `text-brand-slate-400` in `src/**\/*.{ts,tsx}`.
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
 * anywhere in `src/**\/*.{ts,tsx}` that isn't accounted for here. Entries are
 * keyed by `file` + the exact trimmed source line (`text`), not by line
 * number: a line-number key breaks (with false "unlisted"/"stale" pairs) the
 * moment an unrelated edit inserts or removes a line earlier in the same
 * file. Content-keying also closes a blind spot a line-number key has: a
 * single line that legitimately holds one allowed occurrence (e.g. a
 * variant-branching ternary) could regress a *second*, disallowed occurrence
 * onto that same line and still show as "one allow-listed hit" — but the
 * moment the line's text changes to add that occurrence, it no longer
 * matches the allow-listed text and shows up as unlisted. `count` is the
 * total number of `text-brand-slate-400` occurrences expected across every
 * line in the file that has this exact trimmed text (usually 1 — see
 * `sidebar.tsx` below for a file with several identical lines) — the guard
 * also fails if the real count drifts from this, so an unexpected new
 * duplicate (or the disappearance of one this list is counting on) can't
 * pass silently just because the text itself is already allow-listed
 * elsewhere.
 *
 * To fix a failure: if the new occurrence is real body/secondary text on a
 * light surface, change it to `text-brand-slate-500` instead of allow-listing
 * it. Only add an entry here for a genuinely dark-surface use or a non-text
 * decorative glyph, with a one-line reason.
 */
export interface ContrastAllowlistEntry {
  file: string;
  /** Exact trimmed text of the source line(s) carrying this occurrence. */
  text: string;
  /** Expected total occurrences of the pattern across every line in `file` with this exact trimmed text. */
  count: number;
  reason: string;
}

const DARK_SIDEBAR = 'Dark sidebar surface (bg-brand-slate-800): slate-400 measures 4.53:1 here — raising to slate-500 (2.77:1) would regress contrast.';
const ICON_ONLY_TRIGGER = 'Icon-only button trigger (wraps a single lucide icon, no visible text) — meets the 3:1 non-text bar; not body text.';
const STANDALONE_ICON = 'Standalone sized lucide icon (decorative glyph, aria-hidden where applicable) — the 3:1 non-text bar applies, not the 4.5:1 text bar.';

export const CONTRAST_ALLOWLIST: ContrastAllowlistEntry[] = [
  // Sidebar — every use lives on the dark rail (bg-brand-slate-800). Eight nav-link ternaries
  // share this exact "unselected" branch text, so one entry with count: 8 covers all of them —
  // a line-number key would instead need eight separate, individually fragile entries.
  {
    file: 'src/components/layouts/sidebar.tsx',
    text: ": 'text-brand-slate-400 hover:text-brand-slate-200 hover:bg-brand-slate-700'",
    count: 8,
    reason: DARK_SIDEBAR,
  },
  {
    file: 'src/components/layouts/sidebar.tsx',
    text: 'className="flex items-center gap-3 px-3 py-2.5 rounded-button text-sm text-brand-slate-400 hover:text-brand-slate-200 hover:bg-brand-slate-700 transition-colors"',
    count: 1,
    reason: DARK_SIDEBAR,
  },
  {
    file: 'src/components/layouts/sidebar.tsx',
    text: 'className="flex items-center gap-2 text-sm text-brand-slate-400 hover:text-brand-slate-200 transition-colors"',
    count: 1,
    reason: DARK_SIDEBAR,
  },
  {
    file: 'src/components/layouts/sidebar.tsx',
    text: 'className="absolute top-4 right-4 text-brand-slate-400 hover:text-white"',
    count: 1,
    reason: DARK_SIDEBAR,
  },

  // Bell trigger lives in the dark top bar (hover:bg-brand-slate-700), same
  // surface as the sidebar. The dropdown it opens is white — those uses were
  // changed to slate-500 and are not in this list.
  {
    file: 'src/features/notifications/components/notification-bell.tsx',
    text: 'className="relative flex h-9 w-9 items-center justify-center rounded-button text-brand-slate-400 transition-colors hover:bg-brand-slate-700 hover:text-brand-slate-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-500"',
    count: 1,
    reason: 'Bell trigger sits in the dark top bar (hover:bg-brand-slate-700), same surface as the sidebar — slate-400 is correct here.',
  },

  // Icon-only close/menu triggers on white — glyph, not text.
  {
    file: 'src/components/ui/drawer.tsx',
    text: 'className="-mr-1.5 -mt-0.5 rounded-button p-1 text-brand-slate-400 transition-colors hover:bg-brand-slate-50 hover:text-brand-slate-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-500"',
    count: 1,
    reason: ICON_ONLY_TRIGGER,
  },
  {
    file: 'src/components/ui/modal.tsx',
    text: 'className="-mr-1.5 -mt-0.5 rounded-button p-1 text-brand-slate-400 transition-colors hover:bg-brand-slate-50 hover:text-brand-slate-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-500"',
    count: 1,
    reason: ICON_ONLY_TRIGGER,
  },
  {
    file: 'src/components/ui/menu.tsx',
    text: "'flex h-9 w-9 items-center justify-center rounded-button text-brand-slate-400 transition-colors hover:bg-brand-slate-50 hover:text-brand-slate-600',",
    count: 1,
    reason: ICON_ONLY_TRIGGER,
  },

  // Logo tagline: variant="dark" renders on bg-brand-slate-800 (sidebar,
  // auth-layout); only that branch stays at slate-400. The default/light
  // branch on the same line was raised to slate-500 — the trimmed text below
  // includes both branches, so a regression that also flips the light branch
  // back to slate-400 changes this line's text and shows up as unlisted
  // rather than hiding behind this entry.
  {
    file: 'src/components/ui/logo.tsx',
    text: "<p className={`${s.tagline} font-semibold uppercase tracking-[0.12em] ${variant === 'dark' ? 'text-brand-slate-400' : 'text-brand-slate-500'} mt-0.5`}>",
    count: 1,
    reason:
      "Tagline's dark-variant branch renders on bg-brand-slate-800 (sidebar/auth-layout) — the light branch on the same line was raised to slate-500.",
  },

  // Standalone decorative icons (sized, mostly aria-hidden) — non-text 3:1 bar.
  {
    file: 'src/features/iep-comparison/components/comparison-view.tsx',
    text: '<ArrowRight className="w-4 h-4 text-brand-slate-400" strokeWidth={1.8} />',
    count: 1,
    reason: STANDALONE_ICON,
  },
  {
    file: 'src/features/iep-documents/components/iep-upload.tsx',
    text: '<Upload className="w-5 h-5 text-brand-slate-400" strokeWidth={1.8} aria-hidden="true" />',
    count: 1,
    reason: STANDALONE_ICON,
  },
  {
    file: 'src/features/subscription/components/subscription-cancel-page.tsx',
    text: '<XCircle className="w-6 h-6 text-brand-slate-400" strokeWidth={1.8} aria-hidden="true" />',
    count: 1,
    reason: STANDALONE_ICON,
  },
  {
    file: 'src/features/etr-documents/components/etr-section-card.tsx',
    text: '<ChevronUp className="w-4 h-4 text-brand-slate-400 shrink-0" strokeWidth={1.8} aria-hidden="true" />',
    count: 1,
    reason: STANDALONE_ICON,
  },
  {
    file: 'src/features/etr-documents/components/etr-section-card.tsx',
    text: '<ChevronDown className="w-4 h-4 text-brand-slate-400 shrink-0" strokeWidth={1.8} aria-hidden="true" />',
    count: 1,
    reason: STANDALONE_ICON,
  },
  {
    file: 'src/features/etr-documents/components/etr-upload.tsx',
    text: '<Upload className="w-5 h-5 text-brand-slate-400" strokeWidth={1.8} aria-hidden="true" />',
    count: 1,
    reason: STANDALONE_ICON,
  },
  {
    file: 'src/features/advocate/components/thread-list.tsx',
    text: '<MessageSquare className="mt-0.5 h-4 w-4 shrink-0 text-brand-slate-400" aria-hidden="true" />',
    count: 1,
    reason: STANDALONE_ICON,
  },
  {
    file: 'src/features/admin/components/admin-dashboard-page.tsx',
    text: '<Icon size={14} strokeWidth={1.8} className="text-brand-slate-400" />',
    count: 1,
    reason: 'Decorative stat-row icon (lucide component via a dynamic `Icon` prop) next to a separately-colored text label — non-text 3:1 bar.',
  },
  {
    file: 'src/features/progress-reports/components/progress-report-upload.tsx',
    text: 'className="w-5 h-5 text-brand-slate-400"',
    count: 1,
    reason: STANDALONE_ICON,
  },
  {
    file: 'src/features/admin/components/admin-users-page.tsx',
    text: 'className="absolute left-3 top-1/2 -translate-y-1/2 text-brand-slate-400"',
    count: 1,
    reason: 'Decorative search icon positioned inside an input — non-text 3:1 bar, not a text node.',
  },
  {
    file: 'src/features/iep-comparison/components/goal-diff-card.tsx',
    text: '<span className="text-brand-slate-400" aria-hidden="true">',
    count: 1,
    reason: 'aria-hidden decorative arrow glyph (&rarr;) between an old/new value pair — non-text 3:1 bar.',
  },

  // Icon-only buttons elsewhere in the app, same pattern as drawer/modal/menu.
  {
    file: 'src/features/advocate/components/state-hint.tsx',
    text: 'className="shrink-0 rounded p-0.5 text-brand-slate-400 hover:bg-brand-amber-100 hover:text-brand-slate-600 focus:outline-none focus:ring-1 focus:ring-brand-teal-500"',
    count: 1,
    reason: ICON_ONLY_TRIGGER,
  },
  {
    file: 'src/features/document-authoring/components/chat/chat-panel.tsx',
    text: 'className="rounded-button p-1 text-brand-slate-400 hover:bg-brand-slate-100 hover:text-brand-slate-600"',
    count: 1,
    reason: ICON_ONLY_TRIGGER,
  },
  {
    file: 'src/features/meeting-prep/components/checklist-item-row.tsx',
    text: 'className="flex-shrink-0 p-1 rounded hover:bg-brand-slate-100 text-brand-slate-400 hover:text-brand-slate-600 transition-colors"',
    count: 1,
    reason: ICON_ONLY_TRIGGER,
  },
  {
    file: 'src/features/meeting-prep/components/parent-question-row.tsx',
    text: "'shrink-0 rounded p-1 text-brand-slate-400 transition-colors hover:bg-brand-slate-100 hover:text-brand-slate-600 focus:outline-none focus:ring-1 focus:ring-brand-teal-500 disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:bg-transparent',",
    count: 1,
    reason: ICON_ONLY_TRIGGER,
  },

  // Diff-indicator swatch: the '=' (unchanged) case renders an empty string,
  // so no text ever passes through this class — it colors an empty badge.
  {
    file: 'src/features/iep-comparison/components/section-diff.tsx',
    text: "'=': 'text-brand-slate-400 bg-brand-slate-50',",
    count: 1,
    reason: "Diff indicator swatch for the '=' case renders an empty string (no visible text) — decorative badge color, not text.",
  },
];
