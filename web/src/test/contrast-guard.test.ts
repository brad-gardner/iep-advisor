import { describe, expect, it } from 'vitest';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, extname, dirname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { CONTRAST_ALLOWLIST } from './contrast-allowlist';

// Guards the app-wide contrast pass (2026-09-20): `text-brand-slate-400` is
// 3.27:1 on white and 3.04:1 on `slate-50` — below the 4.5:1 AA floor for
// body text. Every text use was raised to `text-brand-slate-500`; this test
// fails if `text-brand-slate-400` reappears anywhere in `src/**/*.tsx`
// outside the documented, reasoned exceptions in `contrast-allowlist.ts`
// (dark surfaces and decorative/non-text glyphs).

const TEST_DIR = dirname(fileURLToPath(import.meta.url));
const SRC_ROOT = join(TEST_DIR, '..');
const PATTERN = 'text-brand-slate-400';

function walk(dir: string): string[] {
  const out: string[] = [];
  for (const name of readdirSync(dir)) {
    const p = join(dir, name);
    if (statSync(p).isDirectory()) {
      out.push(...walk(p));
    } else if (extname(p) === '.tsx') {
      out.push(p);
    }
  }
  return out;
}

function findOccurrences(): { file: string; line: number; text: string }[] {
  const hits: { file: string; line: number; text: string }[] = [];
  for (const absPath of walk(SRC_ROOT)) {
    const relPath = join('src', relative(SRC_ROOT, absPath)).split('\\').join('/');
    const lines = readFileSync(absPath, 'utf8').split('\n');
    lines.forEach((line, i) => {
      if (line.includes(PATTERN)) {
        hits.push({ file: relPath, line: i + 1, text: line.trim() });
      }
    });
  }
  return hits;
}

describe('contrast guard — text-brand-slate-400', () => {
  it('only appears at documented, allow-listed locations', () => {
    const occurrences = findOccurrences();
    const allowed = new Set(CONTRAST_ALLOWLIST.map((e) => `${e.file}:${e.line}`));

    const unlisted = occurrences.filter((o) => !allowed.has(`${o.file}:${o.line}`));

    if (unlisted.length > 0) {
      const detail = unlisted.map((o) => `  ${o.file}:${o.line}\n    ${o.text}`).join('\n');
      throw new Error(
        `Found ${unlisted.length} unlisted use(s) of ${PATTERN} in src/**/*.tsx:\n${detail}\n\n` +
          `${PATTERN} is 3.27:1 on white / 3.04:1 on slate-50 — below the 4.5:1 AA floor for body ` +
          `text. To fix: change body/secondary text on a light surface to text-brand-slate-500. ` +
          `If this is genuinely a dark-surface use or a decorative/non-text glyph (icon-only ` +
          `trigger, aria-hidden decorative icon), add it to CONTRAST_ALLOWLIST in ` +
          `src/test/contrast-allowlist.ts with a one-line reason instead.`,
      );
    }

    // Sanity check that the allow-list itself doesn't drift ahead of reality
    // (an entry pointing at a line that no longer contains the pattern).
    const actual = new Set(occurrences.map((o) => `${o.file}:${o.line}`));
    const stale = CONTRAST_ALLOWLIST.filter((e) => !actual.has(`${e.file}:${e.line}`));
    expect(
      stale,
      `Stale allow-list entries no longer match a ${PATTERN} occurrence — remove them from contrast-allowlist.ts:\n` +
        stale.map((e) => `  ${e.file}:${e.line}`).join('\n'),
    ).toEqual([]);
  });
});
