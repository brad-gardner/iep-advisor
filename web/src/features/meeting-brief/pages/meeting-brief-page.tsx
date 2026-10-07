import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Spinner } from '@/components/ui/spinner';
import { ResponseCard } from '@/features/draft-sharing/components/response-card';
import { FAMILY_CONTACT_METHOD_LABELS, FAMILY_CONTACT_OUTCOME_LABELS } from '@/features/family-contact/types';
import { ChangeSummaryChips } from '@/features/shared-drafts/components/change-summary-chips';
import { usePageTitle } from '@/hooks/use-page-title';
import { formatDate } from '@/lib/format-date';
import { GeneratedLanguageNotice } from '@/lib/i18n/generated-language-notice';
import { BriefChecklist } from '../components/brief-checklist';
import { ResourceCommitmentList } from '../components/resource-commitment-list';
import { useMeetingBrief } from '../hooks/use-meeting-brief';

/**
 * Phone-readable pre-meeting brief for the LEA rep (plan 7, decision 2):
 * source, AI summary, what changed, resource commitments, procedural
 * checklist, open family responses, offline participation, and a disclaimer.
 * Advisory only — never a substitute for the team's own decisions.
 *
 * `meeting-brief` is a staff-only namespace (plan phase 5): its English is
 * registered by `features/meeting-brief/staff-locales`, imported at the top
 * of this page's lazy route chunk (`app/lazy-routes/staff-routes.tsx`) — see
 * `docs/i18n/README.md`'s "Staff and admin namespaces". `FAMILY_CONTACT_*_LABELS`
 * (`features/family-contact/types`) are that feature's own English label
 * maps — out of this phase's scope, so they stay untranslated until that
 * feature's own phase converts them.
 */
