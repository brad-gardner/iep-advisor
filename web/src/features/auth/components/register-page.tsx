import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { ParentRegisterForm } from './parent-register-form';
import { DistrictRegisterForm } from './district-register-form';
import { RegisterPathCard } from './register-path-card';

type RegisterPath = 'parent' | 'district';

export function RegisterPage() {
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
        Create Your Account
      </h2>

      <fieldset className="mb-6" data-testid="register-path-chooser">
        <legend className="sr-only">Who are you signing up as?</legend>
        <div role="radiogroup" aria-label="Account type" className="grid grid-cols-1 gap-3">
          <RegisterPathCard
            title="I'm a parent"
            description="Understand and advocate around your child's IEP. Requires a beta invite code."
            selected={path === 'parent'}
            onSelect={() => setPath('parent')}
            data-testid="register-path-parent"
          />
          <RegisterPathCard
            title="I represent a school or district"
            description="Set up your district to manage IEPs with your team."
            selected={path === 'district'}
            onSelect={() => setPath('district')}
            data-testid="register-path-district"
          />
        </div>
      </fieldset>

      {path === 'parent' && <ParentRegisterForm initialInviteCode={codeFromUrl} />}
      {path === 'district' && <DistrictRegisterForm />}

      <p className="mt-6 text-center text-sm text-brand-slate-400">
        Already have an account?{' '}
        <Link to="/login" className="text-brand-teal-500 hover:text-brand-teal-600">
          Sign in
        </Link>
      </p>
    </div>
  );
}
