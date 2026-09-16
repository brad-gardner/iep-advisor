import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import {
  makeHomeDraft,
  makeHomeMeeting,
  makeHomeObligation,
  makeStaffHome,
} from '../test/fixtures';
import { CaseloadHome } from './caseload-home';

function renderHome(overrides: Parameters<typeof makeStaffHome>[0] = {}) {
  return render(
    <MemoryRouter>
      <CaseloadHome staff={makeStaffHome(overrides)} />
    </MemoryRouter>
  );
}

describe('CaseloadHome', () => {
  it('renders This week, Due soon, and Drafts sections with links to the student/document', () => {
    renderHome({
      meetingsThisWeek: [makeHomeMeeting({ id: 1, studentId: 10, studentName: 'Ada Lovelace' })],
      obligations: [makeHomeObligation({ schoolStudentId: 11, studentName: 'Bob Babbage' })],
      drafts: [makeHomeDraft({ instanceId: 200, studentId: 12, studentName: 'Cleo Curie' })],
    });

    const weekRow = screen.getByTestId('home-this-week-1');
    expect(weekRow.closest('a')).toHaveAttribute('href', '/educator/students/10');
    expect(weekRow).toHaveTextContent('Ada Lovelace');

    const dueRow = screen.getByTestId('home-due-soon-11-AnnualReview');
    expect(dueRow.closest('a')).toHaveAttribute('href', '/educator/students/11');

    const draftRow = screen.getByTestId('home-drafts-200');
    expect(draftRow.closest('a')).toHaveAttribute('href', '/educator/documents/200');
    expect(draftRow).toHaveTextContent('62% complete');
  });

  it('shows empty hints (not errors) for sections with nothing to show', () => {
    renderHome();

    expect(screen.getByTestId('home-this-week-empty')).toHaveTextContent(
      'Nothing scheduled this week.'
    );
    expect(screen.getByTestId('home-due-soon-empty')).toBeInTheDocument();
    expect(screen.getByTestId('home-drafts-empty')).toBeInTheDocument();
    expect(screen.getByTestId('home-shared-awaiting-family-empty')).toBeInTheDocument();
    expect(screen.getByTestId('home-family-responses-empty')).toBeInTheDocument();
    expect(screen.getByTestId('home-provider-requests-empty')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('puts "Provider requests I owe" first for the Provider variant', () => {
    renderHome({ variant: 'Provider' });
    const sections = screen.getAllByRole('heading', { level: 2 }).map((h) => h.textContent);
    expect(sections[0]).toBe('Provider requests I owe');
  });

  it('keeps the default order (This week first) for CaseManager/GeneralEducator', () => {
    renderHome({ variant: 'GeneralEducator' });
    const sections = screen.getAllByRole('heading', { level: 2 }).map((h) => h.textContent);
    expect(sections[0]).toBe('This week');
    expect(sections[sections.length - 1]).toBe('Provider requests I owe');
  });
});
