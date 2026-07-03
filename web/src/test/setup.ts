import '@testing-library/jest-dom';
import { vi } from 'vitest';

// jsdom does not implement scrolling APIs used by overlay scroll-lock. Stub
// them so tests exercising Modal/Drawer open/close don't emit "Not implemented"
// noise.
if (!('scrollTo' in window) || typeof window.scrollTo !== 'function') {
  window.scrollTo = vi.fn();
} else {
  vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
}