export function MeetingBriefPage() {
  const { t } = useTranslation(['meeting-brief', 'common']);
  const { meetingId: meetingIdParam } = useParams<{ meetingId: string }>();
  const meetingId = Number(meetingIdParam);
  usePageTitle(t('page.title'));
  const { brief, isLoading, notFound, error, isGenerating, generateError, regenerate } = useMeetingBrief(meetingId);

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label={t('page.loading')} />
      </div>
    );
  }

  if (notFound) {
    return (
      <PageLayout title={t('page.title')} breadcrumb={[{ label: t('page.title') }]}>
        <EmptyState
          title={t('page.noBriefTitle')}
          description={t('page.noBriefDescription')}
          action={
            <Button onClick={() => void regenerate()} loading={isGenerating} data-testid="brief-generate">
              {t('page.generate')}
            </Button>
          }
          data-testid="brief-not-found"
        />
        {generateError && (
          <div role="alert" className="mt-4">
            <Notice variant="error" title={generateError} />
          </div>
        )}
      </PageLayout>
    );
  }

  if (error || !brief) {
    return (
      <PageLayout title={t('page.title')} breadcrumb={[{ label: t('page.title') }]}>
        <Notice variant="error" title={t('page.loadFailedTitle')}>
          {error ?? t('page.loadFailedFallback')}
        </Notice>
      </PageLayout>
    );
  }

  return (
    <PageLayout
      title={t('page.title')}
      subtitle={t('page.generatedAt', { date: formatDate(brief.generatedAt) })}
      breadcrumb={[{ label: t('page.title') }]}
      actions={
        <Button
          variant="secondary"
          onClick={() => void regenerate()}
          loading={isGenerating}
          data-testid="brief-regenerate"
        >
          {t('page.regenerate')}
        </Button>
      }
    >
      <div className="space-y-6" data-testid="meeting-brief-page">
        {generateError && (
          <div role="alert">
            <Notice variant="error" title={generateError} />
          </div>
        )}

        {brief.source && (
          <p className="text-sm text-brand-slate-500" data-testid="brief-source">
            {t('page.source', { source: t(`sourceKind.${brief.source.kind}`), label: brief.source.label })}
          </p>
        )}

        <Card>
          <h2 className="mb-2 font-serif text-lg text-brand-slate-800">{t('page.summaryHeading')}</h2>
          <GeneratedLanguageNotice generatedLanguage={brief.generatedLanguage} className="mb-2" />
          <p className="text-sm text-brand-slate-700" role="status" aria-live="polite" data-testid="brief-summary">
            {brief.summary}
          </p>
        </Card>

        {brief.changes && (
          <Card>
            <h2 className="mb-2 font-serif text-lg text-brand-slate-800">{t('page.changesHeading')}</h2>
            <ChangeSummaryChips summary={brief.changes} data-testid="brief-change-chips" />
          </Card>
        )}

        <Card>
          <h2 className="mb-3 font-serif text-lg text-brand-slate-800">{t('page.resourceCommitmentsHeading')}</h2>
          <ResourceCommitmentList items={brief.resourceCommitments} />
        </Card>

        <Card>
          <h2 className="mb-3 font-serif text-lg text-brand-slate-800">{t('page.checklistHeading')}</h2>
          <BriefChecklist items={brief.checklist} />
        </Card>

        <Card>
          <h2 className="mb-3 font-serif text-lg text-brand-slate-800">{t('page.familyResponsesHeading')}</h2>
          {brief.openFamilyResponses.length === 0 ? (
            <p className="text-sm text-brand-slate-500" data-testid="brief-family-responses-empty">
              {t('page.familyResponsesEmpty')}
            </p>
          ) : (
            <div className="space-y-3" data-testid="brief-family-responses">
              {brief.openFamilyResponses.map((r) => (
                <ResponseCard key={r.id} response={r} />
              ))}
            </div>
          )}
        </Card>

        <Card>
          <h2 className="mb-3 font-serif text-lg text-brand-slate-800">{t('page.offlineParticipationHeading')}</h2>
          <div className="space-y-4">
            <div>
              <h3 className="mb-1 text-[13px] font-medium uppercase tracking-wide text-brand-slate-500">
                {t('page.contactAttemptsHeading')}
              </h3>
              {brief.contactAttempts.length === 0 ? (
                <p className="text-sm text-brand-slate-500" data-testid="brief-contact-attempts-empty">
                  {t('page.contactAttemptsEmpty')}
                </p>
              ) : (
                <ul className="divide-y divide-brand-slate-100" data-testid="brief-contact-attempts">
                  {brief.contactAttempts.map((a) => (
                    <li key={a.id} className="py-2 text-sm text-brand-slate-700">
                      {FAMILY_CONTACT_METHOD_LABELS[a.method]} · {FAMILY_CONTACT_OUTCOME_LABELS[a.outcome]} ·{' '}
                      {formatDate(a.attemptedAt)}
                    </li>
                  ))}
                </ul>
              )}
            </div>
            <div>
              <h3 className="mb-1 text-[13px] font-medium uppercase tracking-wide text-brand-slate-500">
                {t('page.offlineInputHeading')}
              </h3>
              {brief.offlineInput.length === 0 ? (
                <p className="text-sm text-brand-slate-500" data-testid="brief-offline-input-empty">
                  {t('page.offlineInputEmpty')}
                </p>
              ) : (
                <ul className="divide-y divide-brand-slate-100" data-testid="brief-offline-input">
                  {brief.offlineInput.map((i) => (
                    <li key={i.id} className="py-2 text-sm">
                      <p className="text-brand-slate-700">
                        {FAMILY_CONTACT_METHOD_LABELS[i.method]} · {formatDate(i.receivedAt)}
                      </p>
                      <p className="text-xs text-brand-slate-500">{i.summary}</p>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>
        </Card>

        <p className="text-xs italic text-brand-slate-500" data-testid="brief-disclaimer">
          {brief.disclaimer}
        </p>

        <Link to="/educator/calendar" className="text-sm text-brand-teal-600 underline">
          {t('page.backToCalendar')}
        </Link>
      </div>
    </PageLayout>
  );
}
