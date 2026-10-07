import { useState } from 'react';
import { Trans, useTranslation } from 'react-i18next';
import { Download } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { downloadBlob } from '@/lib/download-file';
import { downloadImportTemplate } from '../api/import-api';
import type { ImportKind } from '../types';

interface TemplateStepProps {
  kind: ImportKind;
  onContinue: () => void;
  // The page focuses the step heading on each transition.
  headingRef?: React.Ref<HTMLHeadingElement>;
}

const STUDENT_COLUMNS =
  'StudentId, SchoolName, FirstName, LastName, DateOfBirth, Grade, DisabilityCategory, HomeLanguage, CaseManagerEmail, IepDate, AnnualReviewDue, EtrDate, ReevaluationDue, Status';
const STAFF_COLUMNS = 'Email, FirstName, LastName, Role, SchoolName, Title';

// Step 1: fetch the server-generated workbook (its Values sheet carries the
// live list of schools/roles/case managers) and explain what goes in it.
export function TemplateStep({ kind, onContinue, headingRef }: TemplateStepProps) {
  const { t } = useTranslation('roster-import');
  const [isDownloading, setIsDownloading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleDownload = async () => {
    setIsDownloading(true);
    setError(null);
    try {
      const { blob, fileName } = await downloadImportTemplate(kind);
      downloadBlob(blob, fileName);
    } catch {
      setError(t('templateStep.downloadError'));
    } finally {
      setIsDownloading(false);
    }
  };

  // The workbook's own sheet names ("Students"/"Staff"/"Values") are a file
  // format detail (like a CSV column header), not UI copy — they stay
  // English regardless of the active language; only the surrounding
  // description is translated. See `docs/i18n/README.md`'s note on keeping
  // file-format tokens stable.
  const sheet = kind === 'Staff' ? 'Staff' : 'Students';
  const columns = kind === 'Staff' ? STAFF_COLUMNS : STUDENT_COLUMNS;
  const noun = kind === 'Staff' ? t('templateStep.nounStaff') : t('templateStep.nounStudent');

  return (
    <Card data-testid="import-template-step">
      <div className="space-y-4">
        <div>
          <h2
            ref={headingRef}
            tabIndex={-1}
            className="font-serif text-lg text-brand-slate-800 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-500"
          >
            {t('templateStep.heading')}
          </h2>
          <p className="mt-1 text-sm text-brand-slate-600">
            <Trans
              t={t}
              i18nKey="templateStep.intro"
              values={{ sheet, noun }}
              components={{
                sheetName: <span className="font-medium" />,
                values: <span className="font-medium" />,
              }}
            />
          </p>
          <p className="mt-2 text-xs text-brand-slate-500">{t('templateStep.columnsLabel', { columns })}</p>
          {kind === 'Students' && (
            <p className="mt-2 text-xs text-brand-slate-500">
              <Trans
                t={t}
                i18nKey="templateStep.studentNote"
                components={{ code: <code className="rounded-badge bg-brand-slate-100 px-1" /> }}
              />
            </p>
          )}
        </div>

        {error && (
          <div role="alert">
            <Notice variant="error" title={error} />
          </div>
        )}

        <div className="flex flex-wrap gap-2">
          <Button
            variant="secondary"
            loading={isDownloading}
            onClick={handleDownload}
            data-testid="import-template-download"
          >
            <Download className="mr-1.5 h-4 w-4" strokeWidth={1.8} aria-hidden="true" />
            {t('templateStep.downloadTemplate')}
          </Button>
          <Button onClick={onContinue} data-testid="import-template-continue">
            {t('templateStep.continueToUpload')}
          </Button>
        </div>
      </div>
    </Card>
  );
}
