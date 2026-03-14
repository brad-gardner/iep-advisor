interface AdvocacyGoalsEmptyStateProps {
  childName: string;
  onAdd: () => void;
}

export function AdvocacyGoalsEmptyState({ childName, onAdd }: AdvocacyGoalsEmptyStateProps) {
  return (
    <div className="text-center py-8 px-4">
      <div className="text-3xl mb-3">
        <svg className="w-12 h-12 mx-auto text-gray-300" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
      </div>
      <h3 className="text-sm font-semibold text-gray-700 mb-1">
        Define your priorities for {childName}
      </h3>
      <p className="text-xs text-gray-500 max-w-sm mx-auto mb-4">
        When you analyze an IEP, we'll check whether these goals are addressed
        and flag any gaps.
      </p>
      <button
        onClick={onAdd}
        className="px-4 py-2 bg-blue-600 hover:bg-blue-700 rounded text-sm text-white transition-colors"
      >
        Add Your First Goal
      </button>
    </div>
  );
}
