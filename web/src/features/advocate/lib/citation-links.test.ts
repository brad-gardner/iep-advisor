import { describe, expect, it } from 'vitest';
import { CITATION_KINDS, type AdvocateCitation } from '../types/advocate';
import { citationHref, citationLabel, openGoalHref } from './citation-links';

const childId = 4;
const cite = (kind: string, id: number, parent?: { kind: string; id: number } | null): AdvocateCitation => ({
  kind,
  id,
  label: `${kind} ${id}`,
  parent: parent ?? null,
});

describe('citationHref', () => {
  it.each<[string, { kind: string; id: number } | null, string | null]>([
    ['kb', null, '/knowledge-base/12'],
    ['child', null, '/children/4/overview'],
    ['iep', null, '/children/4/ieps/12'],
    ['etr', null, '/children/4/etrs/12'],
    ['progress_report', { kind: 'iep', id: 3 }, '/children/4/ieps/3/progress-reports/12'],
    ['progress_report', null, '/children/4/ieps'],
    ['authored_version', null, '/children/4/authored-versions/12'],
    ['shared_draft', null, '/children/4/shared-drafts/12'],
    ['iep_analysis', { kind: 'iep', id: 3 }, '/children/4/analysis'],
    ['etr_analysis', { kind: 'etr', id: 3 }, '/children/4/analysis'],
    ['progress_report_analysis', { kind: 'progress_report', id: 3 }, '/children/4/analysis'],
    ['analysis_run', null, '/children/4/analysis'],
    ['iep_section', { kind: 'iep', id: 3 }, '/children/4/ieps/3'],
    ['iep_section', null, '/children/4/ieps'],
    ['etr_section', { kind: 'etr', id: 3 }, '/children/4/etrs/3'],
    ['etr_section', null, '/children/4/etrs'],
    ['goal', { kind: 'iep', id: 3 }, '/children/4/ieps/3#goal-12'],
    ['goal', null, '/children/4/goals'],
    ['goal_record', null, '/children/4/goals'],
    ['comparison', null, null],
    ['journal', null, '/children/4/journal?entry=12'],
    ['contribution', null, '/children/4/overview'],
    ['advocacy_goal', null, '/children/4/overview'],
    ['meeting_prep', null, '/children/4/meeting-prep'],
    ['meeting', null, '/children/4/overview'],
  ])('maps %s (parent %j) to %s', (kind, parent, expected) => {
    expect(citationHref(cite(kind, 12, parent), childId)).toBe(expected);
  });

  it('covers every kind the server can return', () => {
    const decided = new Set<string>();
    for (const kind of CITATION_KINDS) {
      // Either a route or an explicit non-link; never an accidental fallthrough to a wrong page.
      const href = citationHref(cite(kind, 12, { kind: 'iep', id: 3 }), childId);
      expect(href === null || href.startsWith('/')).toBe(true);
      decided.add(kind);
    }
    expect(decided.size).toBe(CITATION_KINDS.length);
  });

  it('renders unknown kinds and bad ids as non-links', () => {
    expect(citationHref(cite('mystery', 12), childId)).toBeNull();
    expect(citationHref(cite('kb', 0), childId)).toBeNull();
    expect(citationHref(cite('kb', -3), childId)).toBeNull();
    expect(citationHref(cite('goal', 12, { kind: 'iep', id: 0 }), childId)).toBe('/children/4/goals');
  });
});

describe('citationLabel', () => {
  it('prefers the server label and never shows a raw kind token', () => {
    expect(citationLabel({ kind: 'goal', id: 1, label: '  Reading goal ' })).toBe('Reading goal');
    expect(citationLabel({ kind: 'kb', id: 7 })).toBe('Knowledge base #7');
    expect(citationLabel({ kind: 'child', id: 4, label: '' })).toBe('Child profile');
    expect(citationLabel({ kind: 'comparison', id: 2 })).toBe('IEP comparison');
    expect(citationLabel({ kind: 'iep_section', id: 9 })).toBe('Source #9');
  });
});

describe('openGoalHref', () => {
  const citations: AdvocateCitation[] = [cite('goal', 340, { kind: 'iep', id: 12 }), cite('goal', 341)];

  it('reuses the cited goal deep link when the suggestion names a goal cited with its IEP', () => {
    expect(openGoalHref(340, citations, childId)).toBe('/children/4/ieps/12#goal-340');
  });

  it('falls back to the goals tab without an id, an uncited id, or a goal cited without its IEP', () => {
    expect(openGoalHref(undefined, citations, childId)).toBe('/children/4/goals');
    expect(openGoalHref(999, citations, childId)).toBe('/children/4/goals');
    expect(openGoalHref(341, citations, childId)).toBe('/children/4/goals');
  });
});
