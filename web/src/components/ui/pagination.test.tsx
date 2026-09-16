import { render, screen, fireEvent } from '@testing-library/react';
import { vi } from 'vitest';
import { Pagination } from './pagination';

describe('Pagination', () => {
  it('summarises the visible range and disables the edge buttons', () => {
    const onPageChange = vi.fn();
    render(
      <Pagination
        label="Students pagination"
        page={1}
        pageSize={25}
        total={60}
        onPageChange={onPageChange}
        data-testid="pager"
      />,
    );
    expect(screen.getByRole('navigation', { name: 'Students pagination' })).toBeInTheDocument();
    expect(screen.getByTestId('pager-summary')).toHaveTextContent('Showing 1–25 of 60');
    expect(screen.getByRole('button', { name: /previous/i })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: /next/i }));
    expect(onPageChange).toHaveBeenCalledWith(2);
  });

  it('renders nothing when a single page has no size picker', () => {
    const { container } = render(
      <Pagination label="x" page={1} pageSize={50} total={10} onPageChange={vi.fn()} />,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('emits a page-size change from the labelled picker', () => {
    const onPageSizeChange = vi.fn();
    render(
      <Pagination
        label="x"
        page={1}
        pageSize={25}
        total={10}
        onPageChange={vi.fn()}
        onPageSizeChange={onPageSizeChange}
      />,
    );
    fireEvent.change(screen.getByLabelText('Rows per page'), { target: { value: '100' } });
    expect(onPageSizeChange).toHaveBeenCalledWith(100);
  });
});
