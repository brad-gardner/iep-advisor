import { useTranslation } from 'react-i18next';
import { Notice } from '@/components/ui/notice';
import { isSupportedLanguage } from './detect';

interface GeneratedLanguageNoticeProps {
  /** Language the AI artifact was generated in (`generatedLanguage` on the DTO); null/unknown = English. */
  generatedLanguage: string | null | undefined;
  className?: string;
}

/**
 * Shown above an AI-generated artifact (analysis, meeting prep, summaries, …)
 * when it was generated in a different language than the viewer's — the
 * artifact is not regenerated silently. Renders nothing when they match.
 */
export function GeneratedLanguageNotice({ generatedLanguage, className }: GeneratedLanguageNoticeProps) {
  const { t, i18n } = useTranslation('common');
  const generated = isSupportedLanguage(generatedLanguage) ? generatedLanguage : 'en';
  const viewing = isSupportedLanguage(i18n.resolvedLanguage) ? i18n.resolvedLanguage : 'en';
  if (generated === viewing) return null;
  const language = t(`generatedLanguage.language.${generated}`);
  return (
    <Notice
      variant="info"
      className={className}
      title={t('generatedLanguage.title', { language })}
      data-testid="generated-language-notice"
    >
      {t('generatedLanguage.body', { language })}
    </Notice>
  );
}
