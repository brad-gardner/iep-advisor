import { Link, useLocation } from 'react-router-dom';
import { LayoutDashboard, Users, UserCircle, BookOpen, GraduationCap, LogOut, Menu, X, Shield, LifeBuoy } from 'lucide-react';
import { useState } from 'react';
import { Logo } from '@/components/ui/logo';
import { useAuth } from '@/features/auth/hooks/use-auth';

const navItems = [
  { to: '/dashboard', label: 'Dashboard', Icon: LayoutDashboard },
  { to: '/children', label: 'My Children', Icon: Users },
  { to: '/profile', label: 'Profile', Icon: UserCircle },
  // { to: '/subscription', label: 'Subscription', Icon: CreditCard }, // Hidden during beta
  { to: '/knowledge-base', label: 'Knowledge Base', Icon: BookOpen },
  { to: '/iep-101', label: 'IEP 101', Icon: GraduationCap },
];

interface SidebarProps {
  onLogout: () => void;
}

export function Sidebar({ onLogout }: SidebarProps) {
  const location = useLocation();
  const { user } = useAuth();
  const [mobileOpen, setMobileOpen] = useState(false);

  const isActive = (path: string) =>
    location.pathname === path || location.pathname.startsWith(path + '/');

  const navContent = (
    <>
      <div className="p-6">
        <Logo variant="dark" size="md" data-testid="sidebar-logo" />
      </div>

      <nav className="flex-1 px-3 space-y-1">
        {navItems.map(({ to, label, Icon }) => {
          const active = isActive(to);
          const testId = `nav-${to.slice(1)}`;
          return (
            <Link
              key={to}
              to={to}
              data-testid={testId}
              onClick={() => setMobileOpen(false)}
              className={`flex items-center gap-3 px-3 py-2.5 rounded-button text-sm transition-colors ${
                active
                  ? 'text-brand-teal-400 bg-brand-slate-700 border-l-2 border-brand-teal-500 -ml-px'
                  : 'text-brand-slate-400 hover:text-brand-slate-200 hover:bg-brand-slate-700'
              }`}
            >
              <Icon size={18} strokeWidth={1.8} />
              {label}
            </Link>
          );
        })}
        <a
          href="https://docs.google.com/forms/d/e/1FAIpQLSfhj_T_TiRlPp9MYgmxHPqtgKfyNkTk0CXoXQn7HWXILaDmfQ/viewform?usp=publish-editor"
          target="_blank"
          rel="noopener noreferrer"
          data-testid="nav-support"
          className="flex items-center gap-3 px-3 py-2.5 rounded-button text-sm text-brand-slate-400 hover:text-brand-slate-200 hover:bg-brand-slate-700 transition-colors"
        >
          <LifeBuoy size={18} strokeWidth={1.8} />
          Support
        </a>
      </nav>

      {user?.role === 'Admin' && (
        <div className="px-3 mt-2">
          <div className="border-t border-brand-slate-700 pt-3 mb-2">
            <span className="px-3 text-[10px] uppercase tracking-wider font-semibold text-brand-teal-400">
              Admin
            </span>
          </div>
          <Link
            to="/admin"
            data-testid="nav-admin-dashboard"
            onClick={() => setMobileOpen(false)}
            className={`flex items-center gap-3 px-3 py-2.5 rounded-button text-sm transition-colors ${
              location.pathname === '/admin'
                ? 'text-brand-teal-400 bg-brand-slate-700 border-l-2 border-brand-teal-500 -ml-px'
                : 'text-brand-slate-400 hover:text-brand-slate-200 hover:bg-brand-slate-700'
            }`}
          >
            <LayoutDashboard size={18} strokeWidth={1.8} />
            Dashboard
          </Link>
          <Link
            to="/admin/users"
            data-testid="nav-admin-users"
            onClick={() => setMobileOpen(false)}
            className={`flex items-center gap-3 px-3 py-2.5 rounded-button text-sm transition-colors ${
              isActive('/admin/users')
                ? 'text-brand-teal-400 bg-brand-slate-700 border-l-2 border-brand-teal-500 -ml-px'
                : 'text-brand-slate-400 hover:text-brand-slate-200 hover:bg-brand-slate-700'
            }`}
          >
            <Shield size={18} strokeWidth={1.8} />
            Users
          </Link>
        </div>
      )}

      <div className="p-4 border-t border-brand-slate-700">
        <p className="text-sm text-brand-slate-300 truncate mb-2">
          {user?.firstName} {user?.lastName}
        </p>
        <button
          onClick={onLogout}
          data-testid="sidebar-sign-out"
          className="flex items-center gap-2 text-sm text-brand-slate-400 hover:text-brand-slate-200 transition-colors"
        >
          <LogOut size={16} strokeWidth={1.8} />
          Sign Out
        </button>
      </div>
    </>
  );

  return (
    <>
      {/* Mobile hamburger */}
      <button
        onClick={() => setMobileOpen(true)}
        className="md:hidden fixed top-4 left-4 z-50 p-2 rounded-button bg-brand-slate-800 text-white"
        data-testid="mobile-menu-open"
        aria-label="Open navigation"
      >
        <Menu size={20} strokeWidth={1.8} />
      </button>

      {/* Mobile overlay */}
      {mobileOpen && (
        <div
          className="md:hidden fixed inset-0 z-40 bg-black/50"
          onClick={() => setMobileOpen(false)}
        />
      )}

      {/* Mobile sidebar */}
      <aside
        className={`md:hidden fixed inset-y-0 left-0 z-50 w-64 bg-brand-slate-800 flex flex-col transform transition-transform ${
          mobileOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        <button
          onClick={() => setMobileOpen(false)}
          className="absolute top-4 right-4 text-brand-slate-400 hover:text-white"
          data-testid="mobile-menu-close"
          aria-label="Close navigation"
        >
          <X size={20} strokeWidth={1.8} />
        </button>
        {navContent}
      </aside>

      {/* Desktop sidebar */}
      <aside className="hidden md:flex md:w-64 md:flex-col md:fixed md:inset-y-0 bg-brand-slate-800">
        {navContent}
      </aside>
    </>
  );
}
