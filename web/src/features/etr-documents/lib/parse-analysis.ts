import type {
  AssessmentCompleteness,
  EligibilityReview,
  EtrAnalysis,
  EtrRedFlag,
  EtrSuggestedQuestion,
} from '../types';

export interface ParsedEtrAnalysis {
  assessmentCompleteness: AssessmentCompleteness | null;
  eligibilityReview: EligibilityReview | null;
  redFlags: EtrRedFlag[];
  suggestedQuestions: EtrSuggestedQuestion[];
  overallSummary: string;
}

function normalizeAssessmentCompleteness(
  value: AssessmentCompleteness | null | undefined
): AssessmentCompleteness | null {
  if (!value) return null;
  return {
    evaluated_domains: Array.isArray(value.evaluated_domains) ? value.evaluated_domains : [],
    missing_domains: Array.isArray(value.missing_domains) ? value.missing_domains : [],
    overall_completeness_rating: value.overall_completeness_rating || 'adequate',
  };
}

function normalizeEligibilityReview(
  value: EligibilityReview | null | undefined
): EligibilityReview | null {
  if (!value) return null;
  return {
    stated_category: value.stated_category || '',
    stated_conclusion: value.stated_conclusion || '',
    data_supports_conclusion: Boolean(value.data_supports_conclusion),
    supporting_evidence: Array.isArray(value.supporting_evidence) ? value.supporting_evidence : [],
    contradicting_evidence: Array.isArray(value.contradicting_evidence)
      ? value.contradicting_evidence
      : [],
    alternative_considerations: Array.isArray(value.alternative_considerations)
      ? value.alternative_considerations
      : [],
    notes: value.notes,
  };
}

export function parseEtrAnalysis(analysis: EtrAnalysis): ParsedEtrAnalysis {
  return {
    assessmentCompleteness: normalizeAssessmentCompleteness(analysis.assessmentCompleteness),
    eligibilityReview: normalizeEligibilityReview(analysis.eligibilityReview),
    redFlags: Array.isArray(analysis.overallRedFlags) ? analysis.overallRedFlags : [],
    suggestedQuestions: Array.isArray(analysis.suggestedQuestions)
      ? analysis.suggestedQuestions
      : [],
    overallSummary: analysis.overallSummary || '',
  };
}
