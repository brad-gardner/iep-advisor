import { Link } from 'react-router-dom';
import { Users } from 'lucide-react';
import { useChildren } from '../hooks/use-children';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { SharedBadge } from '@/features/sharing/components/shared-badge';

export function ChildrenListPage() {
  const { children, isLoading } = useChildren();

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="font-serif">Your Children</h1>
        <Link to="/children/new">
          <Button data-testid="add-child-button">Add Child</Button>
        </Link>
      </div>

      {children.length === 0 ? (
        <Card className="text-center py-12" data-testid="children-empty-state">
          <Users className="w-12 h-12 mx-auto text-brand-slate-300 mb-3" strokeWidth={1.8} aria-hidden="true" />
          <p className="text-brand-slate-400 mb-4">No child profiles yet.</p>
          <Link to="/children/new">
            <Button>Add Your First Child</Button>
          </Link>
        </Card>
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
    </div>
  );
}
