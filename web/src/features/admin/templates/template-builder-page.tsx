import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { PageLayout } from '@/components/ui/page-layout';
import { useToast } from '@/components/ui/toast';
import { usePageTitle } from '@/hooks/use-page-title';
import { useTemplateBuilder } from './hooks/use-template-builder';
import { SectionEditor } from './components/section-editor';
import { FormPreview } from './components/form-preview';

export function TemplateBuilderPage() {
  const { t } = useTranslation(['admin', 'common']);
  const { templateId } = useParams<{ templateId: string }>();
  const id = Number(templateId);
  const { show: showToast } = useToast();
  const builder = useTemplateBuilder(id);
  const { template, version, isLoading, loadError, conflict, readOnly } = builder;
  usePageTitle(template ? template.name : t('templates.builder.pageTitleFallback'));

  const [addingSection, setAddingSection] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [forking, setForking] = useState(false);
  const [publishErrors, setPublishErrors] = useState<string[] | null>(null);

  const sections = version?.sections ?? [];
  const canPublish = sections.length > 0 && sections.every((s) => s.fields.length > 0);

  const handleAddSection = async () => {
    setAddingSection(true);
    await builder.addSection(t('templates.sectionEditor.newSectionDefaultTitle'));
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
      showToast({ message: t('templates.builder.publishedToast'), variant: 'success' });
    } else if (!result.conflict) {
      setPublishErrors(
        result.errors && result.errors.length ? result.errors : [result.message ?? t('templates.builder.publishFailedFallback')]
      );
    }
  };

  const handleFork = async () => {
    setForking(true);
    setPublishErrors(null);
    const result = await builder.fork();
    setForking(false);
    if (result.ok) {
      showToast({ message: t('templates.builder.newDraftToast'), variant: 'success' });
    } else {
      showToast({ message: result.message ?? t('templates.builder.forkFailedFallback'), variant: 'error' });
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label={t('templates.builder.loading')} />
      </div>
    );
  }

  if (loadError || !version || !template) {
    return (
      <div>
        <Notice variant="error" title={loadError ?? t('templates.builder.loadErrorFallback')}>
          <Button variant="secondary" size="sm" onClick={builder.reload} className="mt-3">
            {t('common:ui.tryAgain')}
          </Button>
        </Notice>
        <Link
          to="/admin/templates"
          className="mt-4 inline-flex items-center gap-1.5 text-sm text-brand-teal-500 hover:text-brand-teal-600"
        >
          <ArrowLeft size={14} strokeWidth={1.8} aria-hidden="true" />
          {t('templates.builder.backToTemplates')}
        </Link>
      </div>
    );
  }

  const subtitle = t('templates.builder.subtitle', {
    state: template.stateCode ?? t('templates.list.column.stateDefault'),
    docType: template.documentTypeDisplayName,
    version: version.versionNumber,
  });

  return (
    <PageLayout
      title={template.name}
      subtitle={subtitle}
      breadcrumb={[{ label: t('templates.list.pageTitle'), to: '/admin/templates' }, { label: template.name }]}
      actions={
        <div className="flex items-center gap-2">
          <Badge variant={version.status === 'Published' ? 'success' : 'neutral'}>
            {t(`templates.versionStatus.${version.status}`)}
          </Badge>
          {readOnly ? (
            <Button onClick={handleFork} loading={forking} data-testid="template-fork-button">
              {t('templates.builder.editNewVersion')}
            </Button>
          ) : (
            <Button
              onClick={handlePublish}
              loading={publishing}
              disabled={!canPublish}
              data-testid="template-publish-button"
            >
              {t('templates.builder.publish')}
            </Button>
          )}
        </div>
      }
    >
      {conflict && (
        <Notice
          variant="warning"
          title={t('templates.builder.conflictTitle')}
          data-testid="template-conflict-notice"
        >
          <p>{t('templates.builder.conflictMessage')}</p>
          <Button variant="secondary" size="sm" onClick={builder.reload} className="mt-3">
            {t('templates.builder.reloadTemplate')}
          </Button>
        </Notice>
      )}

      {readOnly && (
        <Notice variant="info" title={t('templates.builder.readOnlyTitle')} data-testid="template-readonly-notice">
          {t('templates.builder.readOnlyMessage')}
        </Notice>
      )}

      {publishErrors && (
        <Notice variant="error" title={t('templates.builder.publishErrorsTitle')} data-testid="template-publish-errors">
          <ul className="ml-4 list-disc space-y-1">
            {publishErrors.map((e, i) => (
              <li key={i}>{e}</li>
            ))}
          </ul>
        </Notice>
      )}

      {!readOnly && !canPublish && (
        <p className="text-sm text-brand-slate-500">{t('templates.builder.needsSectionAndField')}</p>
      )}

      <div className="grid grid-cols-1 gap-8 xl:grid-cols-2">
        {/* Builder column */}
        <div className="space-y-4">
          <h2 className="text-sm font-medium text-brand-slate-800">{t('templates.builder.structure')}</h2>
          {sections.length === 0 ? (
            <Card>
              <p className="text-sm text-brand-slate-500">{t('templates.builder.noSections')}</p>
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
              {t('templates.builder.addSection')}
            </Button>
          )}
        </div>

        {/* Preview column */}
        <div className="space-y-4">
          <h2 className="text-sm font-medium text-brand-slate-800">{t('templates.builder.formPreviewHeading')}</h2>
          <FormPreview sections={sections} />
        </div>
      </div>
    </PageLayout>
  );
}
