import { useParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Spinner } from '@/components/ui/spinner';
import { usePageTitle } from '@/hooks/use-page-title';
import { formatDate } from '@/lib/format-date';
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
  const { childId: childIdParam, rev: revParam } = useParams<{ childId: string; rev: string }>();
  const childId = Number(childIdParam);
  const revisionId = Number(revParam);

  const { detail, isLoading, error, retry, applyUpdate } = useSharedDraftDetail(revisionId);
  const notesState = useDraftNotes(revisionId);
  const responsesState = useDraftResponses(revisionId);
  const explanations = useDraftExplanations(revisionId);

  usePageTitle(detail ? `${detail.documentTypeDisplayName} · Revision ${detail.revisionNumber}` : 'Shared draft');

  const backTo = `/children/${childId}/shared-drafts`;

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label="Loading shared draft…" />
      </div>
    );
  }

  if (error || !detail) {
    return (
      <PageLayout title="Shared draft unavailable" breadcrumb={[{ label: 'Shared drafts', to: backTo }]}>
        <div role="alert">
          <Notice variant="error" title={error ?? 'This shared draft is unavailable.'}>
            <Button variant="secondary" className="mt-2" onClick={retry} data-testid="shared-draft-retry">
              Try again
            </Button>
          </Notice>
        </div>
      </PageLayout>
    );
  }

  const contextValue: DraftReviewContextValue = {
    revisionId: detail.id,
    canRespond: detail.status === 'Active',
    notes: notesState.notes,
    addNote: notesState.addNote,
    removeNote: notesState.removeNote,
    responses: responsesState.responses,
    addResponse: responsesState.addResponse,
    explanations,
  };

  return (
    <PageLayout
      title={`${detail.documentTypeDisplayName} · Revision ${detail.revisionNumber}`}
      subtitle={`Shared ${formatDate(detail.sharedAt)} by ${detail.sharedByName}`}
      breadcrumb={[{ label: 'Shared drafts', to: backTo }, { label: `Revision ${detail.revisionNumber}` }]}
      data-testid="shared-draft-review-page"
    >
      <DraftReviewContext.Provider value={contextValue}>
        <div className="space-y-6">
          <RevisionStatusBanner status={detail.status} withdrawnAt={detail.withdrawnAt} />
          <RevisionSwitcher childId={childId} documentInstanceId={detail.documentInstanceId} currentRevisionId={detail.id} />

          <Card className="flex flex-wrap items-start justify-between gap-4">
            {detail.message ? (
              <div>
                <p className="text-xs font-medium text-brand-slate-500">Note from the school</p>
                <p className="mt-1 text-sm text-brand-slate-700">{detail.message}</p>
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
              <h2 className="mb-2 font-serif text-base text-brand-slate-800">What's new in this revision</h2>
              <ChangeSummaryChips summary={detail.changeSummary} data-testid="review-change-chips" />
            </Card>
          )}

          {notesState.error && (
            <div role="alert">
              <Notice variant="error" title={notesState.error} />
            </div>
          )}
          {responsesState.error && (
            <div role="alert">
              <Notice variant="error" title={responsesState.error} />
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
