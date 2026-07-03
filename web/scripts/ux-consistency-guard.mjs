#!/usr/bin/env node
/**
 * UX consistency guard — the whole authenticated app has now been migrated onto
 * the design-system primitives. This enforces the invariants the primitives
 * give FULL coverage for:
 *   - no hand-rolled `animate-spin` (use <Spinner>/<Skeleton>)
 *   - no raw Tailwind `red-*` / `brand-red` (use the `brand-danger` scale)
 *   - no native `window.confirm()` (use <ConfirmDialog>)
 * across `src/features`, `src/app`, and `src/components/layouts`. The `ui/`
 * primitives are excluded (the canonical Spinner legitimately animates; the
 * danger scale is defined there).
 *
 * Raw `<button>` is reported for INFORMATION only, not enforced: tab bars,
 * menus, switches, radios, disclosure triggers, and inline text-links are
 * legitimately native `<button>`s with no `Button`-primitive equivalent.
 *
 * Run: `npm run guard:ux`  (exits 1 only on a spinner/red violation)
 */
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, extname } from 'node:path';

const ROOT = new URL('../src', import.meta.url).pathname;
const DIRS = ['features', 'app', 'components/layouts'];

const ENFORCED = [
  { key: 'spinners', label: 'raw animate-spin (use <Spinner>/<Skeleton>)', re: /animate-spin/ },
  {
    key: 'reds',
    label: 'raw red-*/brand-red (use brand-danger)',
    re: /(?:bg|text|border|ring|from|to|via)-red-[0-9]|brand-red\b/,
  },
  {
    key: 'confirm',
    // Native confirm()/window.confirm() — use <ConfirmDialog>. `\bconfirm\(`
    // matches the bare/`window.` call but not ConfirmDialog, onConfirm,
    // confirmLabel, confirmDelete, etc. (no word boundary inside those).
    label: 'native window.confirm() (use <ConfirmDialog>)',
    re: /\bconfirm\s*\(/,
  },
];
const INFO = [{ key: 'rawButtons', label: 'raw <button> (native tabs/menus/switches/links — not enforced)', re: /<button[\s>\n]/ }];

function walk(dir) {
  const out = [];
  for (const name of readdirSync(dir)) {
    const p = join(dir, name);
    if (statSync(p).isDirectory()) {
      if (p.endsWith('/components/ui')) continue; // primitives live here
      out.push(...walk(p));
    } else if (['.tsx', '.ts'].includes(extname(p)) && !/\.test\.(ts|tsx)$/.test(p)) {
      out.push(p);
    }
  }
  return out;
}

const files = DIRS.flatMap((d) => walk(join(ROOT, d)));
let violations = 0;
console.log('UX consistency guard — app-wide (design-system migration)\n');

for (const { label, re } of ENFORCED) {
  const hits = [];
  for (const f of files) {
    readFileSync(f, 'utf8').split('\n').forEach((line, i) => {
      if (re.test(line)) hits.push(`${f.replace(ROOT, 'src')}:${i + 1}`);
    });
  }
  console.log(`${hits.length === 0 ? '✓' : '✗'} ${label}: ${hits.length}`);
  hits.forEach((h) => console.log(`    ${h}`));
  violations += hits.length;
}

for (const { label, re } of INFO) {
  let count = 0;
  for (const f of files) count += (readFileSync(f, 'utf8').match(new RegExp(re, 'g')) || []).length;
  console.log(`• ${label}: ${count}`);
}

if (violations > 0) {
  console.error(`\n✗ ${violations} violation(s) — route through the design-system primitives.`);
  process.exit(1);
}
console.log('\n✓ No raw spinners or reds — design system holds.');
