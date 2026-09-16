import { Link } from 'react-router-dom';
import { Users, FileText, UserCircle, ArrowRight } from 'lucide-react';
import { DashboardChildrenSection } from '@/features/children/components/dashboard-children-section';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Button } from '@/components/ui/button';
import type { User } from '@/types/api';

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
  return (
    <div className="space-y-6" data-testid="parent-home-legacy">
      {user && !user.onboardingCompleted && (
        <div data-testid="onboarding-banner">
          <Notice variant="info" title="Complete your setup to get the most out of IEP Advisor">
            <Link to="/onboarding">
              <Button variant="primary" className="mt-2 gap-1.5" data-testid="onboarding-get-started">
                Get Started
                <ArrowRight size={14} strokeWidth={1.8} aria-hidden="true" />
              </Button>
            </Link>
          </Notice>
        </div>
      )}

      {!user?.state && user?.onboardingCompleted && (
        <Notice variant="warning" title="Set your state for better guidance">
          <Link to="/profile" className="underline hover:text-brand-amber-600">
            Update your profile
          </Link>{' '}
          to get jurisdiction-specific IEP guidance.
        </Notice>
      )}

      <DashboardChildrenSection />

      <Card>
        <h2 className="font-serif mb-4">Your Account</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="bg-brand-slate-50 rounded-card p-4 border-[0.5px] border-brand-slate-200">
            <p className="text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold">
              Email
            </p>
            <p className="text-sm font-medium text-brand-slate-800 mt-1">{user?.email}</p>
          </div>
          <div className="bg-brand-slate-50 rounded-card p-4 border-[0.5px] border-brand-slate-200">
            <p className="text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold">
              State
            </p>
            <p className="text-sm font-medium text-brand-slate-800 mt-1">
              {user?.state || 'Not set'}
            </p>
          </div>
        </div>
      </Card>

      <Card>
        <h2 className="font-serif mb-4">Quick Actions</h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
          <Link
            to="/children"
            className="flex items-center gap-3 p-4 rounded-card border-[0.5px] border-brand-slate-200 hover:bg-brand-teal-50 hover:border-brand-teal-100 transition-colors"
          >
            <Users className="text-brand-teal-500 shrink-0" size={20} strokeWidth={1.8} aria-hidden="true" />
            <div>
              <p className="text-sm font-medium text-brand-slate-800">Add Child Profile</p>
              <p className="text-[11px] text-brand-slate-400">Create a profile for your child</p>
            </div>
          </Link>
          <Link
            to="/children"
            className="flex items-center gap-3 p-4 rounded-card border-[0.5px] border-brand-slate-200 hover:bg-brand-teal-50 hover:border-brand-teal-100 transition-colors"
          >
            <FileText className="text-brand-teal-500 shrink-0" size={20} strokeWidth={1.8} aria-hidden="true" />
            <div>
              <p className="text-sm font-medium text-brand-slate-800">Upload an IEP</p>
              <p className="text-[11px] text-brand-slate-400">Select a child to upload</p>
            </div>
          </Link>
          <Link
            to="/profile"
            className="flex items-center gap-3 p-4 rounded-card border-[0.5px] border-brand-slate-200 hover:bg-brand-teal-50 hover:border-brand-teal-100 transition-colors"
          >
            <UserCircle className="text-brand-teal-500 shrink-0" size={20} strokeWidth={1.8} aria-hidden="true" />
            <div>
              <p className="text-sm font-medium text-brand-slate-800">Edit Profile</p>
              <p className="text-[11px] text-brand-slate-400">Update your information</p>
            </div>
          </Link>
        </div>
      </Card>
    </div>
  );
}
