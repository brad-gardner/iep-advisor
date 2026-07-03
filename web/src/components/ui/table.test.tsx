import { render, screen, fireEvent, within } from '@testing-library/react';
import { vi } from 'vitest';
import { Table, type TableColumn } from './table';

interface Row {
  id: number;
  name: string;
  students: number;
}

const rows: Row[] = [
  { id: 1, name: 'Banneker', students: 12 },
  { id: 2, name: 'Adams', students: 30 },
  { id: 3, name: 'Carver', students: 5 },
];

const columns: TableColumn<Row>[] = [
  { key: 'name', header: 'School', cell: (r) => r.name, sortValue: (r) => r.name },
  {
    key: 'students',
    header: 'Students',
    align: 'right',
    cell: (r) => r.students,
    sortValue: (r) => r.students,
  },
];

function names() {
  return screen
    .getAllByRole('row')
    .slice(1) // drop the header row
    .map((row) => within(row).getAllByRole('cell')[0].textContent);
}

describe('Table', () => {
  it('marks only the active column with aria-sort and orders by it', () => {
    render(
      <Table
        label="Schools"
        columns={columns}
        rows={rows}
        rowKey={(r) => r.id}
        defaultSort={{ key: 'name', direction: 'asc' }}
      />,
    );
    const nameHeader = screen.getByRole('columnheader', { name: /School/ });
    const studentsHeader = screen.getByRole('columnheader', { name: /Students/ });
    expect(nameHeader).toHaveAttribute('aria-sort', 'ascending');
    expect(studentsHeader).not.toHaveAttribute('aria-sort');
    expect(names()).toEqual(['Adams', 'Banneker', 'Carver']);
  });

  it('toggles sort direction and moves aria-sort between columns', () => {
    render(
      <Table
        label="Schools"
        columns={columns}
        rows={rows}
        rowKey={(r) => r.id}
        data-testid="schools"
        defaultSort={{ key: 'name', direction: 'asc' }}
      />,
    );
    // Sort by students ascending.
    fireEvent.click(screen.getByTestId('schools-sort-students'));
    expect(names()).toEqual(['Carver', 'Banneker', 'Adams']);
    expect(screen.getByRole('columnheader', { name: /Students/ })).toHaveAttribute(
      'aria-sort',
      'ascending',
    );
    expect(screen.getByRole('columnheader', { name: /School/ })).not.toHaveAttribute('aria-sort');
    // Toggle to descending.
    fireEvent.click(screen.getByTestId('schools-sort-students'));
    expect(names()).toEqual(['Adams', 'Banneker', 'Carver']);
    expect(screen.getByRole('columnheader', { name: /Students/ })).toHaveAttribute(
      'aria-sort',
      'descending',
    );
  });

  it('renders skeleton rows while loading and the empty slot when empty', () => {
    const { rerender } = render(
      <Table label="Schools" columns={columns} rows={[]} rowKey={(r) => r.id} loading loadingRows={3} />,
    );
    // 3 skeleton rows + header.
    expect(screen.getAllByRole('row')).toHaveLength(4);

    rerender(
      <Table
        label="Schools"
        columns={columns}
        rows={[]}
        rowKey={(r) => r.id}
        empty={<span data-testid="no-schools">No schools yet</span>}
      />,
    );
    expect(screen.getByTestId('no-schools')).toBeInTheDocument();
  });

  it('exposes the scroll container as a labelled region', () => {
    render(<Table label="Schools" columns={columns} rows={rows} rowKey={(r) => r.id} />);
    expect(screen.getByRole('region', { name: 'Schools' })).toBeInTheDocument();
  });

  it('kebab actions do not trigger row-click navigation', () => {
    const onRowClick = vi.fn();
    const onEdit = vi.fn();
    render(
      <Table
        label="Schools"
        columns={columns}
        rows={rows}
        rowKey={(r) => r.id}
        onRowClick={onRowClick}
        rowActionLabel={(r) => r.name}
        rowActions={(r) => [
          { label: 'Edit', onSelect: () => onEdit(r.id), 'data-testid': `edit-${r.id}` },
        ]}
      />,
    );
    // Open the first row's kebab — row navigation must not fire.
    fireEvent.click(screen.getAllByRole('button', { name: /Actions for/ })[0]);
    expect(onRowClick).not.toHaveBeenCalled();
    // Selecting an action runs it, still without navigating.
    fireEvent.click(screen.getAllByTestId(/^edit-/)[0]);
    expect(onEdit).toHaveBeenCalledTimes(1);
    expect(onRowClick).not.toHaveBeenCalled();
  });

  it('navigates on a row-body click', () => {
    const onRowClick = vi.fn();
    render(
      <Table
        label="Schools"
        columns={columns}
        rows={rows}
        rowKey={(r) => r.id}
        onRowClick={onRowClick}
        defaultSort={{ key: 'name', direction: 'asc' }}
      />,
    );
    fireEvent.click(screen.getByText('Adams'));
    expect(onRowClick).toHaveBeenCalledWith(rows[1]);
  });
});
