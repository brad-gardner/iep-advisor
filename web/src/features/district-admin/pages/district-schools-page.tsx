import { useCallback, useEffect, useState } from 'react';
import { Card } from '@/components/ui/card';
import { reloadEducatorProfile } from '@/features/educator/hooks/use-educator-profile';
import {
  createSchool,
  deactivateSchool,
  getDistrictSchools,
  updateSchool,
} from '../api/district-api';
import { SchoolForm } from '../components/school-form';
import { SchoolsList } from '../components/schools-list';
import type { DistrictSchool, SaveSchoolRequest } from '../types';

export function DistrictSchoolsPage() {
  const [schools, setSchools] = useState<DistrictSchool[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const reload = useCallback(async () => {
    try {
      const response = await getDistrictSchools();
      setSchools(response.success && response.data ? response.data : []);
    } catch {
      setSchools([]);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    reload();
  }, [reload]);

  const handleCreate = async (data: SaveSchoolRequest) => {
    try {
      const response = await createSchool(data);
      if (response.success) {
        await reload();
        // School counts in the district overview are now stale.
        void reloadEducatorProfile();
        return { success: true };
      }
      return { success: false, error: response.message || 'Failed to add school' };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  const handleUpdate = async (schoolId: number, data: SaveSchoolRequest) => {
    try {
      const response = await updateSchool(schoolId, data);
      if (response.success) {
        await reload();
        return { success: true };
      }
      return { success: false, error: response.message || 'Failed to update school' };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  const handleDeactivate = async (schoolId: number) => {
    try {
      const response = await deactivateSchool(schoolId);
      if (response.success) {
        await reload();
        void reloadEducatorProfile();
        return { success: true };
      }
      // The backend returns an explicit message when a school still has active
      // students or staff — surface it verbatim.
      return {
        success: false,
        error: response.message || 'This school cannot be deactivated right now',
      };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  return (
    <div className="space-y-6">
      <h1 className="font-serif">Schools</h1>

      <Card className="max-w-lg">
        <h2 className="font-serif text-lg mb-4">Add a school</h2>
        <SchoolForm
          mode="create"
          submitLabel="Add school"
          onSubmit={handleCreate}
          testIdPrefix="district-schools-create"
        />
      </Card>

      {isLoading ? (
        <div className="flex justify-center py-12">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
        </div>
      ) : (
        <SchoolsList
          schools={schools}
          onUpdate={handleUpdate}
          onDeactivate={handleDeactivate}
        />
      )}
    </div>
  );
}
