import { describe, expect, it } from 'vitest';

// Guards the "Staff and admin namespaces" rule in `docs/i18n/README.md`: a
// component reachable from an EAGER (non-lazy) import path must never call
// `useTranslation('<staff ns>')` or reach one via `t('<staff ns>:key')`,
// because that namespace's ENGLISH only exists once its own lazy route
// chunk evaluates (`registerEnglishNamespace` in `index.ts`) — reaching it
// eagerly throws (English falling through to the Spanish-only backend) or,
// worse, shows raw `ns:key` text to an English parent/family user. See the
// phase 5 review finding this test was added for (`AuthoredPdfDownload`
// used the staff-only `document-authoring` namespace while rendering on the
// eager `ParentAuthoredVersionPage`).
//
// Approach: walk the STATIC (non-dynamic) import graph from the app's real
// entry point, `main.tsx`. `React.lazy(() => import('...'))` and the
// barrels' own `import('@/app/lazy-routes/<area>-routes')` calls are
// DYNAMIC `import(...)` expressions, which the regex below deliberately
// does not follow — so the walk never crosses into a lazy chunk, exactly
// mirroring what Vite/Rollup would actually code-split. Everything the walk
// DOES reach is, definitionally, bundled into (or reachable from) the main
// chunk, i.e. "eager" for this purpose. This is a real (if regex-based)
// graph walk rather than a hand-maintained folder allowlist, because a
// folder-based exclude list breaks for an area like `features/meetings`,
// which mixes an eager parent component (`rsvp-button-group.tsx`, namespace
// `meetings`) with many staff-only ones (namespace `meetings-staff`).
//
// Known limitation: a dynamic `import('@/some/other/lazy/page')` unrelated
// to the three staff/admin barrels also stops the walk there, so a
// non-staff lazy page's own staff-namespace misuse (if any) would not be
// caught by this test — out of scope; see the P1 finding this guards.
//
// Second known limitation, and the reason for the small ALLOWLIST below:
// this walk is file-granular, not call-site-granular. A shared utility file
// can legitimately mix an eager-safe export with a staff-only one (e.g.
// `lib/meeting-labels.ts`'s `meetingTypeLabel`/`meetingStatusLabel`, called
// from genuinely eager parent features, alongside `meetingDecisionOutcomeLabel`,
// called only from staff ones) — the file is "eagerly reachable" as a
// whole, so it reads as a violation even though no eager caller actually
// invokes the staff-only export. Splitting every such file into two would
// be the "proper" fix; the allowlist is the pragmatic one, same spirit as
// the task that asked for this guard. Add an entry here ONLY for a file
// you've confirmed is in this exact shape — a genuinely eager component
// calling a staff namespace directly must never be added here.
const ALLOWLIST = new Set<string>([
  // `meetingTypeLabel`/`meetingStatusLabel` (common, eager) live alongside
  // `meetingDecisionOutcomeLabel` (`meetings-staff`, staff-only) in this one
  // file; every caller of the latter is itself a staff component (see the
  // file's own doc comment on `meetingDecisionOutcomeLabel`).
  '/src/lib/meeting-labels.ts',
]);

const ENTRY_POINT = '/src/main.tsx';

// Raw text of every staff namespace's English file, just to derive the
// namespace NAME from its filename — same `namespaceOf` idea as
// `locale-parity.test.ts`, kept independent of that file on purpose (this
// test must keep working even if that one changes shape).
// `import.meta.glob` requires a literal string argument, so the pattern
// can't be hoisted to a named constant.
const staffLocaleModules = import.meta.glob('/src/locales/en/staff/*.json', { eager: true }) as Record<
  string,
  unknown
>;
const staffNamespaces = Object.keys(staffLocaleModules).map((path) => {
  const match = /\/([^/]+)\.json$/.exec(path);
  if (!match) throw new Error(`Could not derive a namespace from ${path}`);
  return match[1];
});

// Raw source text of every non-test TS/TSX file in the app, keyed by its
// absolute-from-root module path (e.g. `/src/app/routes.tsx`) — the same
// shape `import.meta.glob` uses elsewhere in this suite, just with
// `query: '?raw'` so the files are read as text, never executed.
const allSourceModules = import.meta.glob('/src/**/*.{ts,tsx}', {
  eager: true,
  query: '?raw',
  import: 'default',
}) as Record<string, string>;

const sourceByPath = new Map<string, string>();
for (const [path, text] of Object.entries(allSourceModules)) {
  if (path.endsWith('.test.ts') || path.endsWith('.test.tsx') || path.endsWith('.d.ts')) continue;
  sourceByPath.set(path, text);
}

