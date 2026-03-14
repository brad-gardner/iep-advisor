import { Logo } from '@/components/ui/logo';

interface AuthLayoutProps {
  children: React.ReactNode;
}

export function AuthLayout({ children }: AuthLayoutProps) {
  return (
    <div className="min-h-screen flex items-center justify-center bg-brand-slate-800 px-4">
      <div className="w-full max-w-md">
        <div className="flex justify-center mb-8">
          <Logo variant="dark" size="lg" />
        </div>
        <div className="bg-white rounded-card p-8">
          {children}
        </div>
      </div>
    </div>
  );
}
