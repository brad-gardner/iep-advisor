namespace IepAssistant.Domain.Entities;

public enum AnalysisSourceType
{
    IepDocument = 0,
    EtrDocument = 1,
    ProgressReport = 2,

    /// <summary>Plan 6: a frozen <see cref="SharedDraftRevision"/> — feeds the lightweight
    /// <c>DraftExplanationService</c>/<c>DraftQuestionService</c> paths, never the heavy AnalysisRun pipeline.</summary>
    SharedDraftRevision = 3
}
