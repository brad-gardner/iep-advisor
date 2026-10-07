import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, ChevronDown, ChevronUp, Download } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card } from '@/components/ui/card';
import { Markdown } from '@/components/ui/markdown';
import { Notice } from '@/components/ui/notice';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { EmptyState } from '@/components/ui/empty-state';
import { PageHeader } from '@/components/ui/page-header';
import { FileX } from 'lucide-react';
import { useEtrDocument } from '../hooks/use-etr-documents';
import { useEtrProcessing } from '../hooks/use-etr-processing';
import { useEtrSections } from '../hooks/use-etr-sections';
import { useEtrAnalysis } from '../hooks/use-etr-analysis';
import { getDownloadUrl } from '../api/etr-documents-api';
import { evaluationTypeLabel, documentStateLabel } from '../lib/document-labels';
import { EtrUpload } from './etr-upload';
import { EtrProcessingBanner } from './etr-processing-banner';
import { EtrErrorBanner } from './etr-error-banner';
import { EtrSectionsList } from './etr-sections-list';
import { EtrAnalysisTab } from './etr-analysis-tab';
import { usePageTitle } from '@/hooks/use-page-title';
import { formatDate } from '@/lib/format-date';
import { documentStatusLabel } from '@/lib/document-status-label';
import { AskAdvocateButton } from '@/features/advocate/components/ask-advocate-button';

type TabKey = 'overview' | 'sections' | 'analysis';

const IN_FLIGHT = new Set(['uploaded', 'processing']);

