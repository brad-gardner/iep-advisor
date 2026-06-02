import { AccommodationsList } from './accommodations-list';
import { GoalsPanel } from './goals-panel';
import { NarrativeSectionsPanel } from './narrative-sections-panel';
import type { WorkspaceTab } from '../lib/workspace-tabs';
import { ServicesTable } from './services-table';
import { TransitionList } from './transition-list';

interface ActivePanelProps {
  tab: WorkspaceTab;
}

export function ActivePanel({ tab }: ActivePanelProps) {
  switch (tab) {
    case 'goals':
      return <GoalsPanel />;
    case 'services':
      return <ServicesTable />;
    case 'accommodations':
      return <AccommodationsList />;
    case 'transition':
      return <TransitionList />;
    case 'narrative':
      return <NarrativeSectionsPanel />;
  }
}
