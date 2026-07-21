import { useState } from 'react';
import { AxiosError } from 'axios';
import { Button } from '@/components/ui/button';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { Select } from '@/components/ui/input';
import { Spinner } from '@/components/ui/spinner';
import type { ApiResponse } from '@/types/api';
import { createDocument } from '../api/documents-api';
import { useDocumentTypes } from '../hooks/use-document-types';

interface NewDocumentModalProps {
  studentId: number;
  open: boolean;
  onClose: () => void;
  /** Called with the new instance id after a successful create. */
  onCreated: (instanceId: number) => void;
}

/**
 * Picks a document type and creates an instance. Template resolution happens
 * server-side; a **422** (no template available for the student's state+type)
 * is surfaced inline as a graceful message, not a crash.
 *
 * The form body is a child that mounts only while `open`, so its type-loading
 * hook starts fresh each time (no reset-on-open effect).
 */
export function NewDocumentModal({ studentId, open, onClose, onCreated }: NewDocumentModalProps) {
  return (
    <Modal open={open} onClose={onClose} title="New document" data-testid="new-document-modal">
      {open && <NewDocumentForm studentId={studentId} onClose={onClose} onCreated={onCreated} />}
    </Modal>
  );
}

function NewDocumentForm({
  studentId,
  onClose,
  onCreated,
}: Omit<NewDocumentModalProps, 'open'>) {
  const { types, isLoading, error } = useDocumentTypes();
  const [selectedId, setSelectedId] = useState<number | ''>('');
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  // Default to the first type until the user picks one — derived, so no effect.
  const effectiveId: number | '' = selectedId === '' && types.length > 0 ? types[0].id : selectedId;

  const handleCreate = async () => {
    if (effectiveId === '') return;
    setCreating(true);
    setCreateError(null);
    try {
      const res = await createDocument(studentId, effectiveId);
      if (res.success && res.data) {
        onCreated(res.data.id);
        return;
      }
      setCreateError(res.message ?? 'Could not create the document.');
    } catch (err) {
      if (err instanceof AxiosError) {
        const body = err.response?.data as ApiResponse<unknown> | undefined;
        // 422 → no template available for this student's state + type.
        setCreateError(body?.message ?? 'Could not create the document.');
      } else {
        setCreateError('Could not create the document.');
      }
    } finally {
      setCreating(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-6">
        <Spinner label="Loading document types…" />
      </div>
    );
  }

  if (error) {
    return (
      <Notice variant="error" title="Could not load document types">
        {error}
      </Notice>
    );
  }

  if (types.length === 0) {
    return (
      <Notice variant="info" title="No document types available">
        Ask an administrator to enable a document type.
      </Notice>
    );
  }

  return (
    <div className="space-y-4">
      <Select
        label="Document type"
        value={effectiveId}
        onChange={(e) => setSelectedId(Number(e.target.value))}
        data-testid="new-document-type"
      >
        {types.map((t) => (
          <option key={t.id} value={t.id}>
            {t.displayName}
          </option>
        ))}
      </Select>
      <p className="text-sm text-brand-slate-500">
        The matching template for this student's state is applied automatically.
      </p>
      {createError && (
        <div role="alert">
          <Notice variant="error" title="Cannot create this document">
            {createError}
          </Notice>
        </div>
      )}
      <div className="flex items-center justify-end gap-2 pt-2">
        <Button variant="ghost" onClick={onClose} disabled={creating}>
          Cancel
        </Button>
        <Button onClick={handleCreate} loading={creating} data-testid="new-document-create">
          Create document
        </Button>
      </div>
    </div>
  );
}
