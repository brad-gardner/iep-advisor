import { describe, expect, it } from 'vitest';
// `document-authoring` is a staff-only namespace (plan phase 5) — see
// `../staff-locales`'s doc comment and `docs/i18n/README.md`'s "Staff and
// admin namespaces". `computeCompleteness` below calls `i18n.t` for this
// namespace directly (not via a component), so it must be registered here
// too, the same way the real route chunk does.
import '../staff-locales';
import { computeCompleteness } from './completeness';
import type { TemplateVersionDetailDto } from '../types';

const goalsKey = 'f1111111-1111-1111-1111-111111111111';
const plaafpKey = 'f2222222-2222-2222-2222-222222222222';
const goalCol = 'c1111111-1111-1111-1111-111111111111';
const baseCol = 'c2222222-2222-2222-2222-222222222222';
const measCol = 'c3333333-3333-3333-3333-333333333333';
const servicesKey = 'f3333333-3333-3333-3333-333333333333';
const svcType = 'c4444444-4444-4444-4444-444444444444';
const svcFreq = 'c5555555-5555-5555-5555-555555555555';

const template = {
  id: 1,
  versionNumber: 1,
  status: 'Published',
  sections: [
    {
      id: 10,
      title: 'Profile',
      displayOrder: 0,
      fields: [
        { id: 100, fieldKey: plaafpKey, fieldType: 'RichText', label: 'Present Levels', required: true, configJson: '{"semantic":"presentLevels"}', displayOrder: 0 },
      ],
    },
    {
      id: 20,
      title: 'Goals',
      displayOrder: 1,
      fields: [
        {
          id: 200,
          fieldKey: goalsKey,
          fieldType: 'Table',
          label: 'Goals',
          required: false,
          displayOrder: 0,
          configJson: JSON.stringify({
            semantic: 'goals',
            columns: [
              { columnKey: goalCol, type: 'Text', label: 'Goal', required: false, semantic: 'goalText' },
              { columnKey: baseCol, type: 'Text', label: 'Baseline', required: false, semantic: 'baseline' },
              { columnKey: measCol, type: 'Text', label: 'Measure', required: false, semantic: 'measurementMethod' },
            ],
          }),
        },
      ],
    },
  ],
} as unknown as TemplateVersionDetailDto;

const servicesTemplate = {
  ...template,
  sections: [
    {
      id: 30,
      title: 'Services',
      displayOrder: 0,
      fields: [
        {
          id: 300,
          fieldKey: servicesKey,
          fieldType: 'Table',
          label: 'Services',
          required: true,
          displayOrder: 0,
          configJson: JSON.stringify({
            semantic: 'services',
            columns: [
              { columnKey: svcType, type: 'Text', label: 'Service', required: false, semantic: 'serviceType' },
              { columnKey: svcFreq, type: 'Text', label: 'Frequency', required: false, semantic: 'frequency' },
            ],
          }),
        },
      ],
    },
  ],
} as unknown as TemplateVersionDetailDto;

