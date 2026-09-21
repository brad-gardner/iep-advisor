import { describe, expect, it } from 'vitest';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, extname, dirname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { CONTRAST_ALLOWLIST } from './contrast-allowlist';

// Guards the app-wide contrast pass (2026-09-20): `text-brand-slate-400` is
// 3.27:1 on white and 3.04:1 on `slate-50` — below the 4.5:1 AA floor for
// body text. Every text use was raised to `text-brand-slate-500`; this test
// fails if `text-brand-slate-400` reappears anywhere in `src/**/*.{ts,tsx}`
// outside the documented, reasoned exceptions in `contrast-allowlist.ts`
// (dark surfaces and decorative/non-text glyphs).
//
// Entries are matched by `file` + the exact trimmed source line, not by line
// number, so an unrelated line inserted or removed earlier in the same file
// can't produce a false "unlisted" (the shifted real occurrence) plus a false
// "stale" (the allow-list entry now pointing at the wrong line) pair. This
// also closes a blind spot a line-number key has: a single already-allowed
// line (e.g. a variant-branching ternary) could regress a *second* pattern
// occurrence onto that same line and still read as "one allow-listed hit" —
// content-keying instead sees the line's text change and reports it as
// unlisted. The occurrence count is checked too, so an unexpected new
// duplicate of an already-allow-listed line (or the disappearance of one the
// allow-list is counting on) can't hide just because the text itself is
// already recognized elsewhere in the file.

const TEST_DIR = dirname(fileURLToPath(import.meta.url));
const SRC_ROOT = join(TEST_DIR, '..');
const PATTERN = 'text-brand-slate-400';
const PATTERN_RE = new RegExp(PATTERN, 'g');

// This guard's own two files reference the pattern in comments and as the
// `PATTERN` string literal itself — excluded so the guard never self-matches.
const SELF_FILES = new Set(['src/test/contrast-allowlist.ts', 'src/test/contrast-guard.test.ts']);

function walk(dir: string): string[] {
  const out: string[] = [];
  for (const name of readdirSync(dir)) {
    const p = join(dir, name);
    if (statSync(p).isDirectory()) {
      out.push(...walk(p));
    } else if (extname(p) === '.tsx' || extname(p) === '.ts') {
      out.push(p);
    }
  }
  return out;
}

interface Hit {
  file: string;
  line: number;
  text: string;
  /** Occurrences of the pattern on this one line. */
  count: number;
}

function findOccurrences(): Hit[] {
  const hits: Hit[] = [];
  for (const absPath of walk(SRC_ROOT)) {
    const relPath = join('src', relative(SRC_ROOT, absPath)).split('\\').join('/');
    if (SELF_FILES.has(relPath)) continue;
    const lines = readFileSync(absPath, 'utf8').split('\n');
    lines.forEach((line, i) => {
      const matches = line.match(PATTERN_RE);
      if (matches && matches.length > 0) {
        hits.push({ file: relPath, line: i + 1, text: line.trim(), count: matches.length });
      }
    });
  }
  return hits;
}

const entryKey = (file: string, text: string) => `${file}\u0000${text}`;

describe('contrast guard — text-brand-slate-400', () => {
  it('only appears at documented, allow-listed locations, the expected number of times', () => {
    const hits = findOccurrences();
    const allowedByKey = new Map(CONTRAST_ALLOWLIST.map((e) => [entryKey(e.file, e.text), e]));

    const unlisted = hits.filter((h) => !allowedByKey.has(entryKey(h.file, h.text)));
    if (unlisted.length > 0) {
      const detail = unlisted.map((o) => `  ${o.file}:${o.line}\n    ${o.text}`).join('\n');
      throw new Error(
        `Found ${unlisted.length} unlisted use(s) of ${PATTERN} in src/**/*.{ts,tsx}:\n${detail}\n\n` +
          `${PATTERN} is 3.27:1 on white / 3.04:1 on slate-50 — below the 4.5:1 AA floor for body ` +
          `text. To fix: change body/secondary text on a light surface to text-brand-slate-500. ` +
          `If this is genuinely a dark-surface use or a decorative/non-text glyph (icon-only ` +
          `trigger, aria-hidden decorative icon), add it to CONTRAST_ALLOWLIST in ` +
          `src/test/contrast-allowlist.ts (keyed by file + exact trimmed line text) with a ` +
          `one-line reason instead. If this line already looks allow-listed, check whether the ` +
          `line's text has changed — e.g. a second occurrence added to an already-allowed line.`,
      );
    }

    // Aggregate actual occurrences per allow-listed (file, text) key. A line with the pattern
    // twice contributes 2, and several identical lines in one file (e.g. sidebar.tsx's repeated
    // nav-link ternary) sum together under their single shared entry.
    const actualCount = new Map<string, number>();
    for (const h of hits) {
      const k = entryKey(h.file, h.text);
      actualCount.set(k, (actualCount.get(k) ?? 0) + h.count);
    }

    // Covers both a stale entry (an allow-listed line that no longer exists — actual 0) and a
    // drifted one (an unexpected new duplicate, or a removed one, changing the real count away
    // from what the allow-list records).
    const drifted: string[] = [];
    for (const [k, entry] of allowedByKey) {
      const actual = actualCount.get(k) ?? 0;
      if (actual !== entry.count) {
        drifted.push(`  ${entry.file}: expected ${entry.count}, found ${actual} — "${entry.text}"`);
      }
    }
    expect(
      drifted,
      `Allow-list occurrence counts drifted from reality — update contrast-allowlist.ts:\n${drifted.join('\n')}`,
    ).toEqual([]);
  });
});