export function EtrViewerPage() {
  const { t } = useTranslation(['etr-documents', 'iep-documents']);
  usePageTitle('ETR');
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

  const {
    run: analysisRun,
    source: analysisSource,
    sections: analysisSections,
    completeness: analysisCompleteness,
    eligibility: analysisEligibility,
    otherSources: analysisOtherSources,
    stale: analysisStale,
    isLoading: analysisLoading,
    loadError: analysisLoadError,
    isTriggering,
    triggerError,
    trigger: triggerAnalysis,
    reload: reloadAnalysis,
  } = useEtrAnalysis(etr?.childProfileId ?? 0, documentId);

  const [activeTab, setActiveTab] = useState<TabKey>('overview');
  const [notesExpanded, setNotesExpanded] = useState(false);

  // Meeting Prep lives at the child level now (standalone tab), so it is no
  // longer rendered embedded inside the ETR viewer.

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label={t('loadingEvaluation')} />
      </div>
    );
  }

  if (!etr) {
    return <EmptyState icon={FileX} title={t('viewerPage.notFound')} />;
  }

  const headerTitle =
    etr.fileName ||
    (etr.evaluationType
      ? evaluationTypeLabel(etr.evaluationType)
      : `ETR #${etr.id}`);

  const sectionsTabDisabled = etr.status !== 'parsed';
  const sectionsTabHint =
    etr.status === 'created'
      ? t('viewerPage.hintUploadFirst')
      : etr.status === 'error'
        ? t('viewerPage.hintProcessingFailed')
        : IN_FLIGHT.has(etr.status)
          ? t('viewerPage.hintProcessing')
          : undefined;

  const analysisTabDisabled = etr.status !== 'parsed';
  const analysisTabHint =
    etr.status === 'created'
      ? t('viewerPage.hintUploadFirstAnalysis')
      : etr.status === 'error'
        ? t('viewerPage.hintProcessingFailed')
        : IN_FLIGHT.has(etr.status)
          ? t('viewerPage.hintProcessing')
          : undefined;

  const TABS: { key: TabKey; label: string; disabled: boolean; hint?: string }[] = [
    { key: 'overview', label: t('viewerPage.tabOverview'), disabled: false },
    {
      key: 'sections',
      label: t('viewerPage.tabSections'),
      disabled: sectionsTabDisabled,
      hint: sectionsTabHint,
    },
    {
      key: 'analysis',
      label: t('viewerPage.tabAnalysis'),
      disabled: analysisTabDisabled,
      hint: analysisTabHint,
    },
  ];

  return (
    <div className="space-y-4">
      <Link
        to={`/children/${etr.childProfileId}`}
        className="inline-flex items-center gap-1.5 text-[13px] font-medium text-brand-slate-500 hover:text-brand-teal-500 transition-colors"
      >
        <ArrowLeft className="w-4 h-4" strokeWidth={1.8} aria-hidden="true" />
        {t('backToChild')}
      </Link>

      <PageHeader
        title={headerTitle}
        subtitle={t('evaluationTeamReport')}
        actions={
          <AskAdvocateButton
            childId={etr.childProfileId}
            about={{ kind: 'etr', id: etr.id }}
            label={etr.evaluationDate ? t('askAbout.etr', { date: formatDate(etr.evaluationDate) }) : undefined}
            data-testid="etr-ask-advocate"
          />
        }
      />

      <div className="flex items-center gap-3 flex-wrap">
        {etr.evaluationType && (
          <Badge variant="neutral">
            {evaluationTypeLabel(etr.evaluationType)}
          </Badge>
        )}
        {etr.documentState && (
          <Badge variant={etr.documentState === 'final' ? 'success' : 'neutral'}>
            {documentStateLabel(etr.documentState)}
          </Badge>
        )}
        <Badge variant="neutral">{documentStatusLabel(etr.status)}</Badge>
        {etr.evaluationDate && (
          <span className="text-[13px] text-brand-slate-500">
            {t('viewerPage.evaluated', { date: formatDate(etr.evaluationDate) })}
          </span>
        )}
        {isPolling && (
          <span
            className="inline-flex items-center gap-1 text-[11px] text-brand-amber-500"
            data-testid="etr-polling-indicator"
          >
            <span className="inline-block w-1.5 h-1.5 rounded-full bg-brand-amber-500 animate-pulse" />
            {t('viewerPage.refreshing')}
          </span>
        )}
      </div>

      {etr.notes && (
        <div>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setNotesExpanded(!notesExpanded)}
            data-testid="etr-notes-toggle"
          >
            {notesExpanded ? (
              <ChevronUp className="w-3.5 h-3.5 mr-1" strokeWidth={1.8} aria-hidden="true" />
            ) : (
              <ChevronDown className="w-3.5 h-3.5 mr-1" strokeWidth={1.8} aria-hidden="true" />
            )}
            {t('viewerPage.notes')}
          </Button>
          {notesExpanded && (
            <div className="mt-1 bg-brand-slate-50 rounded-card p-3 border border-brand-slate-200">
              <Markdown content={etr.notes ?? ''} className="text-sm text-brand-slate-600" />
            </div>
          )}
        </div>
      )}

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
                    : 'text-brand-slate-500 hover:text-brand-slate-800'
              }`}
            >
              {tab.label}
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
          childId={etr.childProfileId}
          run={analysisRun}
          source={analysisSource}
          sections={analysisSections}
          completeness={analysisCompleteness}
          eligibility={analysisEligibility}
          otherSources={analysisOtherSources}
          stale={analysisStale}
          isLoading={analysisLoading}
          loadError={analysisLoadError}
          isTriggering={isTriggering}
          triggerError={triggerError}
          onTrigger={triggerAnalysis}
          onReload={reloadAnalysis}
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
  const { t } = useTranslation(['etr-documents', 'iep-documents']);
  const handleDownload = async () => {
    const res = await getDownloadUrl(etrId);
    if (res.success && res.data) {
      window.open(res.data.url, '_blank', 'noopener,noreferrer');
    }
  };

  return (
    // Document viewer: cap at a comfortable reading width rather than filling
    // the widened (max-w-7xl) app shell.
    <div className="space-y-4 max-w-5xl">
      <Card data-testid="etr-overview-card">
        <h2 className="font-serif text-[22px] font-semibold mb-4 text-brand-slate-800">
          {t('viewerPage.tabOverview')}
        </h2>
        <dl className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <OverviewItem
            label={t('viewerPage.evaluationDate')}
            value={
              etr.evaluationDate
                ? formatDate(etr.evaluationDate)
                : '—'
            }
          />
          <OverviewItem
            label={t('viewerPage.evaluationType')}
            value={etr.evaluationType ? evaluationTypeLabel(etr.evaluationType) : '—'}
          />
          <OverviewItem
            label={t('viewerPage.documentState')}
            value={etr.documentState ? documentStateLabel(etr.documentState) : '—'}
          />
          <OverviewItem label={t('viewerPage.status')} value={documentStatusLabel(etr.status)} />
          <OverviewItem
            label={t('viewerPage.created')}
            value={formatDate(etr.createdAt)}
          />
          <OverviewItem label={t('viewerPage.file')} value={etr.fileName || '—'} />
        </dl>
      </Card>

      {etr.status === 'created' ? (
        <Card data-testid="etr-upload-card">
          <h2 className="font-serif text-[18px] font-semibold mb-2 text-brand-slate-800">
            {t('viewerPage.uploadHeading')}
          </h2>
          <p className="text-sm text-brand-slate-500 mb-4">
            {t('viewerPage.uploadBody')}
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
                  {t('viewerPage.uploadedDocument')}
                </p>
              </div>
              <Button
                variant="secondary"
                onClick={handleDownload}
                data-testid="etr-download-button"
              >
                <Download className="w-4 h-4 mr-1.5" strokeWidth={1.8} aria-hidden="true" />
                {t('viewerPage.viewDocument')}
              </Button>
            </div>
          </Card>
        )
      )}

      {etr.status !== 'created' && etr.status !== 'parsed' && etr.status !== 'error' && (
        <Notice variant="info" title={t('viewerPage.comingSoonTitle')}>
          {t('viewerPage.comingSoonBody')}
        </Notice>
      )}
    </div>
  );
}

function OverviewItem({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-brand-slate-50 rounded-card p-3 border border-brand-slate-200">
      <dt className="text-[11px] text-brand-slate-500 uppercase tracking-wide font-semibold">
        {label}
      </dt>
      <dd className="text-sm font-medium text-brand-slate-800 mt-1">{value}</dd>
    </div>
  );
}
