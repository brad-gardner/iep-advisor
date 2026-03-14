import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft, ChevronDown, ChevronUp, Download, Play } from 'lucide-react';
import type { IepDocument, IepSection } from '@/types/api';
import { getIepDocument, getIepSections, getDownloadUrl, reprocessIep } from '../api/iep-documents-api';
import { Badge } from '@/components/ui/badge';
import { useIepAnalysis } from '../hooks/use-iep-analysis';
import { useAdvocacyGoals } from '@/features/advocacy-goals/hooks/use-advocacy-goals';
import { useMeetingPrep } from '@/features/meeting-prep/hooks/use-meeting-prep';
import { AnalysisTab } from './analysis-tab';
import { MeetingPrepTab } from '@/features/meeting-prep/components/meeting-prep-tab';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';

const MEETING_TYPE_LABELS: Record<string, string> = {
  initial: 'Initial IEP',
  annual_review: 'Annual Review',
  amendment: 'Amendment',
  reevaluation: 'Reevaluation',
};

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

export function IepViewerPage() {
  const { id } = useParams<{ id: string }>();
  const documentId = Number(id);
  const [document, setDocument] = useState<IepDocument | null>(null);
  const [sections, setSections] = useState<IepSection[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [activeSectionId, setActiveSectionId] = useState<number | null>(null);
  const [activeTab, setActiveTab] = useState<'document' | 'analysis' | 'meeting-prep'>('document');
  const [notesExpanded, setNotesExpanded] = useState(false);

  const {
    analysis,
    isLoading: analysisLoading,
    isTriggering,
    trigger: triggerAnalysis,
    reload: reloadAnalysis,
  } = useIepAnalysis(documentId);

  const { goals: advocacyGoals } = useAdvocacyGoals(document?.childProfileId ?? 0);

  const {
    checklist: meetingPrepChecklist,
    isLoading: meetingPrepLoading,
    isGenerating: meetingPrepGenerating,
    generateFromIep: generateMeetingPrep,
    reload: reloadMeetingPrep,
  } = useMeetingPrep(document?.childProfileId ?? 0, documentId);

  useEffect(() => {
    async function load() {
      try {
        const docRes = await getIepDocument(documentId);
        if (docRes.success && docRes.data) {
          setDocument(docRes.data);

          if (docRes.data.status === 'parsed') {
            const secRes = await getIepSections(documentId);
            if (secRes.success && secRes.data) {
              setSections(secRes.data);
              if (secRes.data.length > 0) {
                setActiveSectionId(secRes.data[0].id);
              }
            }
          }
        }
      } catch {
        // handled
      } finally {
        setIsLoading(false);
      }
    }
    load();
  }, [documentId]);

  const handleDownload = async () => {
    const res = await getDownloadUrl(documentId);
    if (res.success && res.data) {
      window.open(res.data.url, '_blank');
    }
  };

  const handleReprocess = async () => {
    await reprocessIep(documentId);
    setDocument((prev) => (prev ? { ...prev, status: 'processing' } : prev));
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (!document) {
    return (
      <div className="text-center py-12">
        <p className="text-brand-slate-400">Document not found.</p>
      </div>
    );
  }

  const currentSection = sections.find((s) => s.id === activeSectionId);

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-center">
        <div>
          <Link
            to={`/children/${document.childProfileId}`}
            className="inline-flex items-center gap-1.5 text-[13px] font-medium text-brand-slate-400 hover:text-brand-teal-500 transition-colors"
          >
            <ArrowLeft className="w-4 h-4" strokeWidth={1.8} aria-hidden="true" />
            Back to child
          </Link>
          <h1 className="font-serif text-[32px] font-semibold leading-tight mt-1 text-brand-slate-800">
            {document.fileName || (document.meetingType ? MEETING_TYPE_LABELS[document.meetingType] || document.meetingType : `IEP #${document.id}`)}
          </h1>
          <div className="flex items-center gap-3 mt-2 flex-wrap">
            {document.meetingType && (
              <Badge variant="neutral">
                {MEETING_TYPE_LABELS[document.meetingType] || document.meetingType}
              </Badge>
            )}
            {document.iepDate && (
              <span className="text-[13px] text-brand-slate-500">
                {new Date(document.iepDate).toLocaleDateString()}
              </span>
            )}
            {document.attendees && (
              <span className="text-[13px] text-brand-slate-500">
                Attendees: {document.attendees}
              </span>
            )}
          </div>
          {document.notes && (
            <div className="mt-2">
              <button
                onClick={() => setNotesExpanded(!notesExpanded)}
                className="inline-flex items-center gap-1 text-[13px] font-medium text-brand-slate-500 hover:text-brand-teal-500 transition-colors"
              >
                {notesExpanded ? (
                  <ChevronUp className="w-3.5 h-3.5" strokeWidth={1.8} aria-hidden="true" />
                ) : (
                  <ChevronDown className="w-3.5 h-3.5" strokeWidth={1.8} aria-hidden="true" />
                )}
                Notes
              </button>
              {notesExpanded && (
                <p className="mt-1 text-sm text-brand-slate-600 whitespace-pre-wrap bg-brand-slate-50 rounded-card p-3 border-[0.5px] border-brand-slate-200">
                  {document.notes}
                </p>
              )}
            </div>
          )}
        </div>
        <div className="flex gap-2">
          <Button variant="ghost" onClick={handleDownload}>
            <Download className="w-4 h-4 mr-1.5" strokeWidth={1.8} aria-hidden="true" />
            Download PDF
          </Button>
          {(document.status === 'error' || document.status === 'uploaded') && (
            <Button onClick={handleReprocess}>
              <Play className="w-4 h-4 mr-1.5" strokeWidth={1.8} aria-hidden="true" />
              Process
            </Button>
          )}
        </div>
      </div>

      {document.status === 'processing' && (
        <Notice variant="warning" title="Processing">
          Document is being processed. This may take a minute...
        </Notice>
      )}

      {document.status === 'error' && (
        <Notice variant="error" title="Processing failed">
          Try re-uploading or click Process to retry.
        </Notice>
      )}

      {document.status === 'uploaded' && (
        <Notice variant="info" title="Not yet processed">
          Document uploaded but not yet processed. Click Process to extract and analyze.
        </Notice>
      )}

      {document.status === 'parsed' && sections.length > 0 && (
        <>
          {/* Tab bar */}
          <div className="flex border-b border-brand-slate-200">
            <button
              onClick={() => setActiveTab('document')}
              className={`px-4 py-2 text-[13px] font-medium transition-colors ${
                activeTab === 'document'
                  ? 'text-brand-slate-800 border-b-2 border-brand-teal-500'
                  : 'text-brand-slate-400 hover:text-brand-slate-800'
              }`}
            >
              Document
            </button>
            <button
              onClick={() => setActiveTab('analysis')}
              className={`px-4 py-2 text-[13px] font-medium transition-colors ${
                activeTab === 'analysis'
                  ? 'text-brand-slate-800 border-b-2 border-brand-teal-500'
                  : 'text-brand-slate-400 hover:text-brand-slate-800'
              }`}
            >
              Analysis
              {analysis?.status === 'completed' && (
                <span className="ml-2 inline-block w-2 h-2 rounded-full bg-brand-teal-500" />
              )}
            </button>
            <button
              onClick={() => setActiveTab('meeting-prep')}
              className={`px-4 py-2 text-[13px] font-medium transition-colors ${
                activeTab === 'meeting-prep'
                  ? 'text-brand-slate-800 border-b-2 border-brand-teal-500'
                  : 'text-brand-slate-400 hover:text-brand-slate-800'
              }`}
            >
              Meeting Prep
              {meetingPrepChecklist?.status === 'completed' && (
                <span className="ml-2 inline-block w-2 h-2 rounded-full bg-brand-teal-500" />
              )}
            </button>
          </div>

          {/* Tab content */}
          {activeTab === 'document' && (
            <div className="flex gap-4 min-h-[500px]">
              {/* Section nav */}
              <nav className="w-56 shrink-0 space-y-0.5">
                {sections.map((s) => (
                  <button
                    key={s.id}
                    onClick={() => setActiveSectionId(s.id)}
                    className={`w-full text-left px-3 py-2 rounded-button text-[13px] font-medium transition-colors ${
                      activeSectionId === s.id
                        ? 'bg-brand-teal-50 text-brand-teal-600 border-l-2 border-l-brand-teal-500'
                        : 'text-brand-slate-600 hover:bg-brand-slate-50'
                    }`}
                  >
                    {SECTION_LABELS[s.sectionType] || s.sectionType}
                    {s.goals.length > 0 && (
                      <span className="ml-2 text-[11px] opacity-70">({s.goals.length})</span>
                    )}
                  </button>
                ))}
              </nav>

              {/* Section content */}
              <Card className="flex-1 overflow-y-auto">
                {currentSection && (
                  <div>
                    <h2 className="font-serif text-[22px] font-semibold mb-4 text-brand-slate-800">
                      {SECTION_LABELS[currentSection.sectionType] || currentSection.sectionType}
                    </h2>

                    {currentSection.rawText && (
                      <div className="text-brand-slate-600 whitespace-pre-wrap text-sm leading-relaxed">
                        {currentSection.rawText}
                      </div>
                    )}

                    {currentSection.goals.length > 0 && (
                      <div className="mt-6 space-y-4">
                        <h3 className="font-serif text-[17px] font-semibold text-brand-slate-800">
                          Goals ({currentSection.goals.length})
                        </h3>
                        {currentSection.goals.map((goal) => (
                          <div key={goal.id} className="bg-brand-slate-50 rounded-card p-4 space-y-2 border-[0.5px] border-brand-slate-200">
                            <p className="font-medium text-sm text-brand-slate-800">{goal.goalText}</p>
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-sm">
                              {goal.domain && (
                                <div>
                                  <span className="text-brand-slate-400">Domain: </span>
                                  <span className="text-brand-slate-600">{goal.domain}</span>
                                </div>
                              )}
                              {goal.baseline && (
                                <div>
                                  <span className="text-brand-slate-400">Baseline: </span>
                                  <span className="text-brand-slate-600">{goal.baseline}</span>
                                </div>
                              )}
                              {goal.targetCriteria && (
                                <div>
                                  <span className="text-brand-slate-400">Target: </span>
                                  <span className="text-brand-slate-600">{goal.targetCriteria}</span>
                                </div>
                              )}
                              {goal.measurementMethod && (
                                <div>
                                  <span className="text-brand-slate-400">Measurement: </span>
                                  <span className="text-brand-slate-600">{goal.measurementMethod}</span>
                                </div>
                              )}
                              {goal.timeframe && (
                                <div>
                                  <span className="text-brand-slate-400">Timeframe: </span>
                                  <span className="text-brand-slate-600">{goal.timeframe}</span>
                                </div>
                              )}
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )}
              </Card>
            </div>
          )}

          {activeTab === 'analysis' && (
            <AnalysisTab
              analysis={analysis}
              isLoading={analysisLoading}
              isTriggering={isTriggering}
              advocacyGoals={advocacyGoals}
              onTrigger={triggerAnalysis}
              onReload={reloadAnalysis}
            />
          )}

          {activeTab === 'meeting-prep' && (
            <MeetingPrepTab
              checklist={meetingPrepChecklist}
              isLoading={meetingPrepLoading}
              isGenerating={meetingPrepGenerating}
              onGenerate={() => generateMeetingPrep(documentId)}
              onReload={reloadMeetingPrep}
            />
          )}
        </>
      )}
    </div>
  );
}
