import { describe, it, expect } from 'vitest';
import { cn } from './cn';

describe('cn', () => {
  it('joins truthy class names with a single space', () => {
    expect(cn('a', 'b', 'c')).toBe('a b c');
  });

  it('drops falsy values (false, null, undefined, empty string)', () => {
    expect(cn('a', false, null, undefined, '', 'b')).toBe('a b');
  });

  it('supports conditional expressions', () => {
    const isActive = true;
    const size: 'sm' | 'lg' = 'sm';
    expect(cn('base', isActive && 'active', size === 'sm' && 'text-sm')).toBe(
      'base active text-sm'
    );
  });

  it('preserves source order (no Tailwind merge)', () => {
    expect(cn('p-2', 'p-4')).toBe('p-2 p-4');
  });

  it('returns an empty string when everything is falsy', () => {
    expect(cn(false, null, undefined, '')).toBe('');
  });
});
