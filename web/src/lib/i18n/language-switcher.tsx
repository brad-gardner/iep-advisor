import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Select } from '@/components/ui/input';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { cn } from '@/lib/cn';
import { SUPPORTED_LANGUAGES, isSupportedLanguage } from './detect';

interface LanguageSwitcherProps {
  className?: string;
  'data-testid'?: string;
  /**
   * The auth layout and sidebar footers sit on a dark surface; Profile sits
   * on the page's light background. `Select`'s built-in label isn't
   * theme-aware, so this component renders its own and needs to know which
   * palette to use. Defaults to the light-surface palette.
   */
  tone?: 'onDark' | 'onLight';
}

const labelToneClass: Record<'onDark' | 'onLight', string> = {
  onDark: 'text-brand-slate-400',
  onLight: 'text-brand-slate-600',
};

/**
 * The one language switcher — a compact `Select` visually labeled
 * "Idioma / Language", whose options name themselves ("English", "Español"),
 * per the design direction (no flags, no truncation). Placed in the auth
 * layout footer and the sidebar footer, and reused as the Profile page's
 * Language field.
 *
 * Switching updates `<html lang>` immediately (via i18next's
 * `languageChanged` event) and persists the choice: to the account when
 * signed in, or to `localStorage` as the pre-login choice otherwise — see
 * `AuthProvider.setLanguage`.
 */
export function LanguageSwitcher({ className, 'data-testid': testId, tone = 'onLight' }: LanguageSwitcherProps) {
  const { t, i18n } = useTranslation('common');
  const { setLanguage } = useAuth();
  const [error, setError] = useState(false);
  // Rendered twice at once on wide viewports (the sidebar's mobile drawer +
  // desktop rail both mount this), so a fixed id would collide — `useId`
  // keeps the label-control pairing valid in both instances.
  const selectId = useId();

  const handleChange = async (e: React.ChangeEvent<HTMLSelectElement>) => {
    const next = e.target.value;
    if (!isSupportedLanguage(next)) return;
    setError(false);
    const result = await setLanguage(next);
    if (!result.success) setError(true);
  };

  return (
    <div className={className}>
      <label htmlFor={selectId} className={cn('mb-1 block text-xs font-medium', labelToneClass[tone])}>
        {t('languageSwitcher.label')}
      </label>
      <Select
        id={selectId}
        value={i18n.language}
        onChange={handleChange}
        className="w-auto"
        data-testid={testId}
      >
        {SUPPORTED_LANGUAGES.map((lng) => (
          <option key={lng} value={lng}>
            {t(`languageSwitcher.${lng}`)}
          </option>
        ))}
      </Select>
      {error && (
        <p role="alert" className="mt-1 text-xs text-brand-danger-600">
          {t('languageSwitcher.updateFailed')}
        </p>
      )}
    </div>
  );
}
