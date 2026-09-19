import { ABOUT_PATTERN } from '../types/advocate';

/** The record kinds a launcher can open a conversation about — the server grammar. */
export type AboutKind = 'iep' | 'etr' | 'goal' | 'analysis' | 'progress_report' | 'journal';

export interface AboutRef {
  kind: AboutKind;
  id: number;
}

/** `iep:12` — the only shape the API accepts in `SendAdvocateMessageRequest.about`. */
export function formatAbout(kind: AboutKind, id: number): string {
  return `${kind}:${id}`;
}

/** The launcher's destination: a fresh conversation on the advocate page about one record. */
export function advocateHref(childId: number, about: string): string {
  return `/children/${childId}/advocate?about=${encodeURIComponent(about)}`;
}

/** Parses `?about=`; null for anything outside the server grammar (it is then ignored, never sent). */
export function parseAbout(value: string | null | undefined): AboutRef | null {
  if (!value || !ABOUT_PATTERN.test(value)) return null;
  const [kind, id] = value.split(':');
  const n = Number(id);
  return n > 0 ? { kind: kind as AboutKind, id: n } : null;
}

const GENERIC_NOUN: Record<AboutKind, string> = {
  iep: 'this IEP',
  etr: 'this ETR',
  goal: 'this goal',
  analysis: 'this analysis',
  progress_report: 'this progress report',
  journal: 'this journal entry',
};

/**
 * The context pill text. A launcher may pass a human label through router
 * state ("IEP from March 2026"); without one the pill names the kind. The raw
 * `about` token is never shown.
 */
export function aboutContextLabel(about: AboutRef, label?: string | null): string {
  const clean = label?.replace(/\s+/g, ' ').trim();
  return `About: ${clean ? clean : GENERIC_NOUN[about.kind]}`;
}

/** Router `state` shape a launcher attaches so the advocate page can name the record. */
export interface AboutNavigationState {
  aboutLabel?: string;
}

export function readAboutLabel(state: unknown): string | undefined {
  if (!state || typeof state !== 'object') return undefined;
  const label = (state as AboutNavigationState).aboutLabel;
  return typeof label === 'string' && label.trim() ? label : undefined;
}
