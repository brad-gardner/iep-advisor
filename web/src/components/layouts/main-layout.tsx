import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { NotificationsProvider } from '@/features/notifications/stores/notifications-context';
import { cn } from '@/lib/cn';
import { Sidebar } from './sidebar';

interface MainLayoutProps {
  children: React.ReactNode;
  /** Widens the content column beyond the default `max-w-7xl` — for a route
   *  whose content (e.g. a two-column editor with its own nav rail) needs more
   *  room than the standard page shell affords. Keep this a one-file, opt-in
   *  knob rather than adding more width tiers than a route actually needs. */
  wide?: boolean;
}

export function MainLayout({ children, wide }: MainLayoutProps) {
  const { logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    // One shared unread-notifications poll for everything MainLayout renders
    // — the sidebar bell (mounted twice: mobile + desktop) and any page
    // content (e.g. the full Notifications page) — rather than each bell
    // instance racing its own independent fetch. Scoped here (not the app
    // root) so it never polls on public/unauthenticated routes.
    <NotificationsProvider>
      <div className="min-h-screen bg-brand-slate-50">
        <Sidebar onLogout={handleLogout} />

        <main className="md:ml-64">
          <div className={cn('mx-auto px-4 sm:px-6 lg:px-8 py-8 pt-16 md:pt-8', wide ? 'max-w-[1400px]' : 'max-w-7xl')}>
            {children}
          </div>
        </main>
      </div>
    </NotificationsProvider>
  );
}
