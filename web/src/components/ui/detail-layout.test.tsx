import { render, screen } from '@testing-library/react';
import { DetailLayout } from './detail-layout';

describe('DetailLayout', () => {
  it('places main before sidebar in the DOM', () => {
    render(
      <DetailLayout
        data-testid="detail"
        main={<div data-testid="main">Main</div>}
        sidebar={<div data-testid="side">Sidebar</div>}
      />,
    );
    const root = screen.getByTestId('detail');
    const order = Array.from(root.querySelectorAll('[data-testid]')).map((n) =>
      n.getAttribute('data-testid'),
    );
    expect(order.indexOf('main')).toBeLessThan(order.indexOf('side'));
  });

  it('puts the sidebar in a complementary landmark', () => {
    render(
      <DetailLayout main={<div>Main</div>} sidebar={<div>Status</div>} />,
    );
    expect(screen.getByRole('complementary')).toHaveTextContent('Status');
  });
});
