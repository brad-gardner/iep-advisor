import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { useToast } from '@/components/ui/toast';
import { apiErrorMessage } from '@/lib/api-error';
import { getDistrict, updateDistrict } from '../api/district-api';

/**
 * District-wide switch for the plan-6 parent draft-review journey: when off,
 * staff never see the "Share with family" action and the endpoint refuses.
 * Self-contained (fetches its own current value), matching `DistrictOverviewCard`.
 */
export function FamilyDraftSharingToggle() {
  const { t } = useTranslation('district-admin');
  const { show: showToast } = useToast();
  const [enabled, setEnabled] = useState<boolean | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    getDistrict()
      .then((res) => {
        if (active && res.success && res.data) setEnabled(res.data.familyDraftSharingEnabled);
      })
      .catch(() => {
        // Non-critical: the toggle just doesn't render.
      });
    return () => {
      active = false;
    };
  }, []);

  const handleToggle = async () => {
    if (enabled === null || isSaving) return;
    const next = !enabled;
    setIsSaving(true);
    setError(null);
    try {
      const res = await updateDistrict({ familyDraftSharingEnabled: next });
      if (res.success && res.data) {
        setEnabled(res.data.familyDraftSharingEnabled);
        showToast({
          message: next ? t('familyDraftSharing.enabledToast') : t('familyDraftSharing.disabledToast'),
          variant: 'success',
        });
      } else {
        setError(res.message ?? t('familyDraftSharing.errorGeneric'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('familyDraftSharing.errorGeneric')));
    } finally {
      setIsSaving(false);
    }
  };

  if (enabled === null) return null;

  return (
    <Card data-testid="family-sharing-toggle-card">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h2 className="font-serif text-base text-brand-slate-800">{t('familyDraftSharing.title')}</h2>
          <p className="mt-1 max-w-prose text-sm text-brand-slate-500">
            {t('familyDraftSharing.description')}
          </p>
        </div>
        <button
          type="button"
          role="switch"
          aria-checked={enabled}
          aria-label={t('familyDraftSharing.ariaLabel')}
          onClick={handleToggle}
          disabled={isSaving}
          data-testid="family-sharing-toggle"
          className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${
            enabled ? 'bg-brand-teal-500' : 'bg-brand-slate-300'
          }`}
        >
          <span
            className={`pointer-events-none inline-block h-4 w-4 rounded-full bg-white shadow transform transition-transform ${
              enabled ? 'translate-x-4' : 'translate-x-0'
            }`}
          />
        </button>
      </div>
      {error && (
        <div role="alert" className="mt-3">
          <Notice variant="error" title={error} />
        </div>
      )}
    </Card>
  );
}
