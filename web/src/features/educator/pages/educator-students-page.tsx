import { useCallback, useEffect, useMemo, useState } from 'react';
import { createStudent, getStudents } from '../api/educator-api';
import { getDistrictSchools } from '@/features/district-admin/api/district-api';
import type { DistrictSchool } from '@/features/district-admin/types';
import type { CreateSchoolStudentRequest, SchoolStudent } from '../types';
import { ORG_ROLE } from '../types';
import { useEducatorProfile } from '../hooks/use-educator-profile';
import { StudentList } from '../components/student-list';
import { CreateStudentForm } from '../components/create-student-form';
import { SchoolFilter } from '../components/school-filter';

const TEACHER_EMPTY =
  'No students assigned to you yet — your school admin can assign students, or create one.';

export function EducatorStudentsPage() {
  const { profile } = useEducatorProfile();
  const isDistrictAdmin = profile?.orgRoleId === ORG_ROLE.DistrictAdmin;
  const isTeacher = profile?.orgRoleId === ORG_ROLE.Teacher;

  const [students, setStudents] = useState<SchoolStudent[]>([]);
  const [schools, setSchools] = useState<DistrictSchool[]>([]);
  const [schoolFilter, setSchoolFilter] = useState('');
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

  const handleCreate = async (data: CreateSchoolStudentRequest) => {
    try {
      const response = await createStudent(data);
      if (response.success) {
        await reload();
        return { success: true };
      }
      return { success: false, error: response.message || 'Failed to add student' };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  const visibleStudents = useMemo(() => {
    if (!isDistrictAdmin || !schoolFilter) return students;
    return students.filter((s) => String(s.schoolId) === schoolFilter);
  }, [students, isDistrictAdmin, schoolFilter]);

  return (
    <div className="space-y-6">
      <h1 className="font-serif">Students</h1>

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

      {isLoading ? (
        <div className="flex justify-center py-12">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
        </div>
      ) : (
        <StudentList
          students={visibleStudents}
          showSchool={isDistrictAdmin}
          emptyMessage={isTeacher ? TEACHER_EMPTY : undefined}
        />
      )}
    </div>
  );
}
