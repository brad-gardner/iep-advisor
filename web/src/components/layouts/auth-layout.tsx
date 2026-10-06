import { Logo } from '@/components/ui/logo';
import { LanguageSwitcher } from '@/lib/i18n/language-switcher';
import { useLanguageQueryParam } from '@/lib/i18n/use-language-query-param';

interface AuthLayoutProps {
  children: React.ReactNode;
}

export function AuthLayout({ children }: AuthLayoutProps) {
  // Lets an emailed/shared link carry `?lang=en|es` and land in that
  // language once, before the visitor has an account or has chosen one.
  useLanguageQueryParam();

  return (
    <div className="min-h-screen flex items-center justify-center bg-brand-slate-800 px-4">
      <div className="w-full max-w-md">
        <div className="flex justify-center mb-8">
          <Logo variant="dark" size="lg" data-testid="auth-logo" />
        </div>
        <div className="bg-white rounded-card p-8">
          {children}
        </div>
        <div className="mt-6 flex justify-center">
          <LanguageSwitcher tone="onDark" data-testid="auth-language-switcher" />
        </div>
      </div>
    </div>
  );
}
