import { Link } from 'react-router-dom';
import { Users, FileText, UserCircle, ArrowRight } from 'lucide-react';
import { useAuth } from '../hooks/use-auth';
import { SubscriptionStatusCard } from '@/features/subscription/components/subscription-status';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Button } from '@/components/ui/button';

export function DashboardPage() {
  const { user } = useAuth();

  return (
    <div className="space-y-6">
      <h1 className="font-serif">Welcome, {user?.firstName}</h1>

      {user && !user.onboardingCompleted && (
        <Notice variant="info" title="Complete your setup to get the most out of IEP Advisor">
          <Link to="/onboarding">
            <Button variant="primary" className="mt-2 gap-1.5">
              Get Started
              <ArrowRight size={14} strokeWidth={1.8} aria-hidden="true" />
            </Button>
          </Link>
        </Notice>
      )}

      {!user?.state && user?.onboardingCompleted && (
        <Notice variant="warning" title="Set your state for better guidance">
          <Link to="/profile" className="underline hover:text-brand-amber-600">
            Update your profile
          </Link>{' '}
          to get jurisdiction-specific IEP guidance.
        </Notice>
      )}

      <SubscriptionStatusCard />

      <Card>
        <h2 className="font-serif mb-4">Your Account</h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="bg-brand-slate-50 rounded-card p-4 border-[0.5px] border-brand-slate-200">
            <p className="text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold">Email</p>
            <p className="text-sm font-medium text-brand-slate-800 mt-1">{user?.email}</p>
          </div>
          <div className="bg-brand-slate-50 rounded-card p-4 border-[0.5px] border-brand-slate-200">
            <p className="text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold">State</p>
            <p className="text-sm font-medium text-brand-slate-800 mt-1">{user?.state || 'Not set'}</p>
          </div>
          <Link to="/children" className="bg-brand-slate-50 rounded-card p-4 border-[0.5px] border-brand-slate-200 hover:bg-brand-teal-50 hover:border-brand-teal-100 transition-colors block">
            <p className="text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold">Children</p>
            <p className="text-sm font-medium text-brand-teal-500 mt-1">Manage profiles</p>
          </Link>
        </div>
      </Card>

      <Card>
        <h2 className="font-serif mb-4">Quick Actions</h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
          <Link
            to="/children/new"
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
