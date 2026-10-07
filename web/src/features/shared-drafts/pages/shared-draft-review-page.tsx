import { useMemo } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Markdown } from '@/components/ui/markdown';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Spinner } from '@/components/ui/spinner';
import { usePageTitle } from '@/hooks/use-page-title';
import { formatDate } from '@/lib/format-date';
import { loadErrorText } from '@/lib/api-error';
import { AcknowledgeControl } from '../components/acknowledge-control';
import { ChangeSummaryChips } from '../components/change-summary-chips';
import { FrozenSectionList } from '../components/frozen-section-list';
import { MyResponsesSection } from '../components/my-responses-section';
import { RevisionStatusBanner } from '../components/revision-status-banner';
import { RevisionSwitcher } from '../components/revision-switcher';
import { DraftReviewContext, type DraftReviewContextValue } from '../hooks/draft-review-context';
import { useDraftExplanations } from '../hooks/use-draft-explanations';
import { useDraftNotes } from '../hooks/use-draft-notes';
import { useDraftResponses } from '../hooks/use-draft-responses';
import { useSharedDraftDetail } from '../hooks/use-shared-draft-detail';

/**
 * Phone-first parent reading view of one frozen shared-draft revision:
 * status/withdrawal banners, a revision switcher, the "Mark as reviewed"
 * stamp, what changed since the last share, the frozen values (goals/services/
 * accommodations as interactive cards, everything else via the shared
 * snapshot renderer), and the parent's own responses with staff replies.
 */
export function SharedDraftReviewPage() {
  const { t } = useTranslation(['shared-drafts', 'common']);
  const { childId: childIdParam, rev: revParam } = useParams<{ childId: string; rev: string }>();
  const childId = Number(childIdParam);
  const revisionId = Number(revParam);

  const { detail, isLoading, error, retry, applyUpdate } = useSharedDraftDetail(revisionId);
  const notesState = useDraftNotes(revisionId);
  const responsesState = useDraftResponses(revisionId);
  const explanations = useDraftExplanations(revisionId);

  usePageTitle(
    detail ? t('reviewPage.titleLine', { documentType: detail.documentTypeDisplayName, number: detail.revisionNumber }) : t('listPage.breadcrumbSelf')
  );

  const backTo = `/children/${childId}/shared-drafts`;
  const notesError = loadErrorText(notesState.error, t('notesLoadError'));
  const responsesError = loadErrorText(responsesState.error, t('responsesLoadError'));

  // Every card (and its always-mounted drawers) reads this context; a stable
  // value means one "Explain"/"Ask"/"Respond" doesn't re-render the whole list.
  const detailId = detail?.id ?? revisionId;
  const canRespond = detail?.status === 'Active';
  const contextValue: DraftReviewContextValue = useMemo(
    () => ({
      revisionId: detailId,
      canRespond,
      notes: notesState.notes,
      addNote: notesState.addNote,
      removeNote: notesState.removeNote,
      responses: responsesState.responses,
      addResponse: responsesState.addResponse,
      explanations,
    }),
    [
      detailId,
      canRespond,
      notesState.notes,
      notesState.addNote,
      notesState.removeNote,
      responsesState.responses,
      responsesState.addResponse,
      explanations,
    ]
  );

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label={t('reviewPage.loading')} />
      </div>
    );
  }

  if (error || !detail) {
    return (
      <PageLayout title={t('reviewPage.unavailableTitle')} breadcrumb={[{ label: t('listPage.breadcrumbSelf'), to: backTo }]}>
        <div role="alert">
          <Notice variant="error" title={loadErrorText(error, t('reviewPage.unavailableDefault')) ?? t('reviewPage.unavailableDefault')}>
            <Button variant="secondary" className="mt-2" onClick={retry} data-testid="shared-draft-retry">
              {t('common:ui.tryAgain')}
            </Button>
          </Notice>
        </div>
      </PageLayout>
    );
  }

  return (
    <PageLayout
      title={t('reviewPage.titleLine', { documentType: detail.documentTypeDisplayName, number: detail.revisionNumber })}
      subtitle={t('reviewPage.subtitleLine', { date: formatDate(detail.sharedAt), name: detail.sharedByName })}
      breadcrumb={[
        { label: t('listPage.breadcrumbSelf'), to: backTo },
        { label: t('reviewPage.revisionCrumb', { number: detail.revisionNumber }) },
      ]}
      data-testid="shared-draft-review-page"
    >
      <DraftReviewContext.Provider value={contextValue}>
        <div className="space-y-6">
          <RevisionStatusBanner status={detail.status} withdrawnAt={detail.withdrawnAt} />
          <RevisionSwitcher childId={childId} documentInstanceId={detail.documentInstanceId} currentRevisionId={detail.id} />

          <Card className="flex flex-wrap items-start justify-between gap-4">
            {detail.message ? (
              <div>
                <p className="text-xs font-medium text-brand-slate-500">{t('reviewPage.noteFromSchool')}</p>
                <Markdown content={detail.message} className="mt-1" data-testid="shared-draft-message" />
              </div>
            ) : (
              <span />
            )}
            <AcknowledgeControl
              revisionId={detail.id}
              acknowledgedAt={detail.acknowledgedAt}
              onAcknowledged={(acknowledgedAt) => applyUpdate({ acknowledgedAt })}
            />
          </Card>

          {detail.changeSummary && (
            <Card data-testid="review-change-summary">
              <h2 className="mb-2 font-serif text-base text-brand-slate-800">{t('reviewPage.whatsNew')}</h2>
              <ChangeSummaryChips summary={detail.changeSummary} data-testid="review-change-chips" />
            </Card>
          )}

          {notesError && (
            <div role="alert">
              <Notice variant="error" title={notesError} />
            </div>
          )}
          {responsesError && (
            <div role="alert">
              <Notice variant="error" title={responsesError} />
            </div>
          )}

          <FrozenSectionList
            revisionId={detail.id}
            canRespond={detail.status === 'Active'}
            templateVersion={detail.templateVersion}
            values={detail.values}
            changeSummary={detail.changeSummary}
          />

          <MyResponsesSection responses={responsesState.responses} />
        </div>
      </DraftReviewContext.Provider>
    </PageLayout>
  );
}
