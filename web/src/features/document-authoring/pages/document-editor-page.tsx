import { Link, useParams, useSearchParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/empty-state';
import { Spinner } from '@/components/ui/spinner';
import { ConvergeTab } from '@/features/draft-sharing/components/converge-tab';
import { useDocumentInstance } from '../hooks/use-document-instance';
import { DocumentEditor } from '../components/document-editor';
import { usePageTitle } from '@/hooks/use-page-title';

type DocumentTab = 'edit' | 'converge';

function parseTab(raw: string | null): DocumentTab {
  return raw === 'converge' ? 'converge' : 'edit';
}

export function DocumentEditorPage() {
  const { instanceId: instanceIdParam } = useParams<{ instanceId: string }>();
  const instanceId = Number(instanceIdParam);
  const instance = useDocumentInstance(instanceId);
  const { detail, isLoading, loadError } = instance;
  usePageTitle(detail ? detail.documentTypeDisplayName : 'Document');

  const [searchParams, setSearchParams] = useSearchParams();
  const tab = parseTab(searchParams.get('tab'));
  const setTab = (next: DocumentTab) => {
    const params = new URLSearchParams(searchParams);
    if (next === 'edit') params.delete('tab');
    else params.set('tab', next);
    setSearchParams(params, { replace: true });
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label="Loading document…" />
      </div>
    );
  }

  if (loadError || !detail) {
    return (
      <EmptyState
        title="Document not found"
        description={loadError ?? 'This document may have been removed, or you may not have access.'}
        action={
          <Link to="/educator/students">
            <Button variant="secondary">Back to students</Button>
          </Link>
        }
      />
    );
  }

  return (
    <div className="space-y-6" data-testid="document-editor-page">
      <div>
        <Link
          to={`/educator/students/${detail.schoolStudentId}/documents`}
          className="text-sm text-brand-teal-600 hover:underline"
        >
          ← Back to documents
        </Link>
      </div>

      <div role="tablist" className="flex border-b border-brand-slate-200" aria-label="Document views">
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'edit'}
          onClick={() => setTab('edit')}
          data-testid="document-tab-edit"
          className={`px-4 py-2 text-[13px] font-medium transition-colors ${
            tab === 'edit'
              ? 'border-b-2 border-brand-teal-500 text-brand-slate-800'
              : 'text-brand-slate-400 hover:text-brand-slate-800'
          }`}
        >
          Edit
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'converge'}
          onClick={() => setTab('converge')}
          data-testid="document-tab-converge"
          className={`px-4 py-2 text-[13px] font-medium transition-colors ${
            tab === 'converge'
              ? 'border-b-2 border-brand-teal-500 text-brand-slate-800'
              : 'text-brand-slate-400 hover:text-brand-slate-800'
          }`}
        >
          Converge
        </button>
      </div>

      {/* The editor stays mounted while Converge is showing: its autosave queue and the
          ephemeral assistant chat thread live in component state and must survive a tab
          round-trip. Converge is cheap to remount, so it is rendered only when selected. */}
      <div hidden={tab !== 'edit'}>
        <DocumentEditor detail={detail} instance={instance} />
      </div>
      {tab === 'converge' && <ConvergeTab detail={detail} />}
    </div>
  );
}
