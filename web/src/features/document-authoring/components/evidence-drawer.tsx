import { useEffect, useId, useState } from 'react';
import { Trans, useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Button } from '@/components/ui/button';
import { Drawer } from '@/components/ui/drawer';
import { Markdown } from '@/components/ui/markdown';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import { type LoadError, toLoadError, loadErrorText } from '@/lib/api-error';
import { getStudentEvidence, type EvidenceItem, type EvidenceKind, type StudentEvidenceBundle } from '../api/evidence-api';
import type { ActiveFieldTarget } from '../hooks/document-editor-context';

interface EvidenceDrawerProps {
  open: boolean;
  onClose: () => void;
  studentId: number;
  /** The field that last had focus (null when nothing has been focused yet). */
  activeField: ActiveFieldTarget | null;
}

function groups(t: TFunction<['document-authoring', 'common']>): Array<{ kinds: EvidenceKind[]; title: string }> {
  return [
    { kinds: ['Identity', 'TeamMember'], title: t('evidenceDrawer.groupStudentTeam') },
    { kinds: ['PresentLevels', 'EtrFinding'], title: t('evidenceDrawer.groupPresentLevels') },
    { kinds: ['PriorGoal', 'PriorService', 'PriorAccommodation', 'PriorTransition'], title: t('evidenceDrawer.groupPriorPlan') },
    { kinds: ['StudentVoice'], title: t('evidenceDrawer.groupStudentVoice') },
    { kinds: ['ParentContribution'], title: t('evidenceDrawer.groupFamily') },
  ];
}

function roleLabel(t: TFunction<['document-authoring', 'common']>, role: EvidenceItem['authorRole']): string {
  const labels: Record<EvidenceItem['authorRole'], string> = {
    school: t('evidenceDrawer.roleSchool'),
    student: t('evidenceDrawer.roleStudent'),
    family: t('evidenceDrawer.roleFamily'),
    system: t('evidenceDrawer.roleSystem'),
  };
  return labels[role];
}

/**
 * Everything on record for this student that AI and prefill are allowed to
 * see — nothing more. Each item shows its source and date; "Insert" writes the
 * text into whichever field last had focus, as a plain copy.
 */
export function EvidenceDrawer({ open, onClose, studentId, activeField }: EvidenceDrawerProps) {
  const { t } = useTranslation(['document-authoring', 'common']);
  const [bundle, setBundle] = useState<StudentEvidenceBundle | null>(null);
  // A server message is already resolved text; the generic case is
  // translated at render time below — so this effect never needs `t` in its
  // dependency array (a language switch must not re-trigger the fetch).
  const [error, setError] = useState<LoadError | null>(null);
  const [loaded, setLoaded] = useState(false);

  const targetHintId = useId();
  const targetLabel = activeField?.label() ?? null;

  // Fetched once per editor mount; a failed load is retried the next time the
  // drawer opens (the previous error stays visible until the retry answers).
  useEffect(() => {
    if (!open || loaded) return;
    let active = true;
    getStudentEvidence(studentId)
      .then((res) => {
        if (!active) return;
        if (res.success && res.data) {
          setBundle(res.data);
          setError(null);
          setLoaded(true);
        } else setError(toLoadError(res));
      })
      .catch(() => {
        if (active) setError({ kind: 'generic' });
      });
    return () => {
      active = false;
    };
  }, [open, loaded, studentId]);

  return (
    <Drawer open={open} onClose={onClose} title={t('evidenceDrawer.title')}>
      <div className="space-y-5" data-testid="evidence-drawer">
        <p className="text-[13px] text-brand-slate-500">{t('evidenceDrawer.description')}</p>
        <p
          id={targetHintId}
          className="rounded-card border border-brand-slate-200 bg-brand-slate-50 px-3 py-2 text-[13px] text-brand-slate-600"
          data-testid="evidence-target"
        >
          {targetLabel ? (
            <Trans
              t={t}
              i18nKey="evidenceDrawer.insertingInto"
              values={{ label: targetLabel }}
              components={{ target: <span className="font-medium text-brand-slate-800" /> }}
            />
          ) : (
            t('evidenceDrawer.closeHint')
          )}
        </p>
        {!loaded && !error && (
          <div className="space-y-2" role="status" aria-label={t('evidenceDrawer.loadingAriaLabel')}>
            <Skeleton className="h-5 w-3/4" />
            <Skeleton className="h-5 w-1/2" />
            <span className="sr-only">{t('common:ui.loading')}</span>
          </div>
        )}
        {error && (
          <Notice variant="error" title={t('evidenceDrawer.loadErrorTitle')}>
            {loadErrorText(error, t('evidenceDrawer.loadError'))}
          </Notice>
        )}
        {bundle && bundle.items.length === 0 && (
          <Notice variant="info" title={t('evidenceDrawer.emptyTitle')}>
            {t('evidenceDrawer.emptyBody')}
          </Notice>
        )}
        {bundle &&
          groups(t).map((g) => {
            const items = bundle.items.filter((i) => g.kinds.includes(i.kind));
            if (items.length === 0) return null;
            return (
              <section key={g.title} aria-label={g.title}>
                <h3 className="mb-2 text-[13px] font-medium uppercase tracking-wide text-brand-slate-500">{g.title}</h3>
                <ul className="space-y-2">
                  {items.map((item) => (
                    <li
                      key={item.id}
                      className="rounded-card border border-brand-slate-200 bg-white p-3"
                      data-testid={`evidence-${item.id}`}
                    >
                      <div className="mb-1 flex flex-wrap items-center gap-2 text-[11px] text-brand-slate-500">
                        <span className="rounded bg-brand-slate-100 px-1 font-mono text-brand-teal-700">{item.id}</span>
                        <span className="font-medium text-brand-slate-600">{item.sourceLabel}</span>
                        {item.sourceDate && <span>{new Date(item.sourceDate).toLocaleDateString()}</span>}
                        <span className="ml-auto rounded-full border border-brand-slate-200 px-1.5">{roleLabel(t, item.authorRole)}</span>
                      </div>
                      <Markdown content={item.text} className="text-[13px] text-brand-slate-700" />
                      {item.kind !== 'Identity' && item.kind !== 'TeamMember' && (
                        <div className="mt-2 flex justify-end">
                          <Button
                            variant="secondary"
                            size="sm"
                            aria-disabled={!activeField}
                            aria-describedby={targetHintId}
                            aria-label={
                              targetLabel
                                ? t('evidenceDrawer.insertAriaLabelWithTarget', { source: item.sourceLabel, target: targetLabel })
                                : t('evidenceDrawer.insertAriaLabel', { source: item.sourceLabel })
                            }
                            className={activeField ? undefined : 'opacity-60'}
                            onClick={() => activeField?.apply(item.text)}
                            data-testid={`evidence-${item.id}-insert`}
                          >
                            {targetLabel ? t('evidenceDrawer.insertInto', { target: targetLabel }) : t('evidenceDrawer.insert')}
                          </Button>
                        </div>
                      )}
                    </li>
                  ))}
                </ul>
              </section>
            );
          })}
      </div>
    </Drawer>
  );
}
