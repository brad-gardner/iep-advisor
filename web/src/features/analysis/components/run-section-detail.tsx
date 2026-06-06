import type { SectionAnalysis } from "@/types/api";
import { AnalysisSectionDetail } from "@/features/iep-documents/components/analysis-section-detail";
import type { AnalysisRunSectionAnalysis } from "../types";

interface RunSectionDetailProps {
  section: AnalysisRunSectionAnalysis;
}

// Thin adapter: AnalysisRunSectionResult lacks `sectionType` and
// `suggestedQuestions` (questions live in Meeting Prep now), so we map
// `sectionKind` -> `sectionType` and pass an empty questions list to reuse the
// existing AnalysisSectionDetail presentational component.
export function RunSectionDetail({ section }: RunSectionDetailProps) {
  const adapted: SectionAnalysis = {
    sectionType: section.sectionKind,
    plainLanguageSummary: section.plainLanguageSummary,
    keyPoints: section.keyPoints,
    redFlags: section.redFlags,
    suggestedQuestions: [],
    legalReferences: section.legalReferences,
  };

  return <AnalysisSectionDetail sectionAnalysis={adapted} />;
}
