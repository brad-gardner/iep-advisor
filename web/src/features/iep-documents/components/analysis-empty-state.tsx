interface AnalysisEmptyStateProps {
  onTrigger: () => void;
  isTriggering: boolean;
}

export function AnalysisEmptyState({ onTrigger, isTriggering }: AnalysisEmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-4">
      <div className="text-4xl mb-4">&#x1F50D;</div>
      <h3 className="text-xl font-semibold text-gray-900 mb-2">Analyze Your IEP</h3>
      <p className="text-gray-500 text-center max-w-md mb-6">
        Get a comprehensive analysis of your child's IEP, including plain-language
        explanations, goal evaluations, potential concerns, and suggested questions
        for your next IEP meeting.
      </p>
      <button
        onClick={onTrigger}
        disabled={isTriggering}
        className="px-6 py-2.5 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 rounded-lg font-medium text-white transition-colors"
      >
        {isTriggering ? 'Starting Analysis...' : 'Analyze IEP'}
      </button>
    </div>
  );
}
