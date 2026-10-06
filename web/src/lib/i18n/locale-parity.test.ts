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

      it('has no es key that does not exist in en (no orphaned translations)', () => {
        const extra = Object.keys(esFlat).filter((key) => !(key in enFlat));
        expect(extra).toEqual([]);
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
