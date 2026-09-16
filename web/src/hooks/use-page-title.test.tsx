import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/react';
import { usePageTitle } from './use-page-title';

function TitleProbe({ title }: { title: string | null | undefined }) {
  usePageTitle(title);
  return null;
}

describe('usePageTitle', () => {
  afterEach(cleanup);

  it('sets document.title to "<title> · IEP Advisor"', () => {
    render(<TitleProbe title="Students" />);
    expect(document.title).toBe('Students · IEP Advisor');
  });

  it('updates document.title when the title prop changes', () => {
    const { rerender } = render(<TitleProbe title="Students" />);
    expect(document.title).toBe('Students · IEP Advisor');
    rerender(<TitleProbe title="Ada Lovelace" />);
    expect(document.title).toBe('Ada Lovelace · IEP Advisor');
  });

  it('falls back to the bare app name for an empty/undefined title', () => {
    render(<TitleProbe title={undefined} />);
    expect(document.title).toBe('IEP Advisor');
  });
});
