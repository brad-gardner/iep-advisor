import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ParentRegisterForm } from './parent-register-form';
import { DistrictRegisterForm } from './district-register-form';
import { RegisterPathCard } from './register-path-card';
import { usePageTitle } from '@/hooks/use-page-title';

type RegisterPath = 'parent' | 'district';

export function RegisterPage() {
  const { t } = useTranslation('auth');
  usePageTitle(t('register.pageTitle'));
  const [searchParams] = useSearchParams();
  const codeFromUrl = searchParams.get('code') ?? '';
  const typeFromUrl = searchParams.get('type');

  // Preselect a path from the URL: a beta invite (`?code=`) implies the parent
  // path; `?type=district` lets marketing links jump straight to the district
  // form. Otherwise the user chooses explicitly.
  const initialPath: RegisterPath | null = codeFromUrl
    ? 'parent'
    : typeFromUrl === 'district'
      ? 'district'
      : typeFromUrl === 'parent'
        ? 'parent'
        : null;

  const [path, setPath] = useState<RegisterPath | null>(initialPath);

  return (
    <div className="w-full">
      <h2 className="text-2xl font-serif font-semibold text-center mb-6 text-brand-slate-800">
        {t('register.title')}
      </h2>

      <fieldset className="mb-6" data-testid="register-path-chooser">
        <legend className="sr-only">{t('register.chooserLegend')}</legend>
        <div role="radiogroup" aria-label={t('register.accountType')} className="grid grid-cols-1 gap-3">
          <RegisterPathCard
            title={t('register.parentPathTitle')}
            description={t('register.parentPathDescription')}
            selected={path === 'parent'}
            onSelect={() => setPath('parent')}
            data-testid="register-path-parent"
          />
          <RegisterPathCard
            title={t('register.districtPathTitle')}
            description={t('register.districtPathDescription')}
            selected={path === 'district'}
            onSelect={() => setPath('district')}
            data-testid="register-path-district"
          />
        </div>
      </fieldset>

      {path === 'parent' && <ParentRegisterForm initialInviteCode={codeFromUrl} />}
      {path === 'district' && <DistrictRegisterForm />}

      <p className="mt-6 text-center text-sm text-brand-slate-500">
        {t('register.haveAccount')}{' '}
        <Link to="/login" className="text-brand-teal-500 hover:text-brand-teal-600">
          {t('register.signIn')}
        </Link>
      </p>
    </div>
  );
}
