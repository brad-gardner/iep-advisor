import { describe, it, expect, vi, afterEach } from 'vitest';
import { jumpToFieldWhenVisible, sectionDomId } from './section-dom';

/** Queues rAF callbacks so a test can flush "frames" by hand. */
function fakeFrames() {
  const frames: FrameRequestCallback[] = [];
  const spy = vi.spyOn(window, 'requestAnimationFrame').mockImplementation((cb) => {
    frames.push(cb);
    return frames.length;
  });
  return { flush: () => frames.splice(0).forEach((cb) => cb(0)), restore: () => spy.mockRestore() };
}

describe('jumpToFieldWhenVisible', () => {
  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('waits until the target has no hidden ancestor, then scrolls and focuses it', () => {
    const { flush, restore } = fakeFrames();
    const wrapper = document.createElement('div');
    wrapper.hidden = true;
    const field = document.createElement('input');
    field.id = 'document-field-1';
    field.scrollIntoView = vi.fn();
    wrapper.appendChild(field);
    document.body.appendChild(wrapper);

    jumpToFieldWhenVisible('document-field-1', 9);
    flush();
    flush();
    expect(field.scrollIntoView).not.toHaveBeenCalled();

    wrapper.hidden = false;
    flush();
    expect(field.scrollIntoView).toHaveBeenCalledTimes(1);
    expect(document.activeElement).toBe(field);
    restore();
  });

  it('gives up after the frame budget and jumps anyway (degrades to the plain jump)', () => {
    const { flush, restore } = fakeFrames();
    const wrapper = document.createElement('div');
    wrapper.hidden = true;
    const field = document.createElement('input');
    field.id = 'document-field-2';
    field.scrollIntoView = vi.fn();
    wrapper.appendChild(field);
    document.body.appendChild(wrapper);

    jumpToFieldWhenVisible('document-field-2', 9, 3);
    flush(); // attempt(3)
    flush(); // attempt(2)
    flush(); // attempt(1)
    expect(field.scrollIntoView).not.toHaveBeenCalled();
    flush(); // attempt(0) → jump regardless
    expect(field.scrollIntoView).toHaveBeenCalledTimes(1);
    restore();
  });

  it('falls back to the section when the field is missing', () => {
    const { flush, restore } = fakeFrames();
    const section = document.createElement('section');
    section.id = sectionDomId(9);
    section.tabIndex = -1;
    section.scrollIntoView = vi.fn();
    document.body.appendChild(section);

    jumpToFieldWhenVisible('document-field-missing', 9);
    flush();
    expect(section.scrollIntoView).toHaveBeenCalledTimes(1);
    expect(document.activeElement).toBe(section);
    restore();
  });
});
