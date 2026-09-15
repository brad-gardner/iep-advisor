import { useCallback, useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/empty-state';
import { PageLayout } from '@/components/ui/page-layout';
import { ProgressDots } from '@/components/ui/progress-dots';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/toast';
import { isAdminOrgRole } from '@/features/educator/types';
import { useEducatorProfile } from '@/features/educator/hooks/use-educator-profile';
import { getImportBatches } from '../api/import-api';
import type { ImportBatch, ImportKind, ImportResult } from '../types';
import { useImportWizard, WIZARD_STEP_INDEX, WIZARD_STEP_LABELS } from '../hooks/use-import-wizard';
import { ImportKindToggle } from '../components/import-kind-toggle';
import { TemplateStep } from '../components/template-step';
import { UploadStep } from '../components/upload-step';
import { PreviewStep } from '../components/preview-step';
import { ResultStep } from '../components/result-step';
import { ImportHistory } from '../components/import-history';

function parseKind(raw: string | null): ImportKind {
  return raw === 'Staff' ? 'Staff' : 'Students';
}

// Admin-only XLSX import wizard for students (default) or staff (`?kind=Staff`).
// The wizard is linear — template → upload → preview → commit → result — and
// the batch history below reloads after every commit.
export function ImportPage() {
  const { profile, isLoading: profileLoading } = useEducatorProfile();
  const isAdmin = isAdminOrgRole(profile?.orgRoleId);
  const { show: showToast } = useToast();
  const [searchParams, setSearchParams] = useSearchParams();
  const kind = parseKind(searchParams.get('kind'));

  const wizard = useImportWizard();
  const [isConfirming, setIsConfirming] = useState(false);
  const [batches, setBatches] = useState<ImportBatch[]>([]);
  const [historyLoading, setHistoryLoading] = useState(true);

  const reloadHistory = useCallback(async () => {
    try {
      const response = await getImportBatches();
      setBatches(response.success && response.data ? response.data : []);
    } catch {
      setBatches([]);
    }
  }, []);

  useEffect(() => {
    if (!isAdmin) return;
    let active = true;
    (async () => {
      try {
        const response = await getImportBatches();
        if (active) setBatches(response.success && response.data ? response.data : []);
      } catch {
        if (active) setBatches([]);
      } finally {
        if (active) setHistoryLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [isAdmin]);

  const changeKind = (next: ImportKind) => {
    if (next === kind) return;
    const params = new URLSearchParams(searchParams);
    params.set('kind', next);
    setSearchParams(params, { replace: true });
    setIsConfirming(false);
    wizard.reset();
  };

  const handleCommitted = async (result: ImportResult) => {
    setIsConfirming(false);
    wizard.showResult(result);
    showToast({ message: 'Import committed', variant: 'success' });
    await reloadHistory();
  };

  const startOver = () => {
    setIsConfirming(false);
    wizard.reset();
  };

  if (profileLoading) {
    return (
      <div className="space-y-4" data-testid="roster-import-page-loading">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-40 w-full" />
      </div>
    );
  }

  if (!isAdmin) {
    return (
      <PageLayout title="Import" data-testid="roster-import-page">
        <EmptyState
          data-testid="roster-import-not-available"
          title="Not available"
          description="Importing is available to district and school administrators only."
          action={
            <Link to="/educator/students">
              <Button variant="secondary">Back to students</Button>
            </Link>
          }
        />
      </PageLayout>
    );
  }

  const stepIndex = isConfirming && wizard.step === 'preview' ? 3 : WIZARD_STEP_INDEX[wizard.step];

  return (
    <PageLayout
      title="Import"
      subtitle="Add or update many records at once from an Excel workbook."
      data-testid="roster-import-page"
      actions={<ImportKindToggle value={kind} onChange={changeKind} />}
    >
      <ProgressDots
        current={stepIndex}
        total={WIZARD_STEP_LABELS.length}
        labels={WIZARD_STEP_LABELS}
        testId="import-progress"
      />

      {wizard.step === 'template' && <TemplateStep kind={kind} onContinue={wizard.goToUpload} />}
      {wizard.step === 'upload' && (
        <UploadStep kind={kind} onPreviewed={wizard.showPreview} onBack={wizard.reset} />
      )}
      {wizard.step === 'preview' && wizard.preview && (
        <PreviewStep
          preview={wizard.preview}
          onCommitted={handleCommitted}
          onStartOver={startOver}
          onConfirmingChange={setIsConfirming}
        />
      )}
      {wizard.step === 'result' && wizard.result && (
        <ResultStep kind={kind} result={wizard.result} onImportAnother={startOver} />
      )}

      <ImportHistory batches={batches} loading={historyLoading} />
    </PageLayout>
  );
}
