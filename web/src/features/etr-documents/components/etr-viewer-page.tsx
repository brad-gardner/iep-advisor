import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, ChevronDown, ChevronUp, Download } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Button } from '@/components/ui/button';
import { useEtrDocument } from '../hooks/use-etr-documents';
import { useEtrProcessing } from '../hooks/use-etr-processing';
import { useEtrSections } from '../hooks/use-etr-sections';
import { getDownloadUrl } from '../api/etr-documents-api';
import {
  DOCUMENT_STATE_LABELS,
  EVALUATION_TYPE_LABELS,
} from '../types';
import { EtrUpload } from './etr-upload';
import { EtrProcessingBanner } from './etr-processing-banner';
import { EtrErrorBanner } from './etr-error-banner';
import { EtrSectionsList } from './etr-sections-list';
import { EtrAnalysisTab } from './etr-analysis-tab';
import { EtrMeetingPrepTab } from './etr-meeting-prep-tab';

type TabKey = 'overview' | 'sections' | 'analysis' | 'meeting-prep';

const IN_FLIGHT = new Set(['uploaded', 'processing']);

export function EtrViewerPage() {
  const { id } = useParams<{ id: string }>();
  const documentId = Number(id);
  const { etr: initialEtr, isLoading } = useEtrDocument(documentId);
  const { etr, status, isPolling, refresh: refreshEtr } = useEtrProcessing(
    documentId,
    initialEtr
  );
  const { sections, isLoading: sectionsLoading, error: sectionsError } = useEtrSections(
    documentId,
    status
  );

  const [activeTab, setActiveTab] = useState<TabKey>('overview');
  const [notesExpanded, setNotesExpanded] = useState(false);

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (!etr) {
    return (
      <div className="text-center py-12">
        <p className="text-brand-slate-400">ETR document not found.</p>
      </div>
    );
  }

  const headerTitle =
    etr.fileName ||
    (etr.evaluationType
      ? EVALUATION_TYPE_LABELS[etr.evaluationType] || etr.evaluationType
      : `ETR #${etr.id}`);

  const sectionsTabDisabled = etr.status !== 'parsed';
  const sectionsTabHint =
    etr.status === 'created'
      ? 'Upload a document to see sections'
      : etr.status === 'error'
        ? 'Processing failed'
        : IN_FLIGHT.has(etr.status)
          ? 'Processing...'
          : undefined;

  const meetingPrepTabDisabled = etr.status !== 'parsed';
  const meetingPrepTabHint =
    etr.status === 'created'
      ? 'Upload a document first'
      : etr.status === 'error'
        ? 'Processing failed'
        : IN_FLIGHT.has(etr.status)
          ? 'Processing...'
          : undefined;

  const analysisTabDisabled = etr.status !== 'parsed';
  const analysisTabHint =
    etr.status === 'created'
      ? 'Upload a document first'
      : etr.status === 'error'
        ? 'Processing failed'
        : IN_FLIGHT.has(etr.status)
          ? 'Processing...'
          : undefined;

  const TABS: { key: TabKey; label: string; disabled: boolean; hint?: string }[] = [
    { key: 'overview', label: 'Overview', disabled: false },
    {
      key: 'sections',
      label: 'Sections',
      disabled: sectionsTabDisabled,
      hint: sectionsTabHint,
    },
    {
      key: 'analysis',
      label: 'Analysis',
      disabled: analysisTabDisabled,
      hint: analysisTabHint,
    },
    {
      key: 'meeting-prep',
      label: 'Meeting Prep',
      disabled: meetingPrepTabDisabled,
      hint: meetingPrepTabHint,
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-start">
        <div>
          <Link
            to={`/children/${etr.childProfileId}`}
            className="inline-flex items-center gap-1.5 text-[13px] font-medium text-brand-slate-400 hover:text-brand-teal-500 transition-colors"
          >
            <ArrowLeft className="w-4 h-4" strokeWidth={1.8} aria-hidden="true" />
            Back to child
          </Link>
          <h1 className="font-serif text-[32px] font-semibold leading-tight mt-1 text-brand-slate-800">
            {headerTitle}
          </h1>
          <p className="text-[13px] text-brand-slate-500 mt-1">Evaluation Team Report</p>
          <div className="flex items-center gap-3 mt-2 flex-wrap">
            {etr.evaluationType && (
              <Badge variant="neutral">
                {EVALUATION_TYPE_LABELS[etr.evaluationType] || etr.evaluationType}
              </Badge>
            )}
            {etr.documentState && (
              <Badge variant={etr.documentState === 'final' ? 'success' : 'neutral'}>
                {DOCUMENT_STATE_LABELS[etr.documentState] || etr.documentState}
              </Badge>
            )}
            <Badge variant="neutral">{etr.status}</Badge>
            {etr.evaluationDate && (
              <span className="text-[13px] text-brand-slate-500">
                Evaluated: {new Date(etr.evaluationDate).toLocaleDateString()}
              </span>
            )}
            {isPolling && (
              <span
                className="inline-flex items-center gap-1 text-[11px] text-brand-amber-500"
                data-testid="etr-polling-indicator"
              >
                <span className="inline-block w-1.5 h-1.5 rounded-full bg-brand-amber-500 animate-pulse" />
                Refreshing...
              </span>
            )}
          </div>
          {etr.notes && (
            <div className="mt-2">
              <button
                onClick={() => setNotesExpanded(!notesExpanded)}
                className="inline-flex items-center gap-1 text-[13px] font-medium text-brand-slate-500 hover:text-brand-teal-500 transition-colors"
                data-testid="etr-notes-toggle"
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
                  {etr.notes}
                </p>
              )}
            </div>
          )}
        </div>
      </div>

      {IN_FLIGHT.has(etr.status) && <EtrProcessingBanner status={etr.status} />}
      {etr.status === 'error' && (
        <EtrErrorBanner etrId={documentId} onRetried={refreshEtr} />
      )}

      <div className="flex border-b border-brand-slate-200">
        {TABS.map((tab) => {
          const isActive = activeTab === tab.key;
          return (
            <button
              key={tab.key}
              onClick={() => !tab.disabled && setActiveTab(tab.key)}
              disabled={tab.disabled}
              data-testid={`etr-tab-${tab.key}`}
              aria-selected={isActive}
              title={tab.hint}
              className={`px-4 py-2 text-[13px] font-medium transition-colors ${
                isActive
                  ? 'text-brand-slate-800 border-b-2 border-brand-teal-500'
                  : tab.disabled
                    ? 'text-brand-slate-300 cursor-not-allowed'
                    : 'text-brand-slate-400 hover:text-brand-slate-800'
              }`}
            >
              {tab.label}
              {tab.disabled && tab.hint && (
                <span className="ml-2 text-[10px] uppercase tracking-wide text-brand-slate-300">
                  {tab.hint === 'Soon' ? 'Soon' : ''}
                </span>
              )}
            </button>
          );
        })}
      </div>

      {activeTab === 'overview' && (
        <OverviewTab etrId={documentId} etr={etr} onUploaded={refreshEtr} />
      )}

      {activeTab === 'sections' && !sectionsTabDisabled && (
        <EtrSectionsList
          sections={sections}
          isLoading={sectionsLoading}
          error={sectionsError}
        />
      )}

      {activeTab === 'analysis' && !analysisTabDisabled && (
        <EtrAnalysisTab
          etrId={documentId}
          childProfileId={etr.childProfileId}
        />
      )}

      {activeTab === 'meeting-prep' && !meetingPrepTabDisabled && (
        <EtrMeetingPrepTab
          etrId={documentId}
          childProfileId={etr.childProfileId}
        />
      )}
    </div>
  );
}

