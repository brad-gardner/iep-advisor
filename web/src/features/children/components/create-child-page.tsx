import { useNavigate } from 'react-router-dom';
import { createChild } from '../api/children-api';
import { ChildForm } from './child-form';
import type { CreateChildProfileRequest } from '@/types/api';

export function CreateChildPage() {
  const navigate = useNavigate();

  const handleSubmit = async (data: CreateChildProfileRequest) => {
    try {
      const response = await createChild(data);
      if (response.success) {
        navigate('/children');
        return { success: true };
      }
      return { success: false, error: response.message || 'Failed to create child profile' };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  return (
    <div className="space-y-6">
      <h1 className="font-serif">Add Child</h1>
      <ChildForm onSubmit={handleSubmit} submitLabel="Create Profile" />
    </div>
  );
}
