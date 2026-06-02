import { Plus } from 'lucide-react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { DraftListItem } from '../components/draft-list-item';
import { EmptyHint } from '../components/empty-hint';
import { useIepDraftList } from '../hooks/use-iep-draft-list';

export function IepDraftListPage() {
  const { studentId: studentIdParam } = useParams<{ studentId: string }>();
  const studentId = Number(studentIdParam);
  const navigate = useNavigate();
  const { drafts, isLoading, error, creating, create } = useIepDraftList(studentId);

  const handleCreate = async () => {
    const id = await create();
    if (id) navigate(`/educator/students/${studentId}/iep-drafts/${id}`);
  };

  return (
    <div className="space-y-6">
      <div>
        <Link
          to={`/educator/students/${studentId}`}
          className="text-sm text-brand-teal-500 hover:underline"
        >
          ← Back to student
        </Link>
        <div className="flex items-center justify-between gap-4 mt-2">
          <h1 className="font-serif">IEP drafts</h1>
          <Button onClick={handleCreate} disabled={creating} data-testid="new-draft">
            <Plus className="w-4 h-4 mr-1" aria-hidden="true" />
            {creating ? 'Creating…' : 'New IEP draft'}
          </Button>
        </div>
      </div>

      {error && (
        <Notice variant="error" title="Could not load IEP drafts">
          {error}
        </Notice>
      )}

      {isLoading ? (
        <div className="flex justify-center py-12">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
        </div>
      ) : (
        <div className="space-y-3">
          {drafts.length === 0 && !error && (
            <EmptyHint>No IEP drafts yet. Create one to start building.</EmptyHint>
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
    </div>
  );
}
