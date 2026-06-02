export type WorkspaceTab = 'goals' | 'services' | 'accommodations' | 'transition' | 'narrative';

export const WORKSPACE_TABS: { id: WorkspaceTab; label: string }[] = [
  { id: 'goals', label: 'Goals' },
  { id: 'services', label: 'Services' },
  { id: 'accommodations', label: 'Accommodations' },
  { id: 'transition', label: 'Transition' },
  { id: 'narrative', label: 'Narrative' },
];
