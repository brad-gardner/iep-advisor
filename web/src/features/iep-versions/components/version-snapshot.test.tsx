import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { VersionSnapshot } from './version-snapshot';
import type { IepVersionDto } from '../types';

function version(overrides: Partial<IepVersionDto> = {}): IepVersionDto {
  return {
    id: 1,
    schoolStudentId: 1,
    sourceDraftId: 1,
    versionNumber: 1,
    documentType: 'IEP',
    title: 'IEP',
    effectiveDate: null,
    finalizedByUserId: 1,
    finalizedAt: '2026-01-01T00:00:00Z',
    pdfRenderStatus: null,
    pdfBlobUri: null,
    pdfRenderedAt: null,
    sections: [],
    goals: [],
    serviceLines: [],
    accommodations: [],
    transitionItems: [],
    ...overrides,
  };
}

describe('VersionSnapshot', () => {
  it('renders a narrative section RichText value as formatted markdown', () => {
    render(
      <VersionSnapshot
        version={version({
          sections: [
            {
              id: 1,
              sectionKind: 'PresentLevels',
              richText: 'Reads **60 wpm** with support.',
              displayOrder: 0,
              lineageId: 'l1',
            },
          ],
        })}
      />
    );

    const sections = screen.getByTestId('snapshot-sections');
    expect(sections.querySelector('strong')).toHaveTextContent('60 wpm');
  });

  it('renders a goal-table Text value literally, without parsing markdown syntax', () => {
    render(
      <VersionSnapshot
        version={version({
          goals: [
            {
              id: 1,
              domain: null,
              goalText: '**Improve** reading fluency',
              baseline: null,
              targetCriteria: null,
              measurementMethod: null,
              timeframe: null,
              displayOrder: 0,
              lineageId: 'g1',
            },
          ],
        })}
      />
    );

    const goals = screen.getByTestId('snapshot-goals');
    expect(goals.querySelector('strong')).not.toBeInTheDocument();
    expect(goals).toHaveTextContent('**Improve** reading fluency');
  });
});
