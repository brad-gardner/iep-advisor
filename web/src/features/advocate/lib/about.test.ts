import { describe, expect, it } from 'vitest';
import { parseAbout } from './about';

describe('parseAbout', () => {
  it('accepts a kind and a 1-9 digit id, mirroring the server grammar', () => {
    expect(parseAbout('iep:12')).toEqual({ kind: 'iep', id: 12 });
    expect(parseAbout('journal:999999999')).toEqual({ kind: 'journal', id: 999999999 });
  });

  it('rejects an id longer than the server accepts instead of letting a deep link create an empty thread', () => {
    // The server grammar caps at \d{1,9}; a longer id must be dropped client-side
    // (parseAbout returns null) rather than accepted here and 400'd after a
    // thread is already created for it.
    expect(parseAbout('iep:1234567890')).toBeNull();
  });

  it('rejects an unknown kind, a missing id, and a non-numeric id', () => {
    expect(parseAbout('unknown:12')).toBeNull();
    expect(parseAbout('iep:')).toBeNull();
    expect(parseAbout('iep:abc')).toBeNull();
    expect(parseAbout('iep:0')).toBeNull();
    expect(parseAbout(null)).toBeNull();
    expect(parseAbout(undefined)).toBeNull();
  });
});
