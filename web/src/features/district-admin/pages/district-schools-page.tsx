import { useCallback, useEffect, useState } from 'react';
import { Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Modal } from '@/components/ui/modal';
import { Spinner } from '@/components/ui/spinner';
import { PageLayout } from '@/components/ui/page-layout';
import { useToast } from '@/components/ui/toast';
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
  const { show: showToast } = useToast();
  const [schools, setSchools] = useState<DistrictSchool[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isAddOpen, setIsAddOpen] = useState(false);

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
        // Close the host modal only on resolved success; errors stay rendered
        // inside the open dialog.
        setIsAddOpen(false);
        showToast({ message: 'School created', variant: 'success' });
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
        showToast({ message: 'School updated', variant: 'success' });
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
        showToast({ message: 'School deactivated', variant: 'success' });
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
    <PageLayout
      title="Schools"
      data-testid="district-schools-page"
      actions={
        <Button
          onClick={() => setIsAddOpen(true)}
          data-testid="district-schools-add"
        >
          <Plus className="h-4 w-4" strokeWidth={2} aria-hidden="true" />
          Add school
        </Button>
      }
    >
      {isLoading ? (
        <div className="flex justify-center py-12">
          <Spinner />
        </div>
      ) : (
        <SchoolsList
          schools={schools}
          onUpdate={handleUpdate}
          onDeactivate={handleDeactivate}
        />
      )}

      <Modal
        open={isAddOpen}
        onClose={() => setIsAddOpen(false)}
        title="Add a school"
        data-testid="district-schools-add-modal"
      >
        <SchoolForm
          mode="create"
          submitLabel="Add school"
          onSubmit={handleCreate}
          onCancel={() => setIsAddOpen(false)}
          testIdPrefix="district-schools-create"
        />
      </Modal>
    </PageLayout>
  );
}
