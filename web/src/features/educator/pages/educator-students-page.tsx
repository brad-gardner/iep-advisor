import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Spinner } from '@/components/ui/spinner';
import { Button } from '@/components/ui/button';
import { PageLayout } from '@/components/ui/page-layout';
import { useToast } from '@/components/ui/toast';
import { createStudent, getStudents } from '../api/educator-api';
import { getDistrictSchools, getDistrictDashboard } from '@/features/district-admin/api/district-api';
import type { DistrictSchool } from '@/features/district-admin/types';
import type { CreateSchoolStudentRequest, SchoolStudent } from '../types';
import { ORG_ROLE } from '../types';
import { useEducatorProfile } from '../hooks/use-educator-profile';
import { StudentList } from '../components/student-list';
import { CreateStudentForm } from '../components/create-student-form';
import { SchoolFilter } from '../components/school-filter';

const TEACHER_EMPTY =
  'No students assigned to you yet — your school admin can assign students, or create one.';

// Dashboard "needs attention" tiles deep-link here with ?attention=<key>. The
// roster payload carries no assigned-staff / linked-parent signal, so the ID
// set is sourced from the dashboard aggregate (admins only).
const ATTENTION_LABELS: Record<string, string> = {
  'no-staff': 'no assigned staff',
  'no-parent': 'no linked parent',
};

export function EducatorStudentsPage() {
  const { show: showToast } = useToast();
  const { profile } = useEducatorProfile();
  const isDistrictAdmin = profile?.orgRoleId === ORG_ROLE.DistrictAdmin;
  const isSchoolAdmin = profile?.orgRoleId === ORG_ROLE.SchoolAdmin;
  const isAdmin = isDistrictAdmin || isSchoolAdmin;
  const isTeacher = profile?.orgRoleId === ORG_ROLE.Teacher;

  const [searchParams, setSearchParams] = useSearchParams();
  const attention = searchParams.get('attention');
  const attentionLabel = attention ? ATTENTION_LABELS[attention] : undefined;

  const [students, setStudents] = useState<SchoolStudent[]>([]);
  const [schools, setSchools] = useState<DistrictSchool[]>([]);
  const [schoolFilter, setSchoolFilter] = useState('');
  const [attentionIds, setAttentionIds] = useState<Set<number> | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const reload = useCallback(async () => {
    try {
      const response = await getStudents();
      if (response.success && response.data) {
        setStudents(response.data);
      }
    } catch {
      // Leave the list empty on a server/network error rather than surfacing
      // an unhandled rejection; the empty state communicates "no students".
      setStudents([]);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    reload();
  }, [reload]);

  // DistrictAdmin needs the district's schools for both the create-form picker
  // and the roster filter. Other roles never see a school picker.
  useEffect(() => {
    if (!isDistrictAdmin) return;
    let active = true;
    (async () => {
      try {
        const response = await getDistrictSchools();
        if (active && response.success && response.data) {
          setSchools(response.data);
        }
      } catch {
        if (active) setSchools([]);
      }
    })();
    return () => {
      active = false;
    };
  }, [isDistrictAdmin]);

  // Resolve the attention filter's ID set from the dashboard aggregate whenever
  // an admin arrives with ?attention=<key>. A recognised key with no matching
  // dashboard data yields an empty set (filters to nothing), not an unfiltered
  // roster, so the deep-link never silently shows everyone. When the param is
  // absent the memo below ignores any lingering set, so no reset is needed here.
  useEffect(() => {
    if (!isAdmin || !attentionLabel) return;
    let active = true;
    (async () => {
      try {
        const response = await getDistrictDashboard();
        if (!active) return;
        const data = response.success ? response.data : null;
        const source =
          attention === 'no-staff'
            ? data?.studentsWithoutStaff
            : data?.studentsWithoutParent;
        setAttentionIds(new Set((source ?? []).map((s) => s.schoolStudentId)));
      } catch {
        if (active) setAttentionIds(new Set());
      }
    })();
    return () => {
      active = false;
    };
  }, [isAdmin, attention, attentionLabel]);

  const clearAttention = () => {
    searchParams.delete('attention');
    setSearchParams(searchParams, { replace: true });
  };

  const handleCreate = async (data: CreateSchoolStudentRequest) => {
    try {
      const response = await createStudent(data);
      if (response.success) {
        await reload();
        showToast({ message: 'Student added', variant: 'success' });
        return { success: true };
      }
      return { success: false, error: response.message || 'Failed to add student' };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  const visibleStudents = useMemo(() => {
    let result = students;
    if (isDistrictAdmin && schoolFilter) {
      result = result.filter((s) => String(s.schoolId) === schoolFilter);
    }
    if (attentionLabel && attentionIds) {
      result = result.filter((s) => attentionIds.has(s.id));
    }
    return result;
  }, [students, isDistrictAdmin, schoolFilter, attentionLabel, attentionIds]);

  return (
    <PageLayout title="Students" data-testid="educator-students-page">
      <CreateStudentForm
        onSubmit={handleCreate}
        schools={isDistrictAdmin ? schools : undefined}
      />

      {isDistrictAdmin && (
        <SchoolFilter
          schools={schools}
          value={schoolFilter}
          onChange={setSchoolFilter}
        />
      )}

      {isAdmin && attentionLabel && (
        <div
          className="flex items-center justify-between gap-3 rounded-card border border-brand-amber-100 bg-brand-amber-50 px-4 py-2 text-sm"
          data-testid="attention-filter-indicator"
        >
          <span className="text-brand-amber-600">
            Showing students with {attentionLabel}
          </span>
          <Button
            variant="ghost"
            size="sm"
            onClick={clearAttention}
            data-testid="attention-filter-clear"
          >
            Clear
          </Button>
        </div>
      )}

      {isLoading ? (
        <div className="flex justify-center py-12">
          <Spinner label="Loading students…" />
        </div>
      ) : (
        <StudentList
          students={visibleStudents}
          showSchool={isDistrictAdmin}
          emptyMessage={isTeacher ? TEACHER_EMPTY : undefined}
        />
      )}
    </PageLayout>
  );
}
