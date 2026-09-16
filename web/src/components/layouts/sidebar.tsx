import { Link, useLocation } from 'react-router-dom';
import { LayoutDashboard, Users, UserCircle, BookOpen, GraduationCap, LogOut, Menu, X, Shield, LifeBuoy, FileSearch, School, Home, ScrollText, FileText, Upload, Calendar, Bell, MailWarning, ClipboardCheck, Download, Mail, ShieldCheck } from 'lucide-react';
import { useState } from 'react';
import { Logo } from '@/components/ui/logo';
import { Skeleton } from '@/components/ui/skeleton';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { useEducatorProfile } from '@/features/educator/hooks/use-educator-profile';
import { ORG_ROLE } from '@/features/educator/types';
import { NotificationBell } from '@/features/notifications/components/notification-bell';

// Common items shown to every role, after any role-specific section above.
const commonNavItems = [
  { to: '/notifications', label: 'Notifications', Icon: Bell },
  { to: '/profile', label: 'Profile', Icon: UserCircle },
  // { to: '/subscription', label: 'Subscription', Icon: CreditCard }, // Hidden during beta
  { to: '/knowledge-base', label: 'Knowledge Base', Icon: BookOpen },
  { to: '/iep-101', label: 'IEP 101', Icon: GraduationCap },
];

const parentNavItems = [
  { to: '/dashboard', label: 'Dashboard', Icon: LayoutDashboard },
  { to: '/children', label: 'My Children', Icon: Users },
  { to: '/etrs', label: 'ETRs', Icon: FileSearch },
];

const educatorNavItems = [
  { to: '/educator', label: 'Home', Icon: Home },
  { to: '/educator/students', label: 'Students', Icon: School },
  { to: '/educator/calendar', label: 'Calendar', Icon: Calendar },
];

const studentNavItems = [
  { to: '/student', label: 'Home', Icon: Home },
];

// "Administration" group. Schools is DistrictAdmin-only; Staff is visible to
// both DistrictAdmin and SchoolAdmin. Each item declares whether SchoolAdmin
// may see it so the group can mix scopes.
const adminNavItems: {
  to: string;
  label: string;
  Icon: typeof School;
  schoolAdmin: boolean;
}[] = [
  { to: '/educator/admin/schools', label: 'Schools', Icon: School, schoolAdmin: false },
  { to: '/educator/admin/staff', label: 'Staff', Icon: Users, schoolAdmin: true },
  { to: '/educator/admin/compliance', label: 'Compliance', Icon: ClipboardCheck, schoolAdmin: true },
  { to: '/educator/admin/imports', label: 'Import', Icon: Upload, schoolAdmin: true },
  { to: '/educator/admin/activity', label: 'Activity log', Icon: ScrollText, schoolAdmin: true },
  // District-scoped export jobs span every school, so — like Schools — this is
  // DistrictAdmin-only, not offered to a SchoolAdmin.
  { to: '/educator/admin/exports', label: 'Exports', Icon: Download, schoolAdmin: false },
];

// Pilot-gates plan, phase 4, decision 8: linked in the sidebar footer.
// `VITE_MARKETING_URL` — when the marketing site is deployed somewhere other
// than the default — should hold the trust page's own full URL.
const TRUST_URL = import.meta.env.VITE_MARKETING_URL || 'https://iep-advisor.com/trust.html';

interface SidebarProps {
  onLogout: () => void;
}

