import { render, screen, fireEvent, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';
import { Table, type TableColumn } from './table';

function renderInRouter(ui: React.ReactElement) {
  return render(<MemoryRouter>{ui}</MemoryRouter>);
}

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
    renderInRouter(
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
    renderInRouter(
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
    const { rerender } = renderInRouter(
      <Table label="Schools" columns={columns} rows={[]} rowKey={(r) => r.id} loading loadingRows={3} />,
    );
    // 3 skeleton rows + header.
    expect(screen.getAllByRole('row')).toHaveLength(4);

    rerender(
      <MemoryRouter>
        <Table
          label="Schools"
          columns={columns}
          rows={[]}
          rowKey={(r) => r.id}
          empty={<span data-testid="no-schools">No schools yet</span>}
        />
      </MemoryRouter>,
    );
    expect(screen.getByTestId('no-schools')).toBeInTheDocument();
  });

  it('exposes the scroll container as a labelled region', () => {
    renderInRouter(<Table label="Schools" columns={columns} rows={rows} rowKey={(r) => r.id} />);
    expect(screen.getByRole('region', { name: 'Schools' })).toBeInTheDocument();
  });

  it('renders the first column as a keyboard-navigable row link', () => {
    renderInRouter(
      <Table
        label="Schools"
        columns={columns}
        rows={rows}
        rowKey={(r) => r.id}
        rowHref={(r) => `/schools/${r.id}`}
        defaultSort={{ key: 'name', direction: 'asc' }}
      />,
    );
    const link = screen.getByRole('link', { name: 'Adams' });
    expect(link).toHaveAttribute('href', '/schools/2');
  });

  it('keeps the kebab out of the row link so actions never navigate', () => {
    const onEdit = vi.fn();
    renderInRouter(
      <Table
        label="Schools"
        columns={columns}
        rows={rows}
        rowKey={(r) => r.id}
        rowHref={(r) => `/schools/${r.id}`}
        rowActionLabel={(r) => r.name}
        rowActions={(r) => [
          { label: 'Edit', onSelect: () => onEdit(r.id), 'data-testid': `edit-${r.id}` },
        ]}
      />,
    );
    // The kebab lives in its own cell, not inside the row's <Link>.
    const kebab = screen.getAllByRole('button', { name: /Actions for/ })[0];
    expect(kebab.closest('a')).toBeNull();
    fireEvent.click(kebab);
    fireEvent.click(screen.getAllByTestId(/^edit-/)[0]);
    expect(onEdit).toHaveBeenCalledTimes(1);
  });
});
