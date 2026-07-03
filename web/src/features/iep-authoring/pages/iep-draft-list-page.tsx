import { FileText, Plus } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/empty-state';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Spinner } from '@/components/ui/spinner';
import { useToast } from '@/components/ui/toast';
import { DraftListItem } from '../components/draft-list-item';
import { useIepDraftList } from '../hooks/use-iep-draft-list';

export function IepDraftListPage() {
  const { studentId: studentIdParam } = useParams<{ studentId: string }>();
  const studentId = Number(studentIdParam);
  const navigate = useNavigate();
  const { show } = useToast();
  const { drafts, isLoading, error, creating, create } = useIepDraftList(studentId);

  const handleCreate = async () => {
    const id = await create();
    if (id) {
      show({ message: 'IEP draft created', variant: 'success' });
      navigate(`/educator/students/${studentId}/iep-drafts/${id}`);
    }
  };

  const newDraftButton = (testId: string) => (
    <Button onClick={handleCreate} loading={creating} data-testid={testId}>
      <Plus className="w-4 h-4 mr-1" aria-hidden="true" />
      New IEP draft
    </Button>
  );

  return (
    <PageLayout
      title="IEP drafts"
      breadcrumb={[
        { label: 'Student', to: `/educator/students/${studentId}` },
        { label: 'IEP drafts' },
      ]}
      actions={newDraftButton('new-draft')}
    >
      {error && (
        <Notice variant="error" title="Could not load IEP drafts">
          {error}
        </Notice>
      )}

      {isLoading ? (
        <div className="flex justify-center py-12">
          <Spinner label="Loading IEP drafts…" />
        </div>
      ) : (
        <div className="space-y-3">
          {drafts.length === 0 && !error && (
            <EmptyState
              icon={FileText}
              title="No IEP drafts yet"
              description="Create one to start building this student's IEP."
              action={newDraftButton('new-draft-empty')}
              data-testid="empty-hint"
            />
          )}
          {drafts.map((draft) => (
            <DraftListItem
              key={draft.id}
              draft={draft}
              to={`/educator/students/${studentId}/iep-drafts/${draft.id}`}
            />
          ))}
        </div>
      )}
    </PageLayout>
  );
}
