import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation('document-authoring');
  const { instanceId: instanceIdParam } = useParams<{ instanceId: string }>();
  const instanceId = Number(instanceIdParam);
  const instance = useDocumentInstance(instanceId);
  const { detail, isLoading, loadError } = instance;
  usePageTitle(detail ? detail.documentTypeDisplayName : t('editorPage.titleFallback'));

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
        <Spinner label={t('editorPage.loading')} />
      </div>
    );
  }

  if (loadError || !detail) {
    return (
      <EmptyState
        title={t('editorPage.notFoundTitle')}
        description={
          loadError
            ? loadError.kind === 'server'
              ? loadError.message
              : t('editorPage.loadErrorGeneric')
            : t('editorPage.notFoundDescription')
        }
        action={
          <Link to="/educator/students">
            <Button variant="secondary">{t('editorPage.backToStudents')}</Button>
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
          {t('editorPage.backToDocuments')}
        </Link>
      </div>

      <div role="tablist" className="flex border-b border-brand-slate-200" aria-label={t('editorPage.viewsAriaLabel')}>
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'edit'}
          onClick={() => setTab('edit')}
          data-testid="document-tab-edit"
          className={`px-4 py-2 text-[13px] font-medium transition-colors ${
            tab === 'edit'
              ? 'border-b-2 border-brand-teal-500 text-brand-slate-800'
              : 'text-brand-slate-500 hover:text-brand-slate-800'
          }`}
        >
          {t('editorPage.tabEdit')}
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
              : 'text-brand-slate-500 hover:text-brand-slate-800'
          }`}
        >
          {t('editorPage.tabConverge')}
        </button>
      </div>

      {/* The editor stays mounted while Converge is showing: its autosave queue and the
          ephemeral assistant chat thread live in component state and must survive a tab
          round-trip. Converge is cheap to remount, so it is rendered only when selected. */}
      <div hidden={tab !== 'edit'}>
        <DocumentEditor detail={detail} instance={instance} />
      </div>
      {tab === 'converge' && <ConvergeTab detail={detail} onShowEditor={() => setTab('edit')} />}
    </div>
  );
}
