import { useCallback, useEffect, useState } from 'react';
import { createStudent, getStudents } from '../api/educator-api';
import type { CreateSchoolStudentRequest, SchoolStudent } from '../types';
import { StudentList } from '../components/student-list';
import { CreateStudentForm } from '../components/create-student-form';

export function EducatorStudentsPage() {
  const [students, setStudents] = useState<SchoolStudent[]>([]);
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

  return (
    <div className="space-y-6">
      <h1 className="font-serif">Students</h1>

      <CreateStudentForm onSubmit={handleCreate} />

      {isLoading ? (
        <div className="flex justify-center py-12">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
        </div>
      ) : (
        <StudentList students={students} />
      )}
    </div>
  );
}
