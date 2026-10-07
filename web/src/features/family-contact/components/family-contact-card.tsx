import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Markdown } from '@/components/ui/markdown';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import { familyContactMethodLabel, familyContactOutcomeLabel } from '@/lib/family-contact-label';
import { formatDate } from '@/lib/format-date';
import { useFamilyContact } from '../hooks/use-family-contact';
import { LogContactAttemptForm } from './log-contact-attempt-form';
import { RecordOfflineInputForm } from './record-offline-input-form';

interface FamilyContactCardProps {
  studentId: number;
}

/** Student page "Family contact" card (plan 7, decision 7): offline contact
 *  attempts and family input, newest first, each loggable in one short form. */
export function FamilyContactCard({ studentId }: FamilyContactCardProps) {
  const { t } = useTranslation(['family-contact', 'common']);
  const { attempts, offlineInput, isLoading, error, retry, addAttempt, addOfflineInput } =
    useFamilyContact(studentId);
  const [loggingAttempt, setLoggingAttempt] = useState(false);
  const [loggingInput, setLoggingInput] = useState(false);

  return (
    <Card data-testid="family-contact-card">
      <h2 className="mb-4 font-serif text-lg text-brand-slate-800">{t('card.heading')}</h2>

      {error && (
        <div role="alert">
          <Notice variant="error" title={error}>
            <Button size="sm" variant="secondary" className="mt-2" onClick={retry} data-testid="family-contact-retry">
              {t('common:ui.tryAgain')}
            </Button>
          </Notice>
        </div>
      )}

      {!error && isLoading && (
        <div className="space-y-2">
          <Skeleton className="h-8 w-full" />
          <Skeleton className="h-8 w-full" />
        </div>
      )}

      {!error && !isLoading && (
        <div className="space-y-6">
          <section>
            <div className="mb-2 flex items-center justify-between gap-2">
              <h3 className="text-sm font-medium text-brand-slate-700">{t('card.contactAttemptsHeading')}</h3>
              {!loggingAttempt && (
                <Button size="sm" variant="secondary" onClick={() => setLoggingAttempt(true)} data-testid="log-contact-attempt-open">
                  {t('card.logAttemptButton')}
                </Button>
              )}
            </div>
            {loggingAttempt && (
              <div className="mb-3 rounded-card border border-brand-slate-200 p-3">
                <LogContactAttemptForm
                  studentId={studentId}
                  onLogged={(attempt) => {
                    addAttempt(attempt);
                    setLoggingAttempt(false);
                  }}
                  onCancel={() => setLoggingAttempt(false)}
                />
              </div>
            )}
            {attempts.length === 0 ? (
              <p className="text-sm text-brand-slate-500" data-testid="contact-attempts-empty">
                {t('card.noAttemptsYet')}
              </p>
            ) : (
              <ul className="divide-y divide-brand-slate-100" data-testid="contact-attempts-list">
                {attempts.map((a) => (
                  <li key={a.id} className="py-2 text-sm" data-testid={`contact-attempt-${a.id}`}>
                    <p className="text-brand-slate-700">
                      {familyContactMethodLabel(a.method)} · {familyContactOutcomeLabel(a.outcome)} ·{' '}
                      {formatDate(a.attemptedAt)}
                    </p>
                    {a.note && <Markdown content={a.note} className="text-xs text-brand-slate-500" />}
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section>
            <div className="mb-2 flex items-center justify-between gap-2">
              <h3 className="text-sm font-medium text-brand-slate-700">{t('card.offlineInputHeading')}</h3>
              {!loggingInput && (
                <Button size="sm" variant="secondary" onClick={() => setLoggingInput(true)} data-testid="record-offline-input-open">
                  {t('card.recordInputButton')}
                </Button>
              )}
            </div>
            {loggingInput && (
              <div className="mb-3 rounded-card border border-brand-slate-200 p-3">
                <RecordOfflineInputForm
                  studentId={studentId}
                  onLogged={(input) => {
                    addOfflineInput(input);
                    setLoggingInput(false);
                  }}
                  onCancel={() => setLoggingInput(false)}
                />
              </div>
            )}
            {offlineInput.length === 0 ? (
              <p className="text-sm text-brand-slate-500" data-testid="offline-input-empty">
                {t('card.noInputYet')}
              </p>
            ) : (
              <ul className="divide-y divide-brand-slate-100" data-testid="offline-input-list">
                {offlineInput.map((i) => (
                  <li key={i.id} className="py-2 text-sm" data-testid={`offline-input-${i.id}`}>
                    <p className="text-brand-slate-700">
                      {familyContactMethodLabel(i.method)} · {formatDate(i.receivedAt)}
                    </p>
                    <Markdown content={i.summary} className="text-xs text-brand-slate-500" />
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>
      )}
    </Card>
  );
}
