import { Link } from 'react-router-dom';
import { Users } from 'lucide-react';
import { useChildren } from '../hooks/use-children';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import { EmptyState } from '@/components/ui/empty-state';
import { PageLayout } from '@/components/ui/page-layout';
import { SharedBadge } from '@/features/sharing/components/shared-badge';

export function ChildrenListPage() {
  const { children, isLoading } = useChildren();

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label="Loading children…" />
      </div>
    );
  }

  return (
    <PageLayout
      title="Your Children"
      actions={
        <Link to="/children/new">
          <Button data-testid="add-child-button">Add Child</Button>
        </Link>
      }
    >
      {children.length === 0 ? (
        <EmptyState
          icon={Users}
          title="No child profiles yet."
          action={
            <Link to="/children/new">
              <Button>Add Your First Child</Button>
            </Link>
          }
          data-testid="children-empty-state"
        />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {children.map((child) => (
            <Link
              key={child.id}
              to={`/children/${child.id}`}
              className="block"
            >
              <Card className="hover:border-brand-teal-200 transition-colors" data-testid="child-card">
                <h3 className="font-serif text-brand-slate-800">
                  {child.firstName} {child.lastName}
                </h3>
                {child.role !== 'owner' && (
                  <div className="mt-1">
                    <SharedBadge role={child.role} />
                  </div>
                )}
                <div className="mt-2 flex flex-wrap gap-3 text-xs text-brand-slate-400">
                  {child.gradeLevel && <span>Grade: {child.gradeLevel}</span>}
                  {child.disabilityCategory && <span>{child.disabilityCategory}</span>}
                  {child.schoolDistrict && <span>{child.schoolDistrict}</span>}
                </div>
              </Card>
            </Link>
          ))}
        </div>
      )}
    </PageLayout>
  );
}