describe('computeCompleteness', () => {
  it('flags a required empty table and service rows missing frequency', () => {
    const empty = computeCompleteness(servicesTemplate, { [servicesKey]: [] });
    expect(empty.items.map((i) => `${i.severity}:${i.message}`)).toEqual(['required:Services is required']);

    const partial = computeCompleteness(servicesTemplate, { [servicesKey]: [{ _rowId: 'r', [svcType]: 'Speech', [svcFreq]: '' }] });
    // Plan 2026-10-02-002: a services row with no `_ownerUserId` also gets the new
    // "has no owner" advisory, alongside the pre-existing "has no frequency" one.
    expect(partial.items.map((i) => `${i.severity}:${i.message}`)).toEqual([
      'advisory:Service "Speech" has no frequency',
      'advisory:Service "Speech" has no owner',
    ]);
    expect(partial.items[0].fieldId).toBe(300);
  });

  it('flags required blanks as required and semantic gaps as advisory', () => {
    const result = computeCompleteness(template, {
      [goalsKey]: [{ _rowId: 'r1', [goalCol]: 'Read 90 wpm', [baseCol]: '', [measCol]: 'CBM' }],
    });
    // Plan 2026-10-02-002: a goal row with neither `_ownerUserId` nor `_objectives`
    // also gets the new "has no owner" and "has no objectives" advisories.
    expect(result.items.map((i) => `${i.severity}:${i.message}`)).toEqual([
      'required:Present Levels is required',
      'advisory:Goal "Read 90 wpm" has no baseline',
      // no targetCriteria column on this template → nothing to flag
      'advisory:Goal "Read 90 wpm" has no owner',
      'advisory:Goal "Read 90 wpm" has no objectives',
    ]);
    expect(result.percent).toBe(50);
  });

  it('treats html-only rich text as blank and reports an empty goals block', () => {
    const result = computeCompleteness(template, { [plaafpKey]: '<p></p>', [goalsKey]: [] });
    expect(result.items.some((i) => i.message === 'Present Levels is required')).toBe(true);
    expect(result.items.some((i) => i.message === 'No annual goals yet')).toBe(true);
    expect(result.percent).toBe(0);
  });

  it('flags carried-forward rows until they are kept or edited', () => {
    const stale = computeCompleteness(template, {
      [plaafpKey]: '<p>x</p>',
      [goalsKey]: [
        { _rowId: 'r1', _carriedFrom: { versionId: 1, rowId: 'r1' }, _confirmed: false, [goalCol]: 'g', [baseCol]: 'b', [measCol]: 'm' },
        { _rowId: 'r2', _carriedFrom: { versionId: 1, rowId: 'r2' }, _confirmed: true, [goalCol]: 'g', [baseCol]: 'b', [measCol]: 'm' },
      ],
    });
    expect(stale.items.map((i) => i.message)).toContain('1 carried-forward row in Goals not yet reviewed');
  });

  it('is clean when everything is filled', () => {
    const result = computeCompleteness(
      { ...template, sections: [template.sections[0]] } as TemplateVersionDetailDto,
      { [plaafpKey]: '<p>Reads at 42 wpm.</p>' }
    );
    expect(result.items).toEqual([]);
    expect(result.percent).toBe(100);
  });
});

// -----------------------------------------------------------------------------
// Owner + objectives advisories (plan 2026-10-02-002) — parity with
// api/IepAssistant.Services.Tests/DocumentCompletenessServiceTests.cs's
// "Compute_GoalRowWith…" / "Compute_ServiceRowWithNoOwner…" fixtures: a Goals
// table tagged with the real "goals" semantic + a `goalText` column (and
// nothing else), so only the owner/objectives checks can fire — isolating their
// count from the pre-existing goalText/baseline/measurement/targetCriteria
// advisories exercised above.
// -----------------------------------------------------------------------------

const accommodationsKey = 'f4444444-4444-4444-4444-444444444444';
const accommodationCol = 'c6666666-6666-6666-6666-666666666666';
const transitionKey = 'f5555555-5555-5555-5555-555555555555';
const transitionCol = 'c7777777-7777-7777-7777-777777777777';

const semanticTemplate = {
  id: 2,
  versionNumber: 1,
  status: 'Published',
  sections: [
    {
      id: 40,
      title: 'Goals',
      displayOrder: 0,
      fields: [
        {
          id: 400,
          fieldKey: goalsKey,
          fieldType: 'Table',
          label: 'Goals',
          required: false,
          displayOrder: 0,
          configJson: JSON.stringify({
            semantic: 'goals',
            columns: [{ columnKey: goalCol, type: 'Text', label: 'Goal', required: false, semantic: 'goalText' }],
          }),
        },
      ],
    },
    {
      id: 41,
      title: 'Services',
      displayOrder: 1,
      fields: [
        {
          id: 410,
          fieldKey: servicesKey,
          fieldType: 'Table',
          label: 'Services',
          required: false,
          displayOrder: 0,
          configJson: JSON.stringify({
            semantic: 'services',
            columns: [{ columnKey: svcType, type: 'Text', label: 'Service', required: false, semantic: 'serviceType' }],
          }),
        },
      ],
    },
    {
      id: 42,
      title: 'Accommodations',
      displayOrder: 2,
      fields: [
        {
          id: 420,
          fieldKey: accommodationsKey,
          fieldType: 'Table',
          label: 'Accommodations',
          required: false,
          displayOrder: 0,
          configJson: JSON.stringify({
            semantic: 'accommodations',
            columns: [{ columnKey: accommodationCol, type: 'Text', label: 'Accommodation', required: false, semantic: 'accommodation' }],
          }),
        },
      ],
    },
    {
      id: 43,
      title: 'Transition',
      displayOrder: 3,
      fields: [
        {
          id: 430,
          fieldKey: transitionKey,
          fieldType: 'Table',
          label: 'Transition',
          required: false,
          displayOrder: 0,
          configJson: JSON.stringify({
            semantic: 'transition',
            columns: [{ columnKey: transitionCol, type: 'Text', label: 'Services', required: false, semantic: 'transitionServices' }],
          }),
        },
      ],
    },
  ],
} as unknown as TemplateVersionDetailDto;

