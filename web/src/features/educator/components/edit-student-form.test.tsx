import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { makeStudent } from '../test/fixtures';
import { EditStudentForm } from './edit-student-form';

describe('EditStudentForm', () => {
  it('seeds from the student and submits a full-replacement payload (blanks → null)', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue({ success: true });
    render(<EditStudentForm student={makeStudent()} onSubmit={onSubmit} onCancel={vi.fn()} />);

    expect(screen.getByLabelText('First name *')).toHaveValue('Ada');
    expect(screen.getByLabelText('Date of birth')).toHaveValue('2015-03-04');
    expect(screen.getByLabelText('Grade')).toHaveValue('G5');

    await user.selectOptions(screen.getByLabelText('Grade'), 'G6');
    await user.selectOptions(screen.getByLabelText('Disability category'), 'Autism');
    await user.clear(screen.getByLabelText('Annual review due'));
    await user.type(screen.getByLabelText('ETR date'), '2026-05-01');
    await user.clear(screen.getByLabelText('Student ID'));
    await user.type(screen.getByLabelText('Student ID'), '  000999 ');
    await user.click(screen.getByTestId('edit-student-submit'));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    expect(onSubmit).toHaveBeenCalledWith({
      firstName: 'Ada',
      lastName: 'Lovelace',
      dateOfBirth: '2015-03-04',
      stateCode: 'OH',
      externalStudentId: '000999',
      gradeLevel: 'G6',
      disabilityCategory: 'Autism',
      homeLanguage: 'en',
      iepDate: '2026-01-10',
      annualReviewDueDate: null,
      etrDate: '2026-05-01',
      reevaluationDueDate: null,
    });
  });

  it('keeps the form open with the server error on failure', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue({
      success: false,
      error: 'Student ID already in use in this district.',
    });
    render(<EditStudentForm student={makeStudent()} onSubmit={onSubmit} onCancel={vi.fn()} />);
    await user.click(screen.getByTestId('edit-student-submit'));
    expect(await screen.findByText('Student ID already in use in this district.')).toBeInTheDocument();
    expect(screen.getByTestId('edit-student-form')).toBeInTheDocument();
  });

  it('requires a first name before calling the API', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<EditStudentForm student={makeStudent()} onSubmit={onSubmit} onCancel={vi.fn()} />);
    await user.clear(screen.getByLabelText('First name *'));
    // `required` blocks native submit; drive the handler via the form directly.
    (screen.getByTestId('edit-student-form') as HTMLFormElement).noValidate = true;
    await user.click(screen.getByTestId('edit-student-submit'));
    expect(await screen.findByText('First name is required')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
