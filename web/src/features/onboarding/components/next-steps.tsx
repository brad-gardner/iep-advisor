import { FileText, Target, ClipboardCheck, GitCompare } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';

interface NextStepsProps {
  onFinish: () => void;
}

const features = [
  {
    Icon: FileText,
    title: 'Upload an IEP',
    description: "Get AI-powered analysis of your child's IEP document",
  },
  {
    Icon: Target,
    title: 'Set Advocacy Goals',
    description: 'Define your priorities and check if the IEP addresses them',
  },
  {
    Icon: ClipboardCheck,
    title: 'Prep for Meetings',
    description: 'Get actionable checklists to walk in prepared',
  },
  {
    Icon: GitCompare,
    title: 'Compare Versions',
    description: "Track how your child's IEP changes over time",
  },
];

export function NextSteps({ onFinish }: NextStepsProps) {
  return (
    <div className="space-y-6">
      <div className="text-center space-y-2">
        <h1 className="font-serif text-2xl text-brand-slate-800">
          You're All Set!
        </h1>
        <p className="text-sm text-brand-slate-500 leading-relaxed max-w-md mx-auto">
          Here's what you can do with IEP Advisor. Start wherever feels right
          for you.
        </p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        {features.map(({ Icon, title, description }) => (
          <Card key={title} className="flex items-start gap-3 p-4">
            <div className="bg-brand-teal-50 rounded-full p-2 shrink-0">
              <Icon
                className="text-brand-teal-500"
                size={20}
                strokeWidth={1.8}
                aria-hidden="true"
              />
            </div>
            <div>
              <p className="text-sm font-medium text-brand-slate-800">
                {title}
              </p>
              <p className="text-xs text-brand-slate-400 mt-0.5 leading-relaxed">
                {description}
              </p>
            </div>
          </Card>
        ))}
      </div>

      <div className="flex items-center justify-center gap-3 pt-2">
        <Link to="/iep-101">
          <Button variant="secondary">Learn about IEPs</Button>
        </Link>
        <Button onClick={onFinish}>Go to Dashboard</Button>
      </div>
    </div>
  );
}
