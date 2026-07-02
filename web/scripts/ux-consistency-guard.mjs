#!/usr/bin/env node
/**
 * UX consistency guard — asserts the MIGRATED pilot surface has no raw patterns
 * that the design-system primitives replaced. Unmigrated surfaces (parent,
 * student, platform-admin) are intentionally NOT checked yet — they migrate in
 * the fast-follow plan; their app-wide counts are printed for reference only.
 *
 * Run: `npm run guard:ux`  (exits 1 if any pilot-dir violation is found)
 */
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, extname } from 'node:path';

const ROOT = new URL('../src', import.meta.url).pathname;

// Surfaces migrated onto the design system (Plan A). The `ui/` primitives are
// excluded — the canonical Spinner lives there and legitimately uses animate-spin.
const PILOT_DIRS = [
  'features/district-admin',
  'features/staff-invites',
  'features/educator',
];

// Baseline counts recorded when the guard was written (app-wide, pre-migration
// reference from the audit). The fast-follow plan drives these toward zero.
const APP_WIDE_BASELINE = { spinners: 58, rawButtons: 55, reds: 60 };

const CHECKS = [
  { key: 'spinners', label: 'raw animate-spin (use <Spinner>)', re: /animate-spin/ },
  { key: 'rawButtons', label: 'raw <button> (use <Button>)', re: /<button[\s>]/ },
  {
    key: 'reds',
    label: 'raw red-*/brand-red (use brand-danger)',
    re: /(?:bg|text|border|ring)-red-[0-9]|brand-red/,
  },
];

function walk(dir) {
  const out = [];
  for (const name of readdirSync(dir)) {
    const p = join(dir, name);
    if (statSync(p).isDirectory()) out.push(...walk(p));
    else if (['.tsx', '.ts'].includes(extname(p)) && !p.endsWith('.test.tsx') && !p.endsWith('.test.ts'))
      out.push(p);
  }
  return out;
}

let violations = 0;
console.log('UX consistency guard — pilot surface\n');

for (const { key, label, re } of CHECKS) {
  const hits = [];
  for (const rel of PILOT_DIRS) {
    for (const file of walk(join(ROOT, rel))) {
      const lines = readFileSync(file, 'utf8').split('\n');
      lines.forEach((line, i) => {
        if (re.test(line)) hits.push(`${file.replace(ROOT, 'src')}:${i + 1}`);
      });
    }
  }
  const status = hits.length === 0 ? '✓' : '✗';
  console.log(`${status} ${label}: ${hits.length} in pilot dirs`);
  hits.forEach((h) => console.log(`    ${h}`));
  if (hits.length > 0) violations += hits.length;
}

console.log(
  `\nApp-wide baseline (reference, not enforced): ${APP_WIDE_BASELINE.spinners} spinners / ` +
    `${APP_WIDE_BASELINE.rawButtons} raw buttons / ${APP_WIDE_BASELINE.reds} reds — ` +
    `the fast-follow plan migrates the remaining surfaces.`
);

if (violations > 0) {
  console.error(`\n✗ ${violations} pilot-surface violation(s) — route through the design-system primitives.`);
  process.exit(1);
}
console.log('\n✓ Pilot surface is clean.');
