import { useEffect, useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import type { ChildProfile, CreateChildProfileRequest } from '@/types/api';
import { getChild, updateChild, deleteChild } from '../api/children-api';
import { ChildForm } from './child-form';
import { useIepDocuments } from '@/features/iep-documents/hooks/use-iep-documents';
import { CreateIepForm } from '@/features/iep-documents/components/create-iep-form';
import { IepDocumentList } from '@/features/iep-documents/components/iep-document-list';
import { useAdvocacyGoals } from '@/features/advocacy-goals/hooks/use-advocacy-goals';
import { AdvocacyGoalsList } from '@/features/advocacy-goals/components/advocacy-goals-list';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';

export function ChildDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [child, setChild] = useState<ChildProfile | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isEditing, setIsEditing] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [showCreateIep, setShowCreateIep] = useState(false);
  const { documents, isLoading: docsLoading, reload: reloadDocs } = useIepDocuments(Number(id));
  const { goals, isLoading: goalsLoading, reload: reloadGoals } = useAdvocacyGoals(Number(id));

  useEffect(() => {
    async function load() {
      try {
        const response = await getChild(Number(id));
        if (response.success && response.data) {
          setChild(response.data);
        }
      } catch {
        // handled by interceptor
      } finally {
        setIsLoading(false);
      }
    }
    load();
  }, [id]);

  const handleUpdate = async (data: CreateChildProfileRequest) => {
    try {
      const response = await updateChild(Number(id), data);
      if (response.success) {
        const refreshed = await getChild(Number(id));
        if (refreshed.success && refreshed.data) {
          setChild(refreshed.data);
        }
        setIsEditing(false);
        return { success: true };
      }
      return { success: false, error: response.message || 'Update failed' };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  const handleDelete = async () => {
    if (!confirm('Are you sure you want to remove this child profile?')) return;
    setIsDeleting(true);
    try {
      const response = await deleteChild(Number(id));
      if (response.success) {
        navigate('/children');
      }
    } catch {
      // handled by interceptor
    } finally {
      setIsDeleting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (!child) {
    return (
      <div className="text-center py-12">
        <p className="text-brand-slate-400">Child profile not found.</p>
        <Link to="/children" className="text-brand-teal-500 hover:underline mt-2 inline-block">
          Back to children
        </Link>
      </div>
    );
  }

  if (isEditing) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <h1 className="font-serif">Edit {child.firstName}</h1>
          <Button variant="ghost" onClick={() => setIsEditing(false)}>
            Cancel
          </Button>
        </div>
        <ChildForm
          initialValues={{
            firstName: child.firstName,
            lastName: child.lastName ?? '',
            dateOfBirth: child.dateOfBirth?.split('T')[0] ?? '',
            gradeLevel: child.gradeLevel ?? '',
            disabilityCategory: child.disabilityCategory ?? '',
            schoolDistrict: child.schoolDistrict ?? '',
          }}
          onSubmit={handleUpdate}
          submitLabel="Save Changes"
        />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="font-serif">
          {child.firstName} {child.lastName}
        </h1>
        <div className="flex gap-2">
          <Button variant="secondary" onClick={() => setIsEditing(true)}>
            Edit
          </Button>
          <Button variant="danger" onClick={handleDelete} disabled={isDeleting}>
            {isDeleting ? 'Removing...' : 'Remove'}
          </Button>
        </div>
      </div>

      <Card>
        <h2 className="font-serif mb-4">Profile</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {child.dateOfBirth && (
            <div className="bg-brand-slate-50 rounded-card p-3 border-[0.5px] border-brand-slate-200">
              <p className="text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold">Date of Birth</p>
              <p className="text-sm font-medium text-brand-slate-800 mt-1">
                {new Date(child.dateOfBirth).toLocaleDateString()}
              </p>
            </div>
          )}
          {child.gradeLevel && (
            <div className="bg-brand-slate-50 rounded-card p-3 border-[0.5px] border-brand-slate-200">
              <p className="text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold">Grade Level</p>
              <p className="text-sm font-medium text-brand-slate-800 mt-1">{child.gradeLevel}</p>
            </div>
          )}
          {child.disabilityCategory && (
            <div className="bg-brand-slate-50 rounded-card p-3 border-[0.5px] border-brand-slate-200">
              <p className="text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold">Disability Category</p>
              <p className="text-sm font-medium text-brand-slate-800 mt-1">{child.disabilityCategory}</p>
            </div>
          )}
          {child.schoolDistrict && (
            <div className="bg-brand-slate-50 rounded-card p-3 border-[0.5px] border-brand-slate-200">
              <p className="text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold">School District</p>
              <p className="text-sm font-medium text-brand-slate-800 mt-1">{child.schoolDistrict}</p>
            </div>
          )}
        </div>
      </Card>

      <Card>
        <h2 className="font-serif mb-4">Your Advocacy Goals</h2>
        <AdvocacyGoalsList
          childId={Number(id)}
          childName={child.firstName}
          goals={goals}
          isLoading={goalsLoading}
          onReload={reloadGoals}
        />
      </Card>

      <Card>
        <div className="flex justify-between items-center mb-4">
          <h2 className="font-serif">IEP Documents</h2>
          {!showCreateIep && (
            <Button variant="secondary" onClick={() => setShowCreateIep(true)}>
              New IEP
            </Button>
          )}
        </div>
        {showCreateIep && (
          <div className="mb-4">
            <CreateIepForm
              childId={Number(id)}
              onCreated={() => {
                setShowCreateIep(false);
                reloadDocs();
              }}
              onCancel={() => setShowCreateIep(false)}
            />
          </div>
        )}
        <IepDocumentList documents={documents} isLoading={docsLoading} onDeleted={reloadDocs} />
      </Card>
    </div>
  );
}