export function Sidebar({ onLogout }: SidebarProps) {
  const location = useLocation();
  const { user } = useAuth();
  const [mobileOpen, setMobileOpen] = useState(false);

  const isActive = (path: string) =>
    location.pathname === path || location.pathname.startsWith(path + '/');

  // Pick the role-specific nav purely by the user's role: educator nav for
  // Educators, student nav for Students, otherwise the parent nav. Common items
  // appear for every role.
  const showEducatorNav = user?.role === 'Educator';
  const showStudentNav = user?.role === 'Student';
  const roleNavItems = showEducatorNav
    ? educatorNavItems
    : showStudentNav
      ? studentNavItems
      : parentNavItems;
  const navItems = [...roleNavItems, ...commonNavItems];

  // Only Educators have an org role, so only fetch the profile for them. The
  // shared module cache means this reuses any fetch the educator pages already
  // triggered.
  const { profile: educatorProfile, isLoading: educatorProfileLoading } =
    useEducatorProfile({ enabled: user?.role === 'Educator' });
  const isDistrictAdmin =
    showEducatorNav && educatorProfile?.orgRoleId === ORG_ROLE.DistrictAdmin;
  const isSchoolAdmin =
    showEducatorNav && educatorProfile?.orgRoleId === ORG_ROLE.SchoolAdmin;
  // Until the educator profile resolves we don't yet know the admin scope, so
  // reserve the Administration group with a skeleton instead of flashing the
  // wrong nav (showing then hiding admin items, or jumping the layout).
  const adminGroupPending = showEducatorNav && educatorProfileLoading;
  // The Administration group appears for any admin scope; items inside are
  // gated individually (Schools is DistrictAdmin-only).
  const showAdminGroup = isDistrictAdmin || isSchoolAdmin;
  const visibleAdminItems = adminNavItems.filter(
    (item) => isDistrictAdmin || (isSchoolAdmin && item.schoolAdmin)
  );

  // Exact-match the section roots (/educator, /student) so they don't stay
  // highlighted while on a nested route.
  const itemIsActive = (to: string) =>
    to === '/educator' || to === '/student'
      ? location.pathname === to
      : isActive(to);

  const navContent = (
    <>
      <div className="flex items-center justify-between p-6">
        <Logo variant="dark" size="md" data-testid="sidebar-logo" />
        <NotificationBell />
      </div>

      <nav className="flex-1 px-3 space-y-1">
        {navItems.map(({ to, label, Icon }) => {
          const active = itemIsActive(to);
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
          href="mailto:support@iep-advisor.com"
          data-testid="nav-support"
          className="flex items-center gap-3 px-3 py-2.5 rounded-button text-sm text-brand-slate-400 hover:text-brand-slate-200 hover:bg-brand-slate-700 transition-colors"
        >
          <LifeBuoy size={18} strokeWidth={1.8} />
          Support
        </a>
        <p className="px-3 text-[11px] text-brand-slate-500" data-testid="nav-support-note">
          We reply within 1 business day
        </p>
      </nav>

      {adminGroupPending && (
        <div className="px-3 mt-2" aria-hidden="true" data-testid="district-admin-nav-loading">
          <div className="border-t border-brand-slate-700 pt-3 mb-2">
            <Skeleton className="mx-3 h-2.5 w-24 bg-brand-slate-700" />
          </div>
          <div className="space-y-1">
            <Skeleton className="mx-3 h-9 bg-brand-slate-700" />
            <Skeleton className="mx-3 h-9 bg-brand-slate-700" />
          </div>
        </div>
      )}

      {!adminGroupPending && showAdminGroup && (
        <div className="px-3 mt-2" data-testid="district-admin-nav">
          <div className="border-t border-brand-slate-700 pt-3 mb-2">
            <span className="px-3 text-[10px] uppercase tracking-wider font-semibold text-brand-teal-400">
              Administration
            </span>
          </div>
          {visibleAdminItems.map(({ to, label, Icon }) => (
            <Link
              key={to}
              to={to}
              data-testid={`nav-${to.slice(1)}`}
              onClick={() => setMobileOpen(false)}
              className={`flex items-center gap-3 px-3 py-2.5 rounded-button text-sm transition-colors ${
                isActive(to)
                  ? 'text-brand-teal-400 bg-brand-slate-700 border-l-2 border-brand-teal-500 -ml-px'
                  : 'text-brand-slate-400 hover:text-brand-slate-200 hover:bg-brand-slate-700'
              }`}
            >
              <Icon size={18} strokeWidth={1.8} />
              {label}
            </Link>
          ))}
        </div>
      )}

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
          <Link
            to="/admin/templates"
            data-testid="nav-admin-templates"
            onClick={() => setMobileOpen(false)}
            className={`flex items-center gap-3 px-3 py-2.5 rounded-button text-sm transition-colors ${
              isActive('/admin/templates')
                ? 'text-brand-teal-400 bg-brand-slate-700 border-l-2 border-brand-teal-500 -ml-px'
                : 'text-brand-slate-400 hover:text-brand-slate-200 hover:bg-brand-slate-700'
            }`}
          >
            <FileText size={18} strokeWidth={1.8} />
            Templates
          </Link>
          <Link
            to="/admin/notifications"
            data-testid="nav-admin-notifications"
            onClick={() => setMobileOpen(false)}
            className={`flex items-center gap-3 px-3 py-2.5 rounded-button text-sm transition-colors ${
              isActive('/admin/notifications')
                ? 'text-brand-teal-400 bg-brand-slate-700 border-l-2 border-brand-teal-500 -ml-px'
                : 'text-brand-slate-400 hover:text-brand-slate-200 hover:bg-brand-slate-700'
            }`}
          >
            <MailWarning size={18} strokeWidth={1.8} />
            Email failures
          </Link>
          <Link
            to="/admin/email"
            data-testid="nav-admin-email"
            onClick={() => setMobileOpen(false)}
            className={`flex items-center gap-3 px-3 py-2.5 rounded-button text-sm transition-colors ${
              isActive('/admin/email')
                ? 'text-brand-teal-400 bg-brand-slate-700 border-l-2 border-brand-teal-500 -ml-px'
                : 'text-brand-slate-400 hover:text-brand-slate-200 hover:bg-brand-slate-700'
            }`}
          >
            <Mail size={18} strokeWidth={1.8} />
            Outbound email
          </Link>
          <Link
            to="/admin/audit"
            data-testid="nav-admin-audit"
            onClick={() => setMobileOpen(false)}
            className={`flex items-center gap-3 px-3 py-2.5 rounded-button text-sm transition-colors ${
              isActive('/admin/audit')
                ? 'text-brand-teal-400 bg-brand-slate-700 border-l-2 border-brand-teal-500 -ml-px'
                : 'text-brand-slate-400 hover:text-brand-slate-200 hover:bg-brand-slate-700'
            }`}
          >
            <ShieldCheck size={18} strokeWidth={1.8} />
            Audit integrity
          </Link>
        </div>
      )}

      {/* `min-w-0`: this footer is a flex item of the column-flex `<aside>`,
          whose default `min-width: auto` can hold it to its text's natural
          (unwrapped) width and defeat `truncate` below for a long name/email —
          overriding it lets the block actually shrink to the rail's width. */}
      <div className="min-w-0 border-t border-brand-slate-700 p-4">
        <div className="mb-2 min-w-0">
          <p
            className="truncate text-sm text-brand-slate-300"
            title={`${user?.firstName ?? ''} ${user?.lastName ?? ''}`.trim() || undefined}
          >
            {user?.firstName} {user?.lastName}
          </p>
          <p className="truncate text-xs text-brand-slate-500" title={user?.email || undefined}>
            {user?.email}
          </p>
        </div>
        <button
          onClick={onLogout}
          data-testid="sidebar-sign-out"
          className="flex items-center gap-2 text-sm text-brand-slate-400 hover:text-brand-slate-200 transition-colors"
        >
          <LogOut size={16} strokeWidth={1.8} />
          Sign Out
        </button>
        <a
          href={TRUST_URL}
          target="_blank"
          rel="noopener noreferrer"
          data-testid="nav-trust"
          className="mt-3 block text-xs text-brand-slate-500 hover:text-brand-slate-300 transition-colors"
        >
          Trust &amp; privacy
        </a>
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
