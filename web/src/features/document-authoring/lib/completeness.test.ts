import { describe, expect, it } from 'vitest';
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
    expect(partial.items.map((i) => `${i.severity}:${i.message}`)).toEqual(['advisory:Service "Speech" has no frequency']);
    expect(partial.items[0].fieldId).toBe(300);
  });

  it('flags required blanks as required and semantic gaps as advisory', () => {
    const result = computeCompleteness(template, {
      [goalsKey]: [{ _rowId: 'r1', [goalCol]: 'Read 90 wpm', [baseCol]: '', [measCol]: 'CBM' }],
    });
    expect(result.items.map((i) => `${i.severity}:${i.message}`)).toEqual([
      'required:Present Levels is required',
      'advisory:Goal "Read 90 wpm" has no baseline',
      // no targetCriteria column on this template → nothing to flag
    ]);
    expect(result.percent).toBe(50);
  });

  it('treats html-only rich text as blank and reports an empty goals block', () => {
    const result = computeCompleteness(template, { [plaafpKey]: '<p></p>', [goalsKey]: [] });
    expect(result.items.some((i) => i.message === 'Present Levels is required')).toBe(true);
    expect(result.items.some((i) => i.message === 'No annual goals yet')).toBe(true);
    expect(result.percent).toBe(0);
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
