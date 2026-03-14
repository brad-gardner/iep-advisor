import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { Sidebar } from './sidebar';

interface MainLayoutProps {
  children: React.ReactNode;
}

export function MainLayout({ children }: MainLayoutProps) {
  const { logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="min-h-screen bg-brand-slate-50">
      <Sidebar onLogout={handleLogout} />

      <main className="md:ml-64">
        <div className="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-8 pt-16 md:pt-8">
          {children}
        </div>
      </main>
    </div>
  );
}
