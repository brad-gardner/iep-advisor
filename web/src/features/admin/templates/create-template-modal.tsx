import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { Input, Select } from '@/components/ui/input';
import { StateSelector } from '@/features/auth/components/state-selector';
import { useDocumentTypes } from './hooks/use-document-types';
import type { CreateTemplateResult } from './hooks/use-templates';
import type { CreateTemplateRequest } from './types';

interface CreateTemplateModalProps {
  open: boolean;
  onClose: () => void;
  onCreate: (data: CreateTemplateRequest) => Promise<CreateTemplateResult>;
}

export function CreateTemplateModal({ open, onClose, onCreate }: CreateTemplateModalProps) {
  const {
    documentTypes,
    isLoading: typesLoading,
    error: typesError,
    reload: reloadTypes,
  } = useDocumentTypes();
  const activeTypes = documentTypes.filter((t) => t.isActive);

  const [isDefault, setIsDefault] = useState(false);
  const [stateCode, setStateCode] = useState('');
  const [documentTypeId, setDocumentTypeId] = useState('');
  const [name, setName] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const reset = () => {
    setIsDefault(false);
    setStateCode('');
    setDocumentTypeId('');
    setName('');
    setFormError(null);
  };

  const handleClose = () => {
    reset();
    onClose();
  };

  const handleSubmit = async () => {
    const trimmedName = name.trim();
    if (!trimmedName) {
      setFormError('Please enter a template name.');
      return;
    }
    if (!documentTypeId) {
      setFormError('Please select a document type.');
      return;
    }
    if (!isDefault && !stateCode) {
      setFormError('Please select a state, or mark this template as the default.');
      return;
    }

    setIsSubmitting(true);
    setFormError(null);
    const result = await onCreate({
      name: trimmedName,
      documentTypeId: Number(documentTypeId),
      stateCode: isDefault ? undefined : stateCode,
    });
    setIsSubmitting(false);

    if (result.success) {
      handleClose();
    } else {
      setFormError(result.message ?? 'Failed to create template.');
    }
  };

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title="Create Template"
      data-testid="create-template-modal"
      footer={
        <>
          <Button variant="secondary" onClick={handleClose} data-testid="create-template-cancel">
            Cancel
          </Button>
          <Button
            onClick={handleSubmit}
            loading={isSubmitting}
            disabled={typesLoading || activeTypes.length === 0}
            data-testid="create-template-submit"
          >
            Create Template
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {formError && <Notice variant="error" title={formError} />}
        {typesError && (
          <Notice variant="error" title={typesError}>
            <Button variant="secondary" size="sm" onClick={reloadTypes} className="mt-3">
              Retry
            </Button>
          </Notice>
        )}

        <Input
          label="Template name"
          placeholder="e.g. Ohio IEP"
          value={name}
          onChange={(e) => setName(e.target.value)}
          data-testid="create-template-name"
        />

        <Select
          label="Document type"
          value={documentTypeId}
          onChange={(e) => setDocumentTypeId(e.target.value)}
          disabled={typesLoading}
          data-testid="create-template-doc-type"
        >
          <option value="">
            {typesLoading ? 'Loading…' : 'Select a document type…'}
          </option>
          {activeTypes.map((t) => (
            <option key={t.id} value={t.id}>
              {t.displayName}
            </option>
          ))}
        </Select>

        <div className="flex items-center gap-2">
          <input
            id="template-is-default"
            type="checkbox"
            checked={isDefault}
            onChange={(e) => setIsDefault(e.target.checked)}
            className="h-4 w-4 rounded border-brand-slate-300 text-brand-teal-500 focus:ring-brand-teal-400"
            data-testid="create-template-is-default"
          />
          <label
            htmlFor="template-is-default"
            className="text-[13px] font-medium text-brand-slate-600"
          >
            Default template (applies to all states)
          </label>
        </div>

        {!isDefault && (
          <div>
            <label
              htmlFor="template-state"
              className="mb-1 block text-[13px] font-medium text-brand-slate-600"
            >
              State
            </label>
            <StateSelector
              id="template-state"
              data-testid="create-template-state"
              value={stateCode}
              onChange={setStateCode}
            />
          </div>
        )}
      </div>
    </Modal>
  );
}
