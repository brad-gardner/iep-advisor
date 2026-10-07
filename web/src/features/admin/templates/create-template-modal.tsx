import { useState } from 'react';
import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation(['admin', 'common']);
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
      setFormError(t('templates.createModal.errorNameRequired'));
      return;
    }
    if (!documentTypeId) {
      setFormError(t('templates.createModal.errorDocTypeRequired'));
      return;
    }
    if (!isDefault && !stateCode) {
      setFormError(t('templates.createModal.errorStateRequired'));
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
      setFormError(result.message ?? t('templates.createModal.errorCreateFailed'));
    }
  };

  return (
    <Modal
      open={open}
      onClose={handleClose} preventClose={isSubmitting}
      title={t('templates.createModal.title')}
      data-testid="create-template-modal"
      footer={
        <>
          <Button variant="secondary" onClick={handleClose} data-testid="create-template-cancel">
            {t('templates.createModal.cancel')}
          </Button>
          <Button
            onClick={handleSubmit}
            loading={isSubmitting}
            disabled={typesLoading || activeTypes.length === 0}
            data-testid="create-template-submit"
          >
            {t('templates.createModal.submit')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {formError && <Notice variant="error" title={formError} />}
        {typesError && (
          <Notice variant="error" title={typesError}>
            <Button variant="secondary" size="sm" onClick={reloadTypes} className="mt-3">
              {t('common:ui.tryAgain')}
            </Button>
          </Notice>
        )}

        <Input
          label={t('templates.createModal.nameLabel')}
          placeholder={t('templates.createModal.namePlaceholder')}
          value={name}
          onChange={(e) => setName(e.target.value)}
          data-testid="create-template-name"
        />

        <Select
          label={t('templates.createModal.docTypeLabel')}
          value={documentTypeId}
          onChange={(e) => setDocumentTypeId(e.target.value)}
          disabled={typesLoading}
          data-testid="create-template-doc-type"
        >
          <option value="">
            {typesLoading ? t('templates.createModal.docTypeLoading') : t('templates.createModal.docTypePlaceholder')}
          </option>
          {activeTypes.map((docType) => (
            <option key={docType.id} value={docType.id}>
              {docType.displayName}
            </option>
          ))}
        </Select>

        <div className="flex items-center gap-2">
          <input
            id="template-is-default"
            type="checkbox"
            checked={isDefault}
            onChange={(e) => setIsDefault(e.target.checked)}
            className="h-4 w-4 rounded border-brand-slate-300 text-brand-teal-500 focus:ring-brand-teal-500"
            data-testid="create-template-is-default"
          />
          <label
            htmlFor="template-is-default"
            className="text-[13px] font-medium text-brand-slate-600"
          >
            {t('templates.createModal.isDefaultLabel')}
          </label>
        </div>

        {!isDefault && (
          <div>
            <label
              htmlFor="template-state"
              className="mb-1 block text-[13px] font-medium text-brand-slate-600"
            >
              {t('templates.createModal.stateLabel')}
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
