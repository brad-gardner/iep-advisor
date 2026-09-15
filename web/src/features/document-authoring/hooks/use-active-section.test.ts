import { describe, expect, it } from 'vitest';
import { stepSection } from './use-active-section';

describe('stepSection', () => {
  const ids = [10, 20, 30];

  it('steps to the neighbour of the active section and clamps at both ends', () => {
    expect(stepSection(ids, 10, 1)).toBe(20);
    expect(stepSection(ids, 20, 1)).toBe(30);
    expect(stepSection(ids, 30, 1)).toBe(30);
    expect(stepSection(ids, 20, -1)).toBe(10);
    expect(stepSection(ids, 10, -1)).toBe(10);
  });

  it('starts from the first section when nothing is active, and returns null for no sections', () => {
    expect(stepSection(ids, null, 1)).toBe(10);
    expect(stepSection(ids, 999, -1)).toBe(10);
    expect(stepSection([], null, 1)).toBeNull();
  });
});
