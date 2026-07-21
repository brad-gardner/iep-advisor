import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { AutosaveIndicator } from '@/features/admin/templates/components/autosave-indicator';
import { useFlushRegistry } from '@/features/iep-authoring/hooks/use-flush-registry';
import type { DocumentInstance } from '../hooks/use-document-instance';
import { DocumentFlushContext } from '../hooks/flush-registry-context';
import type { DocumentInstanceDetailDto, DocumentInstanceStatus } from '../types';
import { DocumentField } from './field-renderers/document-field';
import { FinalizeDocumentSection } from './finalize-document-section';

const statusVariant: Record<DocumentInstanceStatus, 'neutral' | 'warning' | 'success'> = {
  Draft: 'neutral',
  Finalizing: 'warning',
  Finalized: 'success',
};

interface DocumentEditorProps {
  detail: DocumentInstanceDetailDto;
  instance: Pick<
    DocumentInstance,
    'saveStatus' | 'conflict' | 'reloadKey' | 'readOnly' | 'saveValues' | 'reload' | 'getSaveState'
  >;
}

/**
 * Renders the pinned template version as an editable form: sections in order,
 * fields in order, each via its per-`FieldType` renderer. A 409 latches a
 * reload banner; reloading remounts every input (keyed by `reloadKey`) so stale
 * local values can't overwrite fresher server state.
 */
export function DocumentEditor({ detail, instance }: DocumentEditorProps) {
  const { saveStatus, conflict, reloadKey, readOnly, saveValues, reload, getSaveState } = instance;
  const sections = [...detail.templateVersion.sections].sort((a, b) => a.displayOrder - b.displayOrder);

  // One registry per instance: each field registers its autosave flush so a
  // finalize can drain every pending edit before snapshotting.
  const flushRegistry = useFlushRegistry();

  return (
    <DocumentFlushContext.Provider value={flushRegistry}>
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <h1 className="font-serif text-2xl text-brand-slate-800">{detail.documentTypeDisplayName}</h1>
          <Badge variant={statusVariant[detail.status]}>{detail.status}</Badge>
        </div>
        <AutosaveIndicator status={saveStatus} />
      </div>

      {conflict && (
        <div role="alert">
          <Notice variant="warning" title="This document changed elsewhere">
            <div className="space-y-2">
              <p>
                Your last edit could not be saved because a newer version exists. Reload to get the
                latest values before continuing — unsaved local changes will be discarded.
              </p>
              <Button variant="secondary" size="sm" onClick={reload} data-testid="document-reload">
                Reload document
              </Button>
            </div>
          </Notice>
        </div>
      )}

      {readOnly && !conflict && (
        <Notice variant="info" title="This document is read-only">
          It is currently {detail.status.toLowerCase()} and cannot be edited.
        </Notice>
      )}

      {sections.length === 0 ? (
        <Notice variant="info" title="This template has no sections">
          There is nothing to fill in yet.
        </Notice>
      ) : (
        sections.map((section) => {
          const fields = [...section.fields].sort((a, b) => a.displayOrder - b.displayOrder);
          return (
            <Card key={section.id} data-testid={`section-${section.id}`}>
              <h2 className="mb-4 font-serif text-lg text-brand-slate-800">
                {section.title || 'Untitled section'}
              </h2>
              {fields.length === 0 ? (
                <p className="text-sm text-brand-slate-400">No fields.</p>
              ) : (
                <div className="space-y-4">
                  {fields.map((field) => (
                    // Keyed by reloadKey so a post-conflict reload remounts the
                    // input with server truth (stale local state can't persist).
                    <DocumentField
                      key={`${field.id}:${reloadKey}`}
                      field={field}
                      value={detail.values[field.fieldKey]}
                      disabled={readOnly || conflict}
                      onSave={saveValues}
                    />
                  ))}
                </div>
              )}
            </Card>
          );
        })
      )}

      <Card>
        <h2 className="mb-2 font-serif text-lg text-brand-slate-800">Finalize</h2>
        <p className="mb-4 text-sm text-brand-slate-600">
          Snapshot this draft into an immutable version and generate its PDF.
        </p>
        <FinalizeDocumentSection
          instanceId={detail.id}
          studentId={detail.schoolStudentId}
          documentTypeId={detail.documentTypeId}
          documentTypeDisplayName={detail.documentTypeDisplayName}
          status={detail.status}
          flushBeforeFinalize={flushRegistry.flushAll}
          getSaveState={getSaveState}
        />
      </Card>
    </div>
    </DocumentFlushContext.Provider>
  );
}