interface OverviewTabProps {
  etrId: number;
  etr: {
    status: string;
    fileName: string | null;
    evaluationDate: string | null;
    evaluationType: string | null;
    documentState: string;
    createdAt: string;
  };
  onUploaded: () => void | Promise<void>;
}

function OverviewTab({ etrId, etr, onUploaded }: OverviewTabProps) {
  const handleDownload = async () => {
    const res = await getDownloadUrl(etrId);
    if (res.success && res.data) {
      window.open(res.data.url, '_blank', 'noopener,noreferrer');
    }
  };

  return (
    <div className="space-y-4">
      <Card data-testid="etr-overview-card">
        <h2 className="font-serif text-[22px] font-semibold mb-4 text-brand-slate-800">
          Overview
        </h2>
        <dl className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <OverviewItem
            label="Evaluation Date"
            value={
              etr.evaluationDate
                ? new Date(etr.evaluationDate).toLocaleDateString()
                : '—'
            }
          />
          <OverviewItem
            label="Evaluation Type"
            value={
              etr.evaluationType
                ? EVALUATION_TYPE_LABELS[etr.evaluationType] || etr.evaluationType
                : '—'
            }
          />
          <OverviewItem
            label="Document State"
            value={DOCUMENT_STATE_LABELS[etr.documentState] || etr.documentState || '—'}
          />
          <OverviewItem label="Status" value={etr.status} />
          <OverviewItem
            label="Created"
            value={new Date(etr.createdAt).toLocaleDateString()}
          />
          <OverviewItem label="File" value={etr.fileName || '—'} />
        </dl>
      </Card>

      {etr.status === 'created' ? (
        <Card data-testid="etr-upload-card">
          <h2 className="font-serif text-[18px] font-semibold mb-2 text-brand-slate-800">
            Upload the ETR document
          </h2>
          <p className="text-sm text-brand-slate-500 mb-4">
            Attach a PDF to start parsing. Once uploaded, we'll extract the sections
            and prepare them for analysis.
          </p>
          <EtrUpload
            etrId={etrId}
            onUploaded={() => {
              // Refresh the ETR so status flips to uploaded/processing and
              // the processing hook picks up polling.
              void onUploaded();
            }}
          />
        </Card>
      ) : (
        etr.fileName && (
          <Card data-testid="etr-document-card">
            <div className="flex items-center justify-between gap-3">
              <div className="min-w-0">
                <h2 className="font-serif text-[18px] font-semibold text-brand-slate-800 truncate">
                  {etr.fileName}
                </h2>
                <p className="text-sm text-brand-slate-500 mt-1">
                  Uploaded document
                </p>
              </div>
              <Button
                variant="secondary"
                onClick={handleDownload}
                data-testid="etr-download-button"
              >
                <Download className="w-4 h-4 mr-1.5" strokeWidth={1.8} aria-hidden="true" />
                View document
              </Button>
            </div>
          </Card>
        )
      )}

      {etr.status !== 'created' && etr.status !== 'parsed' && etr.status !== 'error' && (
        <Notice variant="info" title="Analysis and meeting prep coming soon">
          Once processing completes, you'll see parsed sections. Analysis and meeting
          prep tabs are in progress.
        </Notice>
      )}
    </div>
  );
}

function OverviewItem({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-brand-slate-50 rounded-card p-3 border-[0.5px] border-brand-slate-200">
      <dt className="text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold">
        {label}
      </dt>
      <dd className="text-sm font-medium text-brand-slate-800 mt-1">{value}</dd>
    </div>
  );
}
