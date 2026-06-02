import { Sparkles } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { useAuth } from '@/features/auth/hooks/use-auth';

// Placeholder landing for the student. The self-advocacy workspace lands in P8;
// for now this welcomes the student and sets expectations.
export function StudentHomePage() {
  const { user } = useAuth();

  return (
    <div className="space-y-6" data-testid="student-home">
      <div>
        <h1 className="font-serif">
          {user?.firstName ? `Welcome, ${user.firstName}` : 'Your space'}
        </h1>
        <p className="text-sm text-brand-slate-400 mt-1">
          This is your space to understand and take part in your IEP.
        </p>
      </div>

      <Card className="max-w-lg" data-testid="student-home-intro">
        <div className="flex items-start gap-3">
          <Sparkles
            className="w-5 h-5 text-brand-teal-500 mt-0.5 shrink-0"
            strokeWidth={1.8}
            aria-hidden="true"
          />
          <div>
            <h2 className="font-serif text-lg mb-1">Your workspace is coming soon</h2>
            <p className="text-sm text-brand-slate-600">
              Tools to help you understand your goals, share your voice, and prepare
              for meetings will appear here shortly. Your account is now active and
              linked to your IEP team.
            </p>
          </div>
        </div>
      </Card>
    </div>
  );
}
