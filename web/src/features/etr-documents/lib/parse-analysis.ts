import type {
  AssessmentCompleteness,
  EligibilityReview,
  EtrAnalysis,
  EtrRedFlag,
  EtrSuggestedQuestion,
} from '../types';

function safeParse<T>(raw: string | null | undefined): T | null {
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw);
    return parsed as T;
  } catch {
    return null;
  }
}

export function parseAssessmentCompleteness(
  raw: string | null | undefined
): AssessmentCompleteness | null {
  const parsed = safeParse<Partial<AssessmentCompleteness>>(raw);
  if (!parsed) return null;
  return {
    evaluated_domains: Array.isArray(parsed.evaluated_domains) ? parsed.evaluated_domains : [],
    missing_domains: Array.isArray(parsed.missing_domains) ? parsed.missing_domains : [],
    overall_completeness_rating: parsed.overall_completeness_rating || 'adequate',
  };
}

export function parseEligibilityReview(
  raw: string | null | undefined
): EligibilityReview | null {
  const parsed = safeParse<Partial<EligibilityReview>>(raw);
  if (!parsed) return null;
  return {
    stated_category: parsed.stated_category || '',
    stated_conclusion: parsed.stated_conclusion || '',
    data_supports_conclusion: Boolean(parsed.data_supports_conclusion),
    supporting_evidence: Array.isArray(parsed.supporting_evidence) ? parsed.supporting_evidence : [],
    contradicting_evidence: Array.isArray(parsed.contradicting_evidence)
      ? parsed.contradicting_evidence
      : [],
    alternative_considerations: Array.isArray(parsed.alternative_considerations)
      ? parsed.alternative_considerations
      : [],
    notes: parsed.notes,
  };
}

export function parseRedFlags(raw: string | null | undefined): EtrRedFlag[] {
  const parsed = safeParse<EtrRedFlag[]>(raw);
  if (!parsed || !Array.isArray(parsed)) return [];
  return parsed;
}

export function parseSuggestedQuestions(
  raw: string | null | undefined
): EtrSuggestedQuestion[] {
  const parsed = safeParse<EtrSuggestedQuestion[]>(raw);
  if (!parsed || !Array.isArray(parsed)) return [];
  return parsed;
}

export interface ParsedEtrAnalysis {
  assessmentCompleteness: AssessmentCompleteness | null;
  eligibilityReview: EligibilityReview | null;
  redFlags: EtrRedFlag[];
  suggestedQuestions: EtrSuggestedQuestion[];
  overallSummary: string;
}

export function parseEtrAnalysis(analysis: EtrAnalysis): ParsedEtrAnalysis {
  return {
    assessmentCompleteness: parseAssessmentCompleteness(analysis.assessmentCompleteness),
    eligibilityReview: parseEligibilityReview(analysis.eligibilityReview),
    redFlags: parseRedFlags(analysis.overallRedFlags),
    suggestedQuestions: parseSuggestedQuestions(analysis.suggestedQuestions),
    overallSummary: analysis.overallSummary || '',
  };
}
