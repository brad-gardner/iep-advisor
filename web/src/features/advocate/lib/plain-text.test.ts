import { describe, expect, it } from 'vitest';
import { markdownToPlainText } from './plain-text';

describe('markdownToPlainText', () => {
  it('drops markdown syntax but keeps the words', () => {
    const md = '## What PWN is\n\nPrior written notice is the **letter** the school must send.\n\n- First point\n- [Open the guide](/knowledge-base/65)\n\n| a | b |\n|---|---|\n| 1 | 2 |';
    expect(markdownToPlainText(md)).toBe('What PWN is Prior written notice is the letter the school must send. First point Open the guide a b 1 2');
  });

  it('is safe on empty and plain input', () => {
    expect(markdownToPlainText('')).toBe('');
    expect(markdownToPlainText('just words')).toBe('just words');
  });
});
