import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { fieldElementId } from '@/features/document-authoring/components/field-renderers/types';
import { jumpToFieldWhenVisible } from '@/features/document-authoring/lib/section-dom';
import type { DocumentInstanceStatus } from '@/features/document-authoring/types';
import { ChangeSummaryChips } from '@/features/shared-drafts/components/change-summary-chips';
import { formatDate } from '@/lib/format-date';
import { useConverge } from '../hooks/use-converge';
import { buildFieldLocationLookup } from '../lib/field-lookup';
import { ResolveResponseDialog } from './resolve-response-dialog';
import { ResponseCard } from './response-card';
import { ShareWithFamilyButton } from './share-with-family-button';
import type { DraftResponseDto } from '../types';
import type { TemplateVersionDetailDto } from '@/features/admin/templates/types';

interface ConvergePanelProps {
  instanceId: number;
  status: DocumentInstanceStatus;
  templateVersion: TemplateVersionDetailDto;
  /** Called before a jump-to-field so the host can reveal the editor (it is
   *  hidden while this tab is active); the jump itself runs on the next frame. */
  onBeforeJump?: () => void;
}

/** Staff aggregate view for one document: the latest shared revision, what
 *  changed in the live draft since then, and every family response grouped by
 *  open/resolved with jump-to-field and reply/resolve. */
export function ConvergePanel({ instanceId, status, templateVersion, onBeforeJump }: ConvergePanelProps) {
  // `shared-drafts` reached for `ChangeSummaryChips`'s own `t()` calls — loading
  // this namespace's Spanish here guarantees it for that shared component too
  // (see the README's "reach another namespace" gotcha).
  const { t } = useTranslation(['draft-sharing', 'shared-drafts', 'common']);
  const { converge, isLoading, error, retry, applyResolvedResponse } = useConverge(instanceId);
  const [resolving, setResolving] = useState<DraftResponseDto | null>(null);
  const fieldLookup = useMemo(() => buildFieldLocationLookup(templateVersion), [templateVersion]);

  if (isLoading) {
    return (
      <div className="flex justify-center py-8">
        <Spinner label={t('converge.loading')} />
      </div>
    );
  }

  // Only a first-load failure replaces the tree. A failed *refresh* keeps the last-good data
  // (and any open reply/resolve dialog) on screen with the error shown inline above it.
  if (!converge) {
    return (
      <div role="alert">
        <Notice variant="error" title={error && error.kind === 'server' ? error.message : t('converge.loadErrorDefault')}>
          <Button variant="secondary" className="mt-2" onClick={retry} data-testid="converge-retry">
            {t('common:ui.tryAgain')}
          </Button>
        </Notice>
      </div>
    );
  }

  const jumpTo = (response: DraftResponseDto) => {
    const loc = response.targetFieldKey ? fieldLookup.get(response.targetFieldKey) : undefined;
    if (!loc) return;
    // The editor is display:none behind this tab — ask the host to reveal it, then scroll/focus
    // once it is actually un-hidden (the reveal is a router transition, so not necessarily next frame).
    onBeforeJump?.();
    jumpToFieldWhenVisible(fieldElementId(loc.fieldId), loc.sectionId);
  };

  return (
    <div className="space-y-6" data-testid="converge-panel">
      {error && (
        <div role="alert">
          <Notice variant="error" title={error.kind === 'server' ? error.message : t('converge.loadErrorDefault')}>
            <Button variant="secondary" className="mt-2" onClick={retry} data-testid="converge-retry">
              {t('common:ui.tryAgain')}
            </Button>
          </Notice>
        </div>
      )}
      <div className="flex flex-wrap items-center justify-between gap-3">
        {converge.latestRevision ? (
          <p className="text-sm text-brand-slate-600">
            {t('converge.sharedLine', { number: converge.latestRevision.revisionNumber, date: formatDate(converge.latestRevision.sharedAt) })}
          </p>
        ) : (
          <p className="text-sm text-brand-slate-500">{t('converge.notSharedYet')}</p>
        )}
        <ShareWithFamilyButton
          instanceId={instanceId}
          status={status}
          label={converge.latestRevision ? t('converge.shareAgainLabel') : t('shareWithFamilyLabel')}
          onShared={retry}
        />
      </div>

      {converge.acknowledgements.length > 0 && (
        <Card>
          <h2 className="mb-2 font-serif text-base text-brand-slate-800">{t('converge.acknowledgedByHeading')}</h2>
          <ul className="space-y-1 text-sm text-brand-slate-600">
            {converge.acknowledgements.map((a, i) => (
              <li key={`${a.parentName}-${i}`}>{t('converge.acknowledgedLine', { name: a.parentName, date: formatDate(a.acknowledgedAt) })}</li>
            ))}
          </ul>
        </Card>
      )}

      {converge.changesSinceShare && (
        <Card data-testid="converge-changes-since-share">
          <h2 className="mb-2 font-serif text-base text-brand-slate-800">{t('converge.changesSinceLastShare')}</h2>
          <ChangeSummaryChips summary={converge.changesSinceShare} data-testid="converge-change-chips" />
        </Card>
      )}

      <div>
        <h2 className="mb-3 font-serif text-lg text-brand-slate-800">{t('converge.openResponsesHeading')}</h2>
        {converge.openResponses.length === 0 ? (
          <p className="text-sm text-brand-slate-500">{t('converge.noOpenResponses')}</p>
        ) : (
          <div className="space-y-3" data-testid="converge-open-responses">
            {converge.openResponses.map((r) => (
              <ResponseCard
                key={r.id}
                response={r}
                onJump={r.targetFieldKey ? () => jumpTo(r) : undefined}
                onResolve={() => setResolving(r)}
              />
            ))}
          </div>
        )}
      </div>

      {converge.resolvedResponses.length > 0 && (
        <div>
          <h2 className="mb-3 font-serif text-lg text-brand-slate-800">{t('converge.resolvedHeading')}</h2>
          <div className="space-y-3" data-testid="converge-resolved-responses">
            {converge.resolvedResponses.map((r) => (
              <ResponseCard key={r.id} response={r} onJump={r.targetFieldKey ? () => jumpTo(r) : undefined} />
            ))}
          </div>
        </div>
      )}

      <ResolveResponseDialog
        open={resolving !== null}
        onClose={() => setResolving(null)}
        response={resolving}
        onResolved={(updated) => {
          applyResolvedResponse(updated);
          setResolving(null);
        }}
      />
    </div>
  );
}
