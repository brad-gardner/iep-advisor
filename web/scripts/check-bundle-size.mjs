// Main-chunk gzip budget for the i18n plan (docs/plans/2026-10-06-001-*).
// English locales are bundled eagerly, so every converted page adds to the
// main chunk; this fails the build above the budget and warns near it.
// Run after `npm run build`.
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { gzipSync } from 'node:zlib';

const FAIL_KB = 418;
const WARN_KB = 405;
const dist = join(import.meta.dirname, '..', 'dist');
// The entry chunk is the module script index.html loads — not any file that
// happens to match index-*.js (a lazy chunk from an `index` module would too).
const entry = readFileSync(join(dist, 'index.html'), 'utf8').match(
  /<script[^>]*type="module"[^>]*src="\/assets\/([^"]+\.js)"/,
);
if (!entry) {
  console.error('Could not find the entry module script in dist/index.html.');
  process.exit(1);
}
const main = [entry[1]];
const assets = join(dist, 'assets');
const kb = gzipSync(readFileSync(join(assets, main[0]))).length / 1000;
console.log(`Main chunk ${main[0]}: ${kb.toFixed(2)} kB gzip (warn ${WARN_KB}, fail ${FAIL_KB})`);
if (kb > FAIL_KB) {
  console.error(`Main chunk exceeds the ${FAIL_KB} kB gzip budget — split English by role (plan, Phase 5) before adding more eager namespaces.`);
  process.exit(1);
}
if (kb > WARN_KB) console.warn(`::warning::Main chunk is within ${(FAIL_KB - kb).toFixed(2)} kB of the ${FAIL_KB} kB budget.`);