/** Static `import ... from '...'`, `export ... from '...'`, and bare `import '...'`
 *  specifiers only — NOT `import(...)` (dynamic), which is the lazy-chunk boundary. */
function staticImportSpecifiers(source: string): string[] {
  const specifiers: string[] = [];
  const fromImportRe = /(?:^|\n)[^\n]*?\b(?:import|export)\b[^(;\n]*?\bfrom\s*['"]([^'"]+)['"]/g;
  const sideEffectImportRe = /(?:^|\n)\s*import\s*['"]([^'"]+)['"]/g;
  for (const re of [fromImportRe, sideEffectImportRe]) {
    let match: RegExpExecArray | null;
    while ((match = re.exec(source))) {
      specifiers.push(match[1]);
    }
  }
  return specifiers;
}

function resolveSpecifier(fromPath: string, specifier: string): string | null {
  let target: string;
  if (specifier.startsWith('@/')) {
    target = `/src/${specifier.slice(2)}`;
  } else if (specifier.startsWith('./') || specifier.startsWith('../')) {
    const fromDir = fromPath.slice(0, fromPath.lastIndexOf('/'));
    const parts = `${fromDir}/${specifier}`.split('/');
    const resolved: string[] = [];
    for (const part of parts) {
      if (part === '' || part === '.') continue;
      if (part === '..') resolved.pop();
      else resolved.push(part);
    }
    target = `/${resolved.join('/')}`;
  } else {
    return null; // bare/package import — outside our source graph
  }
  const candidates = [target, `${target}.ts`, `${target}.tsx`, `${target}/index.ts`, `${target}/index.tsx`];
  return candidates.find((candidate) => sourceByPath.has(candidate)) ?? null;
}

/** BFS from `main.tsx` over static imports only. Every file this reaches is
 *  "eager" — bundled into (or always reachable from) the main chunk. */
function eagerlyReachableFiles(): Set<string> {
  const visited = new Set<string>();
  const queue: string[] = [ENTRY_POINT];
  while (queue.length > 0) {
    const current = queue.pop()!;
    if (visited.has(current)) continue;
    visited.add(current);
    const source = sourceByPath.get(current);
    if (source === undefined) continue;
    for (const specifier of staticImportSpecifiers(source)) {
      const resolved = resolveSpecifier(current, specifier);
      if (resolved && !visited.has(resolved)) queue.push(resolved);
    }
  }
  return visited;
}

// Strips `//...` and `/* ... */` comments before scanning for a namespace
// reference. Without this, this project's own convention of documenting a
// helper's key path in a doc comment (e.g. "translated via `obligations:kind.*`")
// — which this file's own edits add plenty of — reads as a code violation
// just as readily as a real `t('obligations:kind...')` call would. Simple
// and line-oriented on purpose (per-line `//` stripping can, in theory,
// truncate a real call that shares a line with a `//`-containing string
// literal, which would under-report rather than over-report a violation —
// no such line exists in this codebase today, and this is a guard test, not
// a security boundary).
function stripComments(source: string): string {
  return source
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .split('\n')
    .map((line) => line.replace(/\/\/.*$/, ''))
    .join('\n');
}

function usesStaffNamespace(source: string, ns: string): boolean {
  const code = stripComments(source);
  const escaped = ns.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  // useTranslation('ns') / useTranslation(['ns', 'common']) / useTranslation(['common', 'ns'])
  const hookCalls = code.match(/useTranslation\(([^)]*)\)/g) ?? [];
  const inHookCall = hookCalls.some((call) => new RegExp(`['"]${escaped}['"]`).test(call));
  if (inHookCall) return true;
  // Cross-namespace reference: t('ns:key...') / i18n.t('ns:key...') / i18n.t(`ns:key.${x}`)
  return new RegExp(`['"\`]${escaped}:`).test(code);
}

describe('staff namespace boundary', () => {
  it('found at least one staff namespace to guard (sanity check the glob itself)', () => {
    expect(staffNamespaces.length).toBeGreaterThan(0);
  });

  it('never calls useTranslation/t with a staff-only namespace from eagerly-reachable source', () => {
    const eager = eagerlyReachableFiles();
    const violations: string[] = [];

    for (const path of eager) {
      if (ALLOWLIST.has(path)) continue;
      const source = sourceByPath.get(path);
      if (!source) continue;
      for (const ns of staffNamespaces) {
        if (usesStaffNamespace(source, ns)) {
          violations.push(`${path} uses staff namespace '${ns}'`);
        }
      }
    }

    expect(violations).toEqual([]);
  });
});
