import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Users, FileText, UserCircle } from 'lucide-react';
import { DashboardChildrenSection } from '@/features/children/components/dashboard-children-section';
import { Card } from '@/components/ui/card';
import type { User } from '@/types/api';
import { AccountSetupNotices } from './account-setup-notices';

interface LegacyParentHomeProps {
  user: User | null;
}

/**
 * "Mode C" parent home — a standalone consumer parent with no school-linked
 * child. None of the new operational sections have anything to show for this
 * mode (no meetings, no school-issued documents), so this renders exactly the
 * previous `DashboardPage` body, unchanged: onboarding/state setup notices,
 * children, account, and quick actions.
 */
export function LegacyParentHome({ user }: LegacyParentHomeProps) {
  const { t } = useTranslation('home');
  return (
    <div className="space-y-6" data-testid="parent-home-legacy">
      <AccountSetupNotices user={user} />

      <DashboardChildrenSection />

      <Card>
        <h2 className="font-serif mb-4">{t('legacy.yourAccount')}</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="bg-brand-slate-50 rounded-card p-4 border border-brand-slate-200">
            <p className="text-[11px] text-brand-slate-500 uppercase tracking-wide font-semibold">
              {t('legacy.email')}
            </p>
            <p className="text-sm font-medium text-brand-slate-800 mt-1">{user?.email}</p>
          </div>
          <div className="bg-brand-slate-50 rounded-card p-4 border border-brand-slate-200">
            <p className="text-[11px] text-brand-slate-500 uppercase tracking-wide font-semibold">
              {t('legacy.state')}
            </p>
            <p className="text-sm font-medium text-brand-slate-800 mt-1">
              {user?.state || t('common:ui.notSet')}
            </p>
          </div>
        </div>
      </Card>

      <Card>
        <h2 className="font-serif mb-4">{t('legacy.quickActions')}</h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
          <Link
            to="/children"
            className="flex items-center gap-3 p-4 rounded-card border border-brand-slate-200 hover:bg-brand-teal-50 hover:border-brand-teal-100 transition-colors"
          >
            <Users className="text-brand-teal-500 shrink-0" size={20} strokeWidth={1.8} aria-hidden="true" />
            <div>
              <p className="text-sm font-medium text-brand-slate-800">{t('legacy.addChildTitle')}</p>
              <p className="text-[11px] text-brand-slate-500">{t('legacy.addChildSubtitle')}</p>
            </div>
          </Link>
          <Link
            to="/children"
            className="flex items-center gap-3 p-4 rounded-card border border-brand-slate-200 hover:bg-brand-teal-50 hover:border-brand-teal-100 transition-colors"
          >
            <FileText className="text-brand-teal-500 shrink-0" size={20} strokeWidth={1.8} aria-hidden="true" />
            <div>
              <p className="text-sm font-medium text-brand-slate-800">{t('legacy.uploadIepTitle')}</p>
              <p className="text-[11px] text-brand-slate-500">{t('legacy.uploadIepSubtitle')}</p>
            </div>
          </Link>
          <Link
            to="/profile"
            className="flex items-center gap-3 p-4 rounded-card border border-brand-slate-200 hover:bg-brand-teal-50 hover:border-brand-teal-100 transition-colors"
          >
            <UserCircle className="text-brand-teal-500 shrink-0" size={20} strokeWidth={1.8} aria-hidden="true" />
            <div>
              <p className="text-sm font-medium text-brand-slate-800">{t('legacy.editProfileTitle')}</p>
              <p className="text-[11px] text-brand-slate-500">{t('legacy.editProfileSubtitle')}</p>
            </div>
          </Link>
        </div>
      </Card>
    </div>
  );
}
