import { useState } from 'react';
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
  const [isDownloading, setIsDownloading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleDownload = async () => {
    setIsDownloading(true);
    setError(null);
    try {
      const { blob, fileName } = await downloadImportTemplate(kind);
      downloadBlob(blob, fileName);
    } catch {
      setError('Could not download the template. Try again.');
    } finally {
      setIsDownloading(false);
    }
  };

  const sheet = kind === 'Staff' ? 'Staff' : 'Students';
  const columns = kind === 'Staff' ? STAFF_COLUMNS : STUDENT_COLUMNS;

  return (
    <Card data-testid="import-template-step">
      <div className="space-y-4">
        <div>
          <h2
            ref={headingRef}
            tabIndex={-1}
            className="font-serif text-lg text-brand-slate-800 focus:outline-none"
          >
            Download the template
          </h2>
          <p className="mt-1 text-sm text-brand-slate-600">
            Fill in the <span className="font-medium">{sheet}</span> sheet — one row per{' '}
            {kind === 'Staff' ? 'staff member' : 'student'}. The <span className="font-medium">Values</span>{' '}
            sheet lists the allowed entries, including your district&apos;s schools.
          </p>
          <p className="mt-2 text-xs text-brand-slate-500">Columns: {columns}</p>
          {kind === 'Students' && (
            <p className="mt-2 text-xs text-brand-slate-500">
              Rows are matched on StudentId. Blank cells keep the current value; type{' '}
              <code className="rounded-badge bg-brand-slate-100 px-1">CLEAR</code> to clear one.
              Missing rows never exit anyone.
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
            Download template
          </Button>
          <Button onClick={onContinue} data-testid="import-template-continue">
            Continue to upload
          </Button>
        </div>
      </div>
    </Card>
  );
}
