import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import {
  getStudent,
  getStudentLinks,
  inviteParent,
  revokeStudentLink,
} from '../api/educator-api';
import type { ChildLink, SchoolStudent } from '../types';
import { InviteParentForm } from '../components/invite-parent-form';
import { StudentLinksList } from '../components/student-links-list';

export function EducatorStudentDetailPage() {
  const { studentId: studentIdParam } = useParams<{ studentId: string }>();
  const studentId = Number(studentIdParam);

  const [student, setStudent] = useState<SchoolStudent | null>(null);
  const [links, setLinks] = useState<ChildLink[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [revokingId, setRevokingId] = useState<number | null>(null);
  const [revokeNote, setRevokeNote] = useState<string | null>(null);

  const reloadLinks = useCallback(async () => {
    try {
      const response = await getStudentLinks(studentId);
      if (response.success && response.data) {
        setLinks(response.data);
      }
    } catch {
      // Refetch failure keeps the existing links rather than crashing the page.
    }
  }, [studentId]);

  useEffect(() => {
    async function load() {
      try {
        const studentRes = await getStudent(studentId);
        if (studentRes.success && studentRes.data) {
          setStudent(studentRes.data);
        }
        await reloadLinks();
      } catch {
        // A server/network error leaves `student` null → the "not found" state
        // renders, rather than surfacing an unhandled rejection.
      } finally {
        setIsLoading(false);
      }
    }
    load();
  }, [studentId, reloadLinks]);

  const handleInvite = async (email: string) => {
    try {
      const response = await inviteParent(studentId, { parentEmail: email });
      if (response.success) {
        await reloadLinks();
        return { success: true, message: response.message };
      }
      return { success: false, message: response.message };
    } catch {
      return { success: false, message: 'An error occurred sending the invitation' };
    }
  };

  const handleRevoke = async (link: ChildLink) => {
    if (!confirm('Revoke this parent link? This cannot be undone, and the parent keeps any data already shared.')) {
      return;
    }
    setRevokingId(link.id);
    setRevokeNote(null);
    try {
      const response = await revokeStudentLink(studentId, link.id);
      if (response.success) {
        // Surface the forward-only note from the server (revoke is not retroactive).
        setRevokeNote(response.message || 'Link revoked. This does not remove access already granted.');
        await reloadLinks();
      }
    } finally {
      setRevokingId(null);
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (!student) {
    return (
      <div className="text-center py-12">
        <p className="text-brand-slate-400">Student not found.</p>
        <Link
          to="/educator/students"
          className="text-brand-teal-500 hover:underline mt-2 inline-block"
        >
          Back to students
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <Link
          to="/educator/students"
          className="text-sm text-brand-teal-500 hover:underline"
        >
          ← Back to students
        </Link>
        <h1 className="font-serif mt-2">
          {student.firstName} {student.lastName ?? ''}
        </h1>
      </div>

      <Card className="max-w-lg" data-testid="student-info">
        <dl className="space-y-2 text-sm">
          {student.gradeLevel && (
            <div className="flex justify-between">
              <dt className="text-brand-slate-500">Grade</dt>
              <dd className="text-brand-slate-800">{student.gradeLevel}</dd>
            </div>
          )}
          {student.disabilityCategory && (
            <div className="flex justify-between">
              <dt className="text-brand-slate-500">Disability Category</dt>
              <dd className="text-brand-slate-800">{student.disabilityCategory}</dd>
            </div>
          )}
        </dl>
      </Card>

      <InviteParentForm onInvite={handleInvite} />

      <section className="space-y-3">
        <h2 className="font-serif text-lg">Parent links</h2>
        {revokeNote && <Notice variant="info" title="Link revoked">{revokeNote}</Notice>}
        <StudentLinksList links={links} revokingId={revokingId} onRevoke={handleRevoke} />
      </section>
    </div>
  );
}
