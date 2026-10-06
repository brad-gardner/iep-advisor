import i18n from './index';
import { DEFAULT_LANGUAGE } from './detect';

/**
 * The active UI language as a BCP-47 tag suitable for `Intl.*` constructors.
 * Falls back to `en` before i18next has finished resolving a language (e.g.
 * synchronous module evaluation during the very first render).
 */
export function getActiveLanguage(): string {
  return i18n.language || DEFAULT_LANGUAGE;
}
