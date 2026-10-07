import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AxiosError } from 'axios';
import { Button } from '@/components/ui/button';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { Select } from '@/components/ui/input';
import { Spinner } from '@/components/ui/spinner';
import type { ApiResponse } from '@/types/api';
import { loadErrorText } from '@/lib/api-error';
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
  const { t } = useTranslation('document-authoring');
  // The create request outlives a dismiss gesture, so while it is in flight the dialog cannot be
  // closed — otherwise a "cancelled" create would still land and navigate to the new document.
  const [creating, setCreating] = useState(false);
  return (
    <Modal open={open} onClose={onClose} preventClose={creating} title={t('newDocumentModal.title')} data-testid="new-document-modal">
      {open && (
        <NewDocumentForm studentId={studentId} onClose={onClose} onCreated={onCreated} onCreatingChange={setCreating} />
      )}
    </Modal>
  );
}

function NewDocumentForm({
  studentId,
  onClose,
  onCreated,
  onCreatingChange,
}: Omit<NewDocumentModalProps, 'open'> & { onCreatingChange: (creating: boolean) => void }) {
  const { t } = useTranslation('document-authoring');
  const { types, isLoading, error } = useDocumentTypes();
  const [selectedId, setSelectedId] = useState<number | ''>('');
  const [creating, setCreatingLocal] = useState(false);
  const setCreating = (value: boolean) => {
    setCreatingLocal(value);
    onCreatingChange(value);
  };
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
      setCreateError(res.message ?? t('newDocumentModal.createGenericError'));
    } catch (err) {
      if (err instanceof AxiosError) {
        const body = err.response?.data as ApiResponse<unknown> | undefined;
        // 422 → no template available for this student's state + type.
        setCreateError(body?.message ?? t('newDocumentModal.createGenericError'));
      } else {
        setCreateError(t('newDocumentModal.createGenericError'));
      }
    } finally {
      setCreating(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-6">
        <Spinner label={t('newDocumentModal.loadingTypes')} />
      </div>
    );
  }

  if (error) {
    return (
      <Notice variant="error" title={t('newDocumentModal.loadTypesErrorTitle')}>
        {loadErrorText(error, t('newDocumentModal.loadTypesErrorGeneric'))}
      </Notice>
    );
  }

  if (types.length === 0) {
    return (
      <Notice variant="info" title={t('newDocumentModal.noTypesTitle')}>
        {t('newDocumentModal.noTypesDescription')}
      </Notice>
    );
  }

  return (
    <div className="space-y-4">
      <Select
        label={t('newDocumentModal.typeLabel')}
        value={effectiveId}
        onChange={(e) => setSelectedId(Number(e.target.value))}
        data-testid="new-document-type"
      >
        {types.map((docType) => (
          <option key={docType.id} value={docType.id}>
            {docType.displayName}
          </option>
        ))}
      </Select>
      <p className="text-sm text-brand-slate-500">{t('newDocumentModal.templateHint')}</p>
      {createError && (
        <div role="alert">
          <Notice variant="error" title={t('newDocumentModal.createErrorTitle')}>
            {createError}
          </Notice>
        </div>
      )}
      <div className="flex items-center justify-end gap-2 pt-2">
        <Button variant="ghost" onClick={onClose} disabled={creating}>
          {t('newDocumentModal.cancel')}
        </Button>
        <Button onClick={handleCreate} loading={creating} data-testid="new-document-create">
          {t('newDocumentModal.create')}
        </Button>
      </div>
    </div>
  );
}
