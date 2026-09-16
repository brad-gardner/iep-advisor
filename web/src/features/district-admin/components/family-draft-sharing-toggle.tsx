import { useEffect, useState } from 'react';
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
          message: next ? 'Family draft sharing enabled' : 'Family draft sharing disabled',
          variant: 'success',
        });
      } else {
        setError(res.message ?? 'Could not update this setting.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not update this setting.'));
    } finally {
      setIsSaving(false);
    }
  };

  if (enabled === null) return null;

  return (
    <Card data-testid="family-sharing-toggle-card">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h2 className="font-serif text-base text-brand-slate-800">Family draft sharing</h2>
          <p className="mt-1 max-w-prose text-sm text-brand-slate-500">
            When enabled, staff can share a draft IEP or ETR with a student's family for review before it's
            finalized.
          </p>
        </div>
        <button
          type="button"
          role="switch"
          aria-checked={enabled}
          aria-label="Family draft sharing"
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
