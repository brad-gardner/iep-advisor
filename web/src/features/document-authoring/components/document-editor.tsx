import { useEffect, useMemo, useState } from 'react';
import { MessageSquare } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Drawer } from '@/components/ui/drawer';
import { Notice } from '@/components/ui/notice';
import { AutosaveIndicator } from '@/features/admin/templates/components/autosave-indicator';
import { useFlushRegistry } from '@/hooks/use-flush-registry';
import { useFlushOnNavigate } from '@/hooks/use-flush-on-navigate';
import type { DocumentInstance } from '../hooks/use-document-instance';
import { DocumentFlushContext } from '../hooks/flush-registry-context';
import { DocumentEditorContext } from '../hooks/document-editor-context';
import { computeCompleteness } from '../lib/completeness';
import { sectionDomId } from '../lib/section-dom';
import type { DocumentInstanceDetailDto, DocumentInstanceStatus } from '../types';
import { DocumentField } from './field-renderers/document-field';
import { FinalizeDocumentSection } from './finalize-document-section';
import { ChatPanel } from './chat/chat-panel';
import { SectionNavigator } from './section-navigator';
import { CompletenessPanel } from './completeness-panel';

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
 * Renders the pinned template version as an editable form: a sticky section
 * navigator, sections in order, fields in order via their per-`FieldType`
 * renderer (each with inline AI help where it applies), an advisory
 * completeness panel, and the document-scoped assistant in a drawer. A 409
 * latches a reload banner; reloading remounts every input (keyed by
 * `reloadKey`) so stale local values can't overwrite fresher server state.
 */
export function DocumentEditor({ detail, instance }: DocumentEditorProps) {
  const { saveStatus, conflict, reloadKey, readOnly, saveValues, reload, getSaveState } = instance;
  const sections = useMemo(
    () => [...detail.templateVersion.sections].sort((a, b) => a.displayOrder - b.displayOrder),
    [detail.templateVersion.sections]
  );
  const [chatOpen, setChatOpen] = useState(false);

  // One registry per instance: each field registers its autosave flush so a
  // finalize can drain every pending edit before snapshotting.
  const flushRegistry = useFlushRegistry();
  // Best-effort flush of pending edits on a hard unload (tab close / refresh).
  // In-app navigation is covered by each field's unmount flush (useRegisterFlush).
  useFlushOnNavigate(flushRegistry);

  // Completeness is derived from the last server-normalized values (updated on
  // every successful save), so it tracks what is actually persisted.
  const completeness = useMemo(
    () => computeCompleteness(detail.templateVersion, detail.values),
    [detail.templateVersion, detail.values]
  );

  // Keyboard: [ and ] jump between sections (Steph is keyboard-first).
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.target instanceof HTMLElement && ['INPUT', 'TEXTAREA', 'SELECT'].includes(e.target.tagName)) return;
      if (e.key !== '[' && e.key !== ']') return;
      const ids = sections.map((s) => sectionDomId(s.id));
      const current = ids.findIndex((id) => {
        const el = document.getElementById(id);
        return el != null && el.getBoundingClientRect().top >= -8;
      });
      const next = e.key === ']' ? Math.min(ids.length - 1, Math.max(0, current) + 1) : Math.max(0, current - 1);
      document.getElementById(ids[next])?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [sections]);

  const editorContext = useMemo(
    () => ({ instanceId: detail.id, studentId: detail.schoolStudentId }),
    [detail.id, detail.schoolStudentId]
  );

  return (
    <DocumentEditorContext.Provider value={editorContext}>
      <DocumentFlushContext.Provider value={flushRegistry}>
        <div className="space-y-6">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-3">
              <h1 className="font-serif text-2xl text-brand-slate-800">{detail.documentTypeDisplayName}</h1>
              <Badge variant={statusVariant[detail.status]}>{detail.status}</Badge>
            </div>
            <div className="flex items-center gap-3">
              <AutosaveIndicator status={saveStatus} />
              <Button
                variant="secondary"
                size="sm"
                onClick={() => setChatOpen(true)}
                data-testid="document-chat-open"
              >
                <MessageSquare className="mr-1 h-4 w-4" aria-hidden="true" />
                Ask the assistant
              </Button>
            </div>
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

          <div className="grid gap-6 lg:grid-cols-[14rem_minmax(0,1fr)_16rem]">
            <SectionNavigator sections={sections} />

            <div className="min-w-0 space-y-6">
              {sections.length === 0 ? (
                <Notice variant="info" title="This template has no sections">
                  There is nothing to fill in yet.
                </Notice>
              ) : (
                sections.map((section) => {
                  const fields = [...section.fields].sort((a, b) => a.displayOrder - b.displayOrder);
                  return (
                    <Card
                      key={section.id}
                      id={sectionDomId(section.id)}
                      className="scroll-mt-4"
                      data-testid={`section-${section.id}`}
                    >
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

            <CompletenessPanel summary={completeness} />
          </div>
        </div>

        <Drawer open={chatOpen} onClose={() => setChatOpen(false)} title="Assistant">
          <div className="h-[70vh]">
            <ChatPanel instanceId={detail.id} onClose={() => setChatOpen(false)} />
          </div>
        </Drawer>
      </DocumentFlushContext.Provider>
    </DocumentEditorContext.Provider>
  );
}
