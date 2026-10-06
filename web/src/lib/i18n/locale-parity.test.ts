import { describe, expect, it } from 'vitest';

// Eager (not lazy) on purpose: a test needs both languages' full content at
// once, and this file is never shipped to the browser. Discovering
// namespaces by glob (rather than a hardcoded list) means a new namespace
// added in a later phase is covered automatically.
const enModules = import.meta.glob('/src/locales/en/*.json', { eager: true }) as Record<
  string,
  { default: Record<string, unknown> }
>;
const esModules = import.meta.glob('/src/locales/es/*.json', { eager: true }) as Record<
  string,
  { default: Record<string, unknown> }
>;

function namespaceOf(path: string): string {
  const match = /\/([^/]+)\.json$/.exec(path);
  if (!match) throw new Error(`Could not derive a namespace from ${path}`);
  return match[1];
}

// i18next's CLDR plural suffixes (https://www.i18next.com/translation-function/plurals).
// English only ever needs `_one`/`_other`; a language with more plural
// categories (the project's `es` doesn't currently need any, but a later
// locale might) can carry the extra forms without tripping the "no
// orphaned es key" check below.
const PLURAL_SUFFIX_PATTERN = /_(zero|one|two|few|many|other)$/;

/** `{{name}}`-style placeholder names used in an i18next resource string. */
function placeholdersOf(value: unknown): Set<string> {
  if (typeof value !== 'string') return new Set();
  const names = Array.from(value.matchAll(/\{\{\s*([^}]+?)\s*\}\}/g)).map((m) => m[1]);
  return new Set(names);
}

function setsEqual(a: Set<string>, b: Set<string>): boolean {
  if (a.size !== b.size) return false;
  for (const value of a) {
    if (!b.has(value)) return false;
  }
  return true;
}

function describeSet(s: Set<string>): string {
  return `{${[...s].join(', ')}}`;
}

/** Flattens a nested resource object into `{ "a.b.c": "value" }`. */
function flatten(obj: Record<string, unknown>, prefix = ''): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(obj)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
      Object.assign(out, flatten(value as Record<string, unknown>, path));
    } else {
      out[path] = value;
    }
  }
  return out;
}

const enByNamespace = new Map(
  Object.entries(enModules).map(([path, mod]) => [namespaceOf(path), mod.default])
);
const esByNamespace = new Map(
  Object.entries(esModules).map(([path, mod]) => [namespaceOf(path), mod.default])
);

describe('locale key parity (en <-> es)', () => {
  it('ships the same set of namespace files for en and es', () => {
    expect(new Set(esByNamespace.keys())).toEqual(new Set(enByNamespace.keys()));
  });

  for (const [namespace, enResource] of enByNamespace) {
    describe(`namespace: ${namespace}`, () => {
      const esResource = esByNamespace.get(namespace);

      it('exists in es', () => {
        expect(esResource).toBeDefined();
      });

      const enFlat = flatten(enResource);
      const esFlat = esResource ? flatten(esResource) : {};

      it('has every en key present in es', () => {
        const missing = Object.keys(enFlat).filter((key) => !(key in esFlat));
        expect(missing).toEqual([]);
      });

      it('has no es key that does not exist in en (no orphaned translations, except an allowed extra CLDR plural form)', () => {
        const extra = Object.keys(esFlat).filter((key) => {
          if (key in enFlat) return false;
          const match = PLURAL_SUFFIX_PATTERN.exec(key);
          if (!match) return true; // genuinely orphaned
          // Allowed only when en pluralizes this same base key at all —
          // an es-only `_many` (etc.) alongside en's `_one`/`_other` is
          // fine; an es-only key with no en plural counterpart isn't.
          const base = key.slice(0, match.index);
          return !(`${base}_one` in enFlat || `${base}_other` in enFlat);
        });
        expect(extra).toEqual([]);
      });

      it('uses the same {{placeholder}} set in es as in en for every key', () => {
        const mismatches: string[] = [];

        for (const [key, enValue] of Object.entries(enFlat)) {
          const esValue = esFlat[key];
          if (esValue === undefined) continue; // covered by the "every en key present in es" check above
          const enPlaceholders = placeholdersOf(enValue);
          const esPlaceholders = placeholdersOf(esValue);
          if (!setsEqual(enPlaceholders, esPlaceholders)) {
            mismatches.push(`${key}: en=${describeSet(enPlaceholders)} es=${describeSet(esPlaceholders)}`);
          }
        }

        // An allowed extra es plural form (e.g. `_many`) is compared against
        // its en `_other` (falling back to `_one`) reference, since that's
        // the form it stands in for.
        for (const key of Object.keys(esFlat)) {
          if (key in enFlat) continue;
          const match = PLURAL_SUFFIX_PATTERN.exec(key);
          if (!match) continue;
          const base = key.slice(0, match.index);
          const enReference = enFlat[`${base}_other`] ?? enFlat[`${base}_one`];
          if (enReference === undefined) continue;
          const enPlaceholders = placeholdersOf(enReference);
          const esPlaceholders = placeholdersOf(esFlat[key]);
          if (!setsEqual(enPlaceholders, esPlaceholders)) {
            mismatches.push(`${key}: en(reference)=${describeSet(enPlaceholders)} es=${describeSet(esPlaceholders)}`);
          }
        }

        expect(mismatches).toEqual([]);
      });

      it('has a non-empty es value for every key', () => {
        const empty = Object.entries(esFlat)
          .filter(([, value]) => typeof value !== 'string' || value.trim().length === 0)
          .map(([key]) => key);
        expect(empty).toEqual([]);
      });

      it('has a non-empty en value for every key', () => {
        const empty = Object.entries(enFlat)
          .filter(([, value]) => typeof value !== 'string' || value.trim().length === 0)
          .map(([key]) => key);
        expect(empty).toEqual([]);
      });
    });
  }
});
