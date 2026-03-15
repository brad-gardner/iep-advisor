import { BookOpen, FileText, Users, Shield, Calendar, Library } from 'lucide-react';
import { Card } from '@/components/ui/card';

function SectionHeader({
  Icon,
  title,
}: {
  Icon: typeof BookOpen;
  title: string;
}) {
  return (
    <div className="flex items-center gap-3 mb-3">
      <div className="bg-brand-teal-50 rounded-full p-2 shrink-0">
        <Icon
          className="text-brand-teal-500"
          size={20}
          strokeWidth={1.8}
          aria-hidden="true"
        />
      </div>
      <h2 className="font-serif text-lg text-brand-slate-800">{title}</h2>
    </div>
  );
}

function GlossaryTerm({ term, definition }: { term: string; definition: string }) {
  return (
    <div className="py-2 border-b border-brand-slate-100 last:border-0">
      <dt className="text-sm font-medium text-brand-slate-800">{term}</dt>
      <dd className="text-sm text-brand-slate-500 mt-0.5">{definition}</dd>
    </div>
  );
}

export function Iep101Page() {
  return (
    <div className="space-y-6 max-w-3xl">
      {/* Page header */}
      <div className="flex items-center gap-3">
        <div className="bg-brand-teal-50 rounded-full p-2.5">
          <BookOpen
            className="text-brand-teal-500"
            size={24}
            strokeWidth={1.8}
            aria-hidden="true"
          />
        </div>
        <div>
          <h1 className="font-serif text-2xl text-brand-slate-800">IEP 101</h1>
          <p className="text-sm text-brand-slate-400">
            Everything you need to know, in plain language
          </p>
        </div>
      </div>

      {/* What is an IEP? */}
      <Card>
        <SectionHeader Icon={FileText} title="What is an IEP?" />
        <p className="text-sm text-brand-slate-600 leading-relaxed">
          An Individualized Education Program is a legally binding document that
          outlines the special education services your child will receive. Think
          of it as your child's educational roadmap — it describes where they are
          now, where they're headed, and how they'll get there.
        </p>
      </Card>

      {/* Who Gets an IEP? */}
      <Card>
        <SectionHeader Icon={Users} title="Who Gets an IEP?" />
        <p className="text-sm text-brand-slate-600 leading-relaxed">
          Children aged 3-21 who have a disability that affects their learning.
          There are 13 categories under IDEA (the Individuals with Disabilities
          Education Act), including autism, specific learning disabilities,
          speech/language impairments, and more. Your child must be evaluated and
          found eligible before an IEP can be created.
        </p>
      </Card>

      {/* What's in an IEP? */}
      <Card>
        <SectionHeader Icon={FileText} title="What's in an IEP?" />
        <ul className="space-y-3 text-sm text-brand-slate-600">
          <li className="flex gap-2">
            <span className="font-medium text-brand-slate-800 shrink-0">
              Present Levels:
            </span>
            <span>
              How your child is currently doing academically and functionally
            </span>
          </li>
          <li className="flex gap-2">
            <span className="font-medium text-brand-slate-800 shrink-0">
              Annual Goals:
            </span>
            <span>
              Measurable targets for the year (these should be SMART — Specific,
              Measurable, Achievable, Relevant, Time-bound)
            </span>
          </li>
          <li className="flex gap-2">
            <span className="font-medium text-brand-slate-800 shrink-0">
              Services:
            </span>
            <span>
              What support your child will receive (speech therapy, OT, tutoring,
              etc.)
            </span>
          </li>
          <li className="flex gap-2">
            <span className="font-medium text-brand-slate-800 shrink-0">
              Accommodations:
            </span>
            <span>
              Changes to how your child learns or is tested (extra time,
              preferential seating, etc.)
            </span>
          </li>
          <li className="flex gap-2">
            <span className="font-medium text-brand-slate-800 shrink-0">
              Placement:
            </span>
            <span>
              Where your child receives services (general education classroom,
              resource room, etc.)
            </span>
          </li>
        </ul>
      </Card>

      {/* Your Rights as a Parent */}
      <Card>
        <SectionHeader Icon={Shield} title="Your Rights as a Parent" />
        <ul className="space-y-2 text-sm text-brand-slate-600">
          <li className="flex items-start gap-2">
            <span className="text-brand-teal-500 mt-1 shrink-0">&#8226;</span>
            You are an equal member of the IEP team
          </li>
          <li className="flex items-start gap-2">
            <span className="text-brand-teal-500 mt-1 shrink-0">&#8226;</span>
            You must give consent before evaluations and services begin
          </li>
          <li className="flex items-start gap-2">
            <span className="text-brand-teal-500 mt-1 shrink-0">&#8226;</span>
            You have the right to see all records related to your child
          </li>
          <li className="flex items-start gap-2">
            <span className="text-brand-teal-500 mt-1 shrink-0">&#8226;</span>
            The school must give you Prior Written Notice before making changes
          </li>
          <li className="flex items-start gap-2">
            <span className="text-brand-teal-500 mt-1 shrink-0">&#8226;</span>
            You can disagree — and there are formal processes to resolve disputes
            (mediation, due process)
          </li>
        </ul>
      </Card>

      {/* What to Expect at an IEP Meeting */}
      <Card>
        <SectionHeader
          Icon={Calendar}
          title="What to Expect at an IEP Meeting"
        />
        <p className="text-sm text-brand-slate-600 leading-relaxed">
          The meeting typically lasts 1-2 hours. The team reviews your child's
          progress, discusses goals for the coming year, and decides on services
          and placement. You can bring anyone you want to support you — an
          advocate, family member, or friend.
        </p>
      </Card>

      {/* Common Terms */}
      <Card>
        <SectionHeader Icon={Library} title="Common Terms" />
        <dl className="divide-y-0">
          <GlossaryTerm
            term="FAPE"
            definition="Free Appropriate Public Education"
          />
          <GlossaryTerm
            term="LRE"
            definition="Least Restrictive Environment"
          />
          <GlossaryTerm
            term="IDEA"
            definition="Individuals with Disabilities Education Act"
          />
          <GlossaryTerm
            term="Related Services"
            definition="Support services like speech, OT, PT, counseling"
          />
          <GlossaryTerm
            term="Transition"
            definition="Planning for life after high school (starts at age 16)"
          />
          <GlossaryTerm
            term="Prior Written Notice (PWN)"
            definition="Written notification the school must provide before changing your child's program"
          />
          <GlossaryTerm
            term="Due Process"
            definition="A formal hearing to resolve disagreements"
          />
        </dl>
      </Card>
    </div>
  );
}
