import { describe, expect, it } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { AskAdvocateButton } from './ask-advocate-button';

function Probe() {
  const location = useLocation();
  const label = (location.state as { aboutLabel?: string } | null)?.aboutLabel ?? '';
  return (
    <output data-testid="location">
      {location.pathname + location.search}|{label}
    </output>
  );
}

function renderAt(ui: React.ReactNode) {
  return render(
    <MemoryRouter initialEntries={['/children/4/ieps/12']}>
      <Routes>
        <Route path="/children/:childId/ieps/:id" element={ui} />
        <Route path="/children/:childId/advocate" element={<Probe />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('AskAdvocateButton', () => {
  it('links to a fresh advocate conversation about the record and carries the human label in router state', () => {
    renderAt(<AskAdvocateButton childId={4} about={{ kind: 'iep', id: 12 }} label="IEP from Mar 3, 2026" />);
    const link = screen.getByRole('link', { name: 'Ask the advocate' });
    expect(link).toHaveAttribute('href', '/children/4/advocate?about=iep%3A12');
    expect(link).toHaveAccessibleDescription('Opens a private conversation with the Virtual Advocate about this item.');
    fireEvent.click(link);
    expect(screen.getByTestId('location')).toHaveTextContent('/children/4/advocate?about=iep%3A12|IEP from Mar 3, 2026');
  });

  it('keeps an accessible name in icon mode', () => {
    renderAt(<AskAdvocateButton childId={4} about={{ kind: 'goal', id: 340 }} appearance="icon" ariaLabel="Ask the advocate about this reading goal" />);
    const link = screen.getByRole('link', { name: 'Ask the advocate about this reading goal' });
    expect(link).toHaveAttribute('href', '/children/4/advocate?about=goal%3A340');
    expect(link).toHaveAttribute('data-about', 'goal:340');
  });

  it('renders nothing for a viewer who cannot ask', () => {
    renderAt(<AskAdvocateButton childId={4} about={{ kind: 'etr', id: 5 }} canAsk={false} />);
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });
});
