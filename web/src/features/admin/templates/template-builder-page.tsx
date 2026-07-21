import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { PageLayout } from '@/components/ui/page-layout';
import { useToast } from '@/components/ui/toast';
import { useTemplateBuilder } from './hooks/use-template-builder';
import { SectionEditor } from './components/section-editor';
import { FormPreview } from './components/form-preview';

export function TemplateBuilderPage() {
  const { templateId } = useParams<{ templateId: string }>();
  const id = Number(templateId);
  const { show: showToast } = useToast();
  const builder = useTemplateBuilder(id);
  const { template, version, isLoading, loadError, conflict, readOnly } = builder;

  const [addingSection, setAddingSection] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [forking, setForking] = useState(false);
  const [publishErrors, setPublishErrors] = useState<string[] | null>(null);

  const sections = version?.sections ?? [];
  const canPublish = sections.length > 0 && sections.every((s) => s.fields.length > 0);

  const handleAddSection = async () => {
    setAddingSection(true);
    await builder.addSection('New section');
    setAddingSection(false);
  };

  const moveSection = (index: number, direction: -1 | 1) => {
    const ids = sections.map((s) => s.id);
    const target = index + direction;
    if (target < 0 || target >= ids.length) return;
    [ids[index], ids[target]] = [ids[target], ids[index]];
    void builder.reorderSections(ids);
  };

  const handlePublish = async () => {
    setPublishing(true);
    setPublishErrors(null);
    const result = await builder.publish();
    setPublishing(false);
    if (result.ok) {
      showToast({ message: 'Template version published.', variant: 'success' });
    } else if (!result.conflict) {
      setPublishErrors(result.errors && result.errors.length ? result.errors : [result.message ?? 'Publish failed.']);
    }
  };

  const handleFork = async () => {
    setForking(true);
    setPublishErrors(null);
    const result = await builder.fork();
    setForking(false);
    if (result.ok) {
      showToast({ message: 'New draft version created.', variant: 'success' });
    } else {
      showToast({ message: result.message ?? 'Failed to start a new version.', variant: 'error' });
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label="Loading template…" />
      </div>
    );
  }

  if (loadError || !version || !template) {
    return (
      <div>
        <Notice variant="error" title={loadError ?? 'Failed to load template.'}>
          <Button variant="secondary" size="sm" onClick={builder.reload} className="mt-3">
            Retry
          </Button>
        </Notice>
        <Link
          to="/admin/templates"
          className="mt-4 inline-flex items-center gap-1.5 text-sm text-brand-teal-500 hover:text-brand-teal-600"
        >
          <ArrowLeft size={14} strokeWidth={1.8} aria-hidden="true" />
          Back to templates
        </Link>
      </div>
    );
  }

  const subtitle = `${template.stateCode ?? 'Default'} · ${template.documentTypeDisplayName} · v${version.versionNumber}`;

  return (
    <PageLayout
      title={template.name}
      subtitle={subtitle}
      breadcrumb={[{ label: 'Templates', to: '/admin/templates' }, { label: template.name }]}
      actions={
        <div className="flex items-center gap-2">
          <Badge variant={version.status === 'Published' ? 'success' : 'neutral'}>{version.status}</Badge>
          {readOnly ? (
            <Button onClick={handleFork} loading={forking} data-testid="template-fork-button">
              Edit (new version)
            </Button>
          ) : (
            <Button
              onClick={handlePublish}
              loading={publishing}
              disabled={!canPublish}
              data-testid="template-publish-button"
            >
              Publish
            </Button>
          )}
        </div>
      }
    >
      {conflict && (
        <Notice
          variant="warning"
          title="This template changed in another session."
          data-testid="template-conflict-notice"
        >
          <p>Your working copy is out of date. Reload to get the latest before editing further.</p>
          <Button variant="secondary" size="sm" onClick={builder.reload} className="mt-3">
            Reload template
          </Button>
        </Notice>
      )}

      {readOnly && (
        <Notice variant="info" title="This version is published and read-only." data-testid="template-readonly-notice">
          Published versions are immutable. Choose “Edit (new version)” to start a new draft from this version.
        </Notice>
      )}

      {publishErrors && (
        <Notice variant="error" title="This template can’t be published yet." data-testid="template-publish-errors">
          <ul className="ml-4 list-disc space-y-1">
            {publishErrors.map((e, i) => (
              <li key={i}>{e}</li>
            ))}
          </ul>
        </Notice>
      )}

      {!readOnly && !canPublish && (
        <p className="text-sm text-brand-slate-400">
          Add at least one section, and at least one field to every section, to publish.
        </p>
      )}

      <div className="grid grid-cols-1 gap-8 xl:grid-cols-2">
        {/* Builder column */}
        <div className="space-y-4">
          <h2 className="text-sm font-medium text-brand-slate-800">Structure</h2>
          {sections.length === 0 ? (
            <Card>
              <p className="text-sm text-brand-slate-400">
                No sections yet. Add a section to start building this template.
              </p>
            </Card>
          ) : (
            sections.map((section, i) => (
              <SectionEditor
                key={`${builder.reloadKey}:${section.id}`}
                section={section}
                builder={builder}
                readOnly={readOnly}
                canMoveUp={i > 0}
                canMoveDown={i < sections.length - 1}
                onMoveUp={() => moveSection(i, -1)}
                onMoveDown={() => moveSection(i, 1)}
              />
            ))
          )}

          {!readOnly && (
            <Button
              variant="secondary"
              onClick={handleAddSection}
              loading={addingSection}
              data-testid="template-add-section"
            >
              <Plus size={14} strokeWidth={1.8} className="mr-1.5" aria-hidden="true" />
              Add section
            </Button>
          )}
        </div>

        {/* Preview column */}
        <div className="space-y-4">
          <h2 className="text-sm font-medium text-brand-slate-800">Form preview</h2>
          <FormPreview sections={sections} />
        </div>
      </div>
    </PageLayout>
  );
}
