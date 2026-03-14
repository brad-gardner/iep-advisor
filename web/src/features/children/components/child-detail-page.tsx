import { useEffect, useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import type { ChildProfile, CreateChildProfileRequest } from '@/types/api';
import { getChild, updateChild, deleteChild } from '../api/children-api';
import { ChildForm } from './child-form';
import { useIepDocuments } from '@/features/iep-documents/hooks/use-iep-documents';
import { IepUpload } from '@/features/iep-documents/components/iep-upload';
import { IepDocumentList } from '@/features/iep-documents/components/iep-document-list';
import { useAdvocacyGoals } from '@/features/advocacy-goals/hooks/use-advocacy-goals';
import { AdvocacyGoalsList } from '@/features/advocacy-goals/components/advocacy-goals-list';

export function ChildDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [child, setChild] = useState<ChildProfile | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isEditing, setIsEditing] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
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
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-gray-900" />
      </div>
    );
  }

  if (!child) {
    return (
      <div className="text-center py-12">
        <p className="text-gray-500">Child profile not found.</p>
        <Link to="/children" className="text-blue-600 hover:underline mt-2 inline-block">
          Back to children
        </Link>
      </div>
    );
  }

  if (isEditing) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <h1 className="text-3xl font-bold text-gray-900">Edit {child.firstName}</h1>
          <button
            onClick={() => setIsEditing(false)}
            className="text-sm text-gray-500 hover:text-gray-900"
          >
            Cancel
          </button>
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
        <h1 className="text-3xl font-bold text-gray-900">
          {child.firstName} {child.lastName}
        </h1>
        <div className="flex gap-2">
          <button
            onClick={() => setIsEditing(true)}
            className="px-4 py-2 bg-gray-200 hover:bg-gray-300 rounded text-sm transition-colors text-gray-700"
          >
            Edit
          </button>
          <button
            onClick={handleDelete}
            disabled={isDeleting}
            className="px-4 py-2 bg-red-50 hover:bg-red-100 text-red-600 rounded text-sm transition-colors disabled:opacity-50"
          >
            {isDeleting ? 'Removing...' : 'Remove'}
          </button>
        </div>
      </div>

      <div className="bg-white rounded-lg p-6 shadow-sm border border-gray-200">
        <h2 className="text-lg font-semibold mb-4 text-gray-900">Profile</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {child.dateOfBirth && (
            <div className="bg-gray-50 rounded p-3 border border-gray-200">
              <p className="text-gray-500 text-sm">Date of Birth</p>
              <p className="font-medium text-gray-900">
                {new Date(child.dateOfBirth).toLocaleDateString()}
              </p>
            </div>
          )}
          {child.gradeLevel && (
            <div className="bg-gray-50 rounded p-3 border border-gray-200">
              <p className="text-gray-500 text-sm">Grade Level</p>
              <p className="font-medium text-gray-900">{child.gradeLevel}</p>
            </div>
          )}
          {child.disabilityCategory && (
            <div className="bg-gray-50 rounded p-3 border border-gray-200">
              <p className="text-gray-500 text-sm">Disability Category</p>
              <p className="font-medium text-gray-900">{child.disabilityCategory}</p>
            </div>
          )}
          {child.schoolDistrict && (
            <div className="bg-gray-50 rounded p-3 border border-gray-200">
              <p className="text-gray-500 text-sm">School District</p>
              <p className="font-medium text-gray-900">{child.schoolDistrict}</p>
            </div>
          )}
        </div>
      </div>

      <div className="bg-white rounded-lg p-6 space-y-4 shadow-sm border border-gray-200">
        <h2 className="text-lg font-semibold text-gray-900">Your Advocacy Goals</h2>
        <AdvocacyGoalsList
          childId={Number(id)}
          childName={child.firstName}
          goals={goals}
          isLoading={goalsLoading}
          onReload={reloadGoals}
        />
      </div>

      <div className="bg-white rounded-lg p-6 space-y-4 shadow-sm border border-gray-200">
        <h2 className="text-lg font-semibold text-gray-900">IEP Documents</h2>
        <IepUpload childId={Number(id)} onUploaded={reloadDocs} />
        <IepDocumentList documents={documents} isLoading={docsLoading} onDeleted={reloadDocs} />
      </div>
    </div>
  );
}