function onlySection(sectionId: number): TemplateVersionDetailDto {
  return { ...semanticTemplate, sections: semanticTemplate.sections.filter((s) => s.id === sectionId) } as TemplateVersionDetailDto;
}

describe('computeCompleteness — owner + objectives advisories', () => {
  it('a goal with an owner and objectives has neither advisory', () => {
    const result = computeCompleteness(onlySection(40), {
      [goalsKey]: [{ _rowId: 'r1', [goalCol]: 'Read better', _ownerUserId: 7, _objectives: [{ description: 'Read a paragraph' }] }],
    });
    expect(result.items).toEqual([]);
  });

  it('a goal with objectives but no owner gets exactly the owner advisory', () => {
    const result = computeCompleteness(onlySection(40), {
      [goalsKey]: [{ _rowId: 'r1', [goalCol]: 'Read better', _objectives: [{ description: 'Read a paragraph' }] }],
    });
    expect(result.items.map((i) => i.message)).toEqual(['Goal "Read better" has no owner']);
  });

  it('a goal with neither owner nor objectives gets both advisories', () => {
    const result = computeCompleteness(onlySection(40), {
      [goalsKey]: [{ _rowId: 'r1', [goalCol]: 'Read better' }],
    });
    expect(result.items.map((i) => i.message)).toEqual([
      'Goal "Read better" has no owner',
      'Goal "Read better" has no objectives',
    ]);
  });

  it('an empty `_objectives` array still counts as "no objectives" (owner present, so only one advisory)', () => {
    const result = computeCompleteness(onlySection(40), {
      [goalsKey]: [{ _rowId: 'r1', [goalCol]: 'Read better', _ownerUserId: 7, _objectives: [] }],
    });
    expect(result.items.map((i) => i.message)).toEqual(['Goal "Read better" has no objectives']);
  });

  it('a service with no owner gets exactly one advisory, never an objectives check', () => {
    const result = computeCompleteness(onlySection(41), {
      [servicesKey]: [{ _rowId: 'r1', [svcType]: 'OT' }],
    });
    expect(result.items.map((i) => i.message)).toEqual(['Service "OT" has no owner']);
  });

  it('an accommodation with no owner is flagged with the accommodation item label', () => {
    const result = computeCompleteness(onlySection(42), {
      [accommodationsKey]: [{ _rowId: 'r1', [accommodationCol]: 'Extended time' }],
    });
    expect(result.items.map((i) => i.message)).toEqual(['Accommodation "Extended time" has no owner']);
  });

  it('a transition item with no owner is flagged with the "Transition item" label', () => {
    const result = computeCompleteness(onlySection(43), {
      [transitionKey]: [{ _rowId: 'r1', [transitionCol]: 'Explore electives' }],
    });
    expect(result.items.map((i) => i.message)).toEqual(['Transition item "Explore electives" has no owner']);
  });

  it("an untagged table never counts owner or objectives advisories (mirrors the server's gating)", () => {
    // A Goals table with a `goalText`-tagged column but NO field-level `semantic`
    // at all (unlike every other fixture above, which sets `semantic: 'goals'`) —
    // confirms the new checks are gated on the field's own semantic, not on
    // having a reserved key missing from an arbitrary table.
    const untagged = {
      ...onlySection(40),
      sections: [
        {
          ...onlySection(40).sections[0],
          fields: [
            {
              ...onlySection(40).sections[0].fields[0],
              configJson: JSON.stringify({
                columns: [{ columnKey: goalCol, type: 'Text', label: 'Goal', required: false, semantic: 'goalText' }],
              }),
            },
          ],
        },
      ],
    } as unknown as TemplateVersionDetailDto;

    const result = computeCompleteness(untagged, {
      [goalsKey]: [{ _rowId: 'r1', [goalCol]: 'Read better' }],
    });
    expect(result.items.some((i) => /has no (owner|objectives)/.test(i.message))).toBe(false);
  });
});
