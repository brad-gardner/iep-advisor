import { useNavigate } from 'react-router-dom';
import { createChild } from '../api/children-api';
import { ChildForm } from './child-form';
import { PageLayout } from '@/components/ui/page-layout';
import { useToast } from '@/components/ui/toast';
import type { CreateChildProfileRequest } from '@/types/api';

export function CreateChildPage() {
  const navigate = useNavigate();
  const { show: showToast } = useToast();

  const handleSubmit = async (data: CreateChildProfileRequest) => {
    try {
      const response = await createChild(data);
      if (response.success) {
        showToast({ message: 'Child profile added', variant: 'success' });
        navigate('/children');
        return { success: true };
      }
      return { success: false, error: response.message || 'Failed to create child profile' };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  return (
    <PageLayout title="Add Child">
      <ChildForm onSubmit={handleSubmit} submitLabel="Create Profile" />
    </PageLayout>
  );
}
