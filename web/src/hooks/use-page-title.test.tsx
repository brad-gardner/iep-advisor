import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup, act } from '@testing-library/react';
import { useState } from 'react';
import { usePageTitle } from './use-page-title';

function TitleProbe({ title }: { title: string | null | undefined }) {
  usePageTitle(title);
  return null;
}

function ChildWithOwnTitle({ title }: { title: string }) {
  usePageTitle(title);
  return null;
}

/** A persistent parent (its own title never changes) that can later mount a
 * child with its own `usePageTitle` call — e.g. a modal opening on top of an
 * already-rendered page — and unmount it again. The child starts absent so
 * its effect runs in its own commit, after the parent's title has already
 * settled (effects fire child-before-parent within the SAME commit, so a
 * child present from the first render would have its title immediately
 * overwritten by the parent's — this mirrors the realistic case instead). */
function ParentWithToggleableChild() {
  const [showChild, setShowChild] = useState(false);
  usePageTitle('Parent');
  return (
    <>
      {showChild && <ChildWithOwnTitle title="Child" />}
      <button onClick={() => setShowChild(true)}>show child</button>
      <button onClick={() => setShowChild(false)}>hide child</button>
    </>
  );
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

  it('restores the previous title when the component unmounts', () => {
    document.title = 'Whatever was there before';
    const { unmount } = render(<TitleProbe title="Students" />);
    expect(document.title).toBe('Students · IEP Advisor');
    unmount();
    expect(document.title).toBe('Whatever was there before');
  });

  it("restores a persistent parent's title when a nested child with its own usePageTitle mounts then unmounts", () => {
    const { getByRole } = render(<ParentWithToggleableChild />);
    expect(document.title).toBe('Parent · IEP Advisor');

    act(() => {
      getByRole('button', { name: 'show child' }).click();
    });
    expect(document.title).toBe('Child · IEP Advisor');

    act(() => {
      getByRole('button', { name: 'hide child' }).click();
    });
    expect(document.title).toBe('Parent · IEP Advisor');
  });
});
