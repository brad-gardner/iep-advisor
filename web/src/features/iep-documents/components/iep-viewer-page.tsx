import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import type { IepDocument, IepSection } from '@/types/api';
import { getIepDocument, getIepSections, getDownloadUrl, reprocessIep } from '../api/iep-documents-api';
import { useIepAnalysis } from '../hooks/use-iep-analysis';
import { useAdvocacyGoals } from '@/features/advocacy-goals/hooks/use-advocacy-goals';
import { AnalysisTab } from './analysis-tab';

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
  const [activeTab, setActiveTab] = useState<'document' | 'analysis'>('document');

  const {
    analysis,
    isLoading: analysisLoading,
    isTriggering,
    trigger: triggerAnalysis,
    reload: reloadAnalysis,
  } = useIepAnalysis(documentId);

  const { goals: advocacyGoals } = useAdvocacyGoals(document?.childProfileId ?? 0);

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
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-gray-900" />
      </div>
    );
  }

  if (!document) {
    return (
      <div className="text-center py-12">
        <p className="text-gray-500">Document not found.</p>
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
            className="text-sm text-gray-500 hover:text-gray-900"
          >
            Back to child
          </Link>
          <h1 className="text-2xl font-bold mt-1 text-gray-900">{document.fileName}</h1>
        </div>
        <div className="flex gap-2">
          <button
            onClick={handleDownload}
            className="px-3 py-1.5 bg-gray-200 hover:bg-gray-300 rounded text-sm transition-colors text-gray-700"
          >
            Download PDF
          </button>
          {(document.status === 'error' || document.status === 'uploaded') && (
            <button
              onClick={handleReprocess}
              className="px-3 py-1.5 bg-blue-600 hover:bg-blue-700 rounded text-sm text-white transition-colors"
            >
              Process
            </button>
          )}
        </div>
      </div>

      {document.status === 'processing' && (
        <div className="bg-yellow-50 border border-yellow-400 rounded p-4 flex items-center gap-3">
          <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-yellow-500" />
          <p className="text-yellow-700 text-sm">
            Document is being processed. This may take a minute...
          </p>
        </div>
      )}

      {document.status === 'error' && (
        <div className="bg-red-50 border border-red-300 rounded p-4">
          <p className="text-red-600 text-sm">
            Processing failed. Try re-uploading or click Process to retry.
          </p>
        </div>
      )}

      {document.status === 'uploaded' && (
        <div className="bg-gray-50 rounded p-4 border border-gray-200">
          <p className="text-gray-500 text-sm">
            Document uploaded but not yet processed. Click Process to extract and analyze.
          </p>
        </div>
      )}

      {document.status === 'parsed' && sections.length > 0 && (
        <>
          {/* Tab bar */}
          <div className="flex border-b border-gray-200">
            <button
              onClick={() => setActiveTab('document')}
              className={`px-4 py-2 text-sm font-medium transition-colors ${
                activeTab === 'document'
                  ? 'text-gray-900 border-b-2 border-blue-500'
                  : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              Document
            </button>
            <button
              onClick={() => setActiveTab('analysis')}
              className={`px-4 py-2 text-sm font-medium transition-colors ${
                activeTab === 'analysis'
                  ? 'text-gray-900 border-b-2 border-blue-500'
                  : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              Analysis
              {analysis?.status === 'completed' && (
                <span className="ml-2 inline-block w-2 h-2 rounded-full bg-green-500" />
              )}
            </button>
          </div>

          {/* Tab content */}
          {activeTab === 'document' && (
            <div className="flex gap-4 min-h-[500px]">
              {/* Section nav */}
              <nav className="w-56 shrink-0 space-y-1">
                {sections.map((s) => (
                  <button
                    key={s.id}
                    onClick={() => setActiveSectionId(s.id)}
                    className={`w-full text-left px-3 py-2 rounded text-sm transition-colors ${
                      activeSectionId === s.id
                        ? 'bg-blue-600 text-white'
                        : 'text-gray-600 hover:bg-gray-100'
                    }`}
                  >
                    {SECTION_LABELS[s.sectionType] || s.sectionType}
                    {s.goals.length > 0 && (
                      <span className="ml-2 text-xs opacity-70">({s.goals.length})</span>
                    )}
                  </button>
                ))}
              </nav>

              {/* Section content */}
              <div className="flex-1 bg-white rounded-lg p-6 overflow-y-auto shadow-sm border border-gray-200">
                {currentSection && (
                  <div>
                    <h2 className="text-xl font-semibold mb-4 text-gray-900">
                      {SECTION_LABELS[currentSection.sectionType] || currentSection.sectionType}
                    </h2>

                    {currentSection.rawText && (
                      <div className="text-gray-600 whitespace-pre-wrap text-sm leading-relaxed">
                        {currentSection.rawText}
                      </div>
                    )}

                    {currentSection.goals.length > 0 && (
                      <div className="mt-6 space-y-4">
                        <h3 className="text-lg font-medium text-gray-900">
                          Goals ({currentSection.goals.length})
                        </h3>
                        {currentSection.goals.map((goal) => (
                          <div key={goal.id} className="bg-gray-50 rounded p-4 space-y-2 border border-gray-200">
                            <p className="font-medium text-gray-900">{goal.goalText}</p>
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-sm">
                              {goal.domain && (
                                <div>
                                  <span className="text-gray-500">Domain: </span>
                                  <span className="text-gray-700">{goal.domain}</span>
                                </div>
                              )}
                              {goal.baseline && (
                                <div>
                                  <span className="text-gray-500">Baseline: </span>
                                  <span className="text-gray-700">{goal.baseline}</span>
                                </div>
                              )}
                              {goal.targetCriteria && (
                                <div>
                                  <span className="text-gray-500">Target: </span>
                                  <span className="text-gray-700">{goal.targetCriteria}</span>
                                </div>
                              )}
                              {goal.measurementMethod && (
                                <div>
                                  <span className="text-gray-500">Measurement: </span>
                                  <span className="text-gray-700">{goal.measurementMethod}</span>
                                </div>
                              )}
                              {goal.timeframe && (
                                <div>
                                  <span className="text-gray-500">Timeframe: </span>
                                  <span className="text-gray-700">{goal.timeframe}</span>
                                </div>
                              )}
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )}
              </div>
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
        </>
      )}
    </div>
  );
}
