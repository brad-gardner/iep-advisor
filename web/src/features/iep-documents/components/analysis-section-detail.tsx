import { CheckCircle, HelpCircle, Scale } from 'lucide-react';
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
        <h2 className="font-serif text-[22px] font-semibold mb-3 text-brand-slate-800">
          {SECTION_LABELS[sectionAnalysis.sectionType] || sectionAnalysis.sectionType}
        </h2>
        <p className="text-brand-slate-600 text-sm leading-relaxed">
          {sectionAnalysis.plainLanguageSummary}
        </p>
      </div>

      {sectionAnalysis.keyPoints.length > 0 && (
        <div>
          <h3 className="text-[10px] font-semibold text-brand-teal-500 uppercase tracking-wide mb-2">
            Key Points
          </h3>
          <ul className="space-y-1.5">
            {sectionAnalysis.keyPoints.map((point, i) => (
              <li key={i} className="flex gap-2 text-sm text-brand-slate-600">
                <CheckCircle className="w-4 h-4 text-brand-teal-500 shrink-0 mt-0.5" strokeWidth={1.8} aria-hidden="true" />
                <span>{point}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {sectionAnalysis.redFlags.length > 0 && (
        <div>
          <h3 className="text-[10px] font-semibold text-brand-teal-500 uppercase tracking-wide mb-2">
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
          <h3 className="text-[10px] font-semibold text-brand-teal-500 uppercase tracking-wide mb-2">
            Questions to Ask
          </h3>
          <ul className="space-y-1.5">
            {sectionAnalysis.suggestedQuestions.map((q, i) => (
              <li key={i} className="flex gap-2 text-sm text-brand-slate-600">
                <HelpCircle className="w-4 h-4 text-brand-teal-500 shrink-0 mt-0.5" strokeWidth={1.8} aria-hidden="true" />
                <span>{q}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {sectionAnalysis.legalReferences.length > 0 && (
        <div>
          <h3 className="text-[10px] font-semibold text-brand-teal-500 uppercase tracking-wide mb-2">
            Related Legal Provisions
          </h3>
          <div className="space-y-2">
            {sectionAnalysis.legalReferences.map((ref, i) => (
              <div key={i} className="bg-brand-slate-50 rounded-card p-3 border-[0.5px] border-brand-slate-200">
                <div className="flex items-center gap-1.5 mb-1">
                  <Scale className="w-3.5 h-3.5 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
                  <p className="text-sm font-medium text-brand-teal-600">{ref.provision}</p>
                </div>
                <p className="text-[11px] text-brand-slate-400 mt-1">{ref.summary}</p>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
