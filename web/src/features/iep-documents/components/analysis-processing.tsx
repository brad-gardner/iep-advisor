interface AnalysisProcessingProps {
  onReload: () => void;
}

export function AnalysisProcessing({ onReload }: AnalysisProcessingProps) {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-4">
      <div className="animate-spin rounded-full h-10 w-10 border-b-2 border-blue-500 mb-4" />
      <h3 className="text-xl font-semibold text-gray-900 mb-2">Analyzing Your IEP</h3>
      <p className="text-gray-500 text-center max-w-md mb-6">
        This typically takes 30-60 seconds. We're reviewing each section, evaluating
        goals against SMART criteria, and identifying areas that may need attention.
      </p>
      <button
        onClick={onReload}
        className="px-4 py-2 bg-gray-200 hover:bg-gray-300 rounded text-sm transition-colors text-gray-700"
      >
        Check Status
      </button>
    </div>
  );
}
