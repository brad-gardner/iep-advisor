import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, ChevronDown, ChevronUp } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { useEtrDocument } from '../hooks/use-etr-documents';
import {
  DOCUMENT_STATE_LABELS,
  EVALUATION_TYPE_LABELS,
} from '../types';

type TabKey = 'overview' | 'sections' | 'analysis' | 'meeting-prep';

const TABS: { key: TabKey; label: string; disabled: boolean }[] = [
  { key: 'overview', label: 'Overview', disabled: false },
  { key: 'sections', label: 'Sections', disabled: true },
  { key: 'analysis', label: 'Analysis', disabled: true },
  { key: 'meeting-prep', label: 'Meeting Prep', disabled: true },
];

export function EtrViewerPage() {
  const { id } = useParams<{ id: string }>();
  const documentId = Number(id);
  const { etr, isLoading } = useEtrDocument(documentId);
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
          <p className="text-[13px] text-brand-slate-500 mt-1">
            Evaluation Team Report
          </p>
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
              className={`px-4 py-2 text-[13px] font-medium transition-colors ${
                isActive
                  ? 'text-brand-slate-800 border-b-2 border-brand-teal-500'
                  : tab.disabled
                    ? 'text-brand-slate-300 cursor-not-allowed'
                    : 'text-brand-slate-400 hover:text-brand-slate-800'
              }`}
            >
              {tab.label}
              {tab.disabled && (
                <span className="ml-2 text-[10px] uppercase tracking-wide text-brand-slate-300">
                  Soon
                </span>
              )}
            </button>
          );
        })}
      </div>

      {activeTab === 'overview' && (
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
            </dl>
          </Card>

          <Notice variant="info" title="More features coming soon">
            Upload, parsed sections, AI analysis, and meeting prep are in progress and will
            arrive in subsequent releases. For now, you can create, view, and delete ETR
            metadata.
          </Notice>
        </div>
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
