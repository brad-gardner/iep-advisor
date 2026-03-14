import type { SectionAnalysis } from '@/types/api';
import { RedFlagCard } from './red-flag-card';

interface AnalysisSectionDetailProps {
  sectionAnalysis: SectionAnalysis;
}

const SECTION_LABELS: Record<string, string> = {
  student_profile: 'Student Profile',
  present_levels: 'Present Levels of Performance',
  evaluations: 'Evaluations',
  assessments: 'Assessments & Test Scores',
  eligibility: 'Eligibility',
  annual_goals: 'Annual Goals',
  services: 'Services',
  accommodations: 'Accommodations',
  placement: 'Placement',
  transition: 'Transition Planning',
  progress_monitoring: 'Progress Monitoring',
  other: 'Other',
};

export function AnalysisSectionDetail({ sectionAnalysis }: AnalysisSectionDetailProps) {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold mb-3 text-gray-900">
          {SECTION_LABELS[sectionAnalysis.sectionType] || sectionAnalysis.sectionType}
        </h2>
        <p className="text-gray-600 leading-relaxed">
          {sectionAnalysis.plainLanguageSummary}
        </p>
      </div>

      {sectionAnalysis.keyPoints.length > 0 && (
        <div>
          <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wide mb-2">
            Key Points
          </h3>
          <ul className="space-y-1.5">
            {sectionAnalysis.keyPoints.map((point, i) => (
              <li key={i} className="flex gap-2 text-sm text-gray-600">
                <span className="text-blue-600 shrink-0">-</span>
                <span>{point}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {sectionAnalysis.redFlags.length > 0 && (
        <div>
          <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wide mb-2">
            Concerns
          </h3>
          <div className="space-y-2">
            {sectionAnalysis.redFlags.map((flag, i) => (
              <RedFlagCard key={i} redFlag={flag} />
            ))}
          </div>
        </div>
      )}

      {sectionAnalysis.suggestedQuestions.length > 0 && (
        <div>
          <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wide mb-2">
            Questions to Ask
          </h3>
          <ul className="space-y-1.5">
            {sectionAnalysis.suggestedQuestions.map((q, i) => (
              <li key={i} className="flex gap-2 text-sm text-gray-600">
                <span className="text-blue-600 shrink-0">?</span>
                <span>{q}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {sectionAnalysis.legalReferences.length > 0 && (
        <div>
          <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wide mb-2">
            Related Legal Provisions
          </h3>
          <div className="space-y-2">
            {sectionAnalysis.legalReferences.map((ref, i) => (
              <div key={i} className="bg-gray-50 rounded p-3 border border-gray-200">
                <p className="text-sm font-medium text-blue-600">{ref.provision}</p>
                <p className="text-xs text-gray-500 mt-1">{ref.summary}</p>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
