import { useTranslation } from 'react-i18next';
import { BookOpen, FileText, Users, Shield, Calendar, Library, ArrowRight } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import { usePageTitle } from '@/hooks/use-page-title';

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
  const { t } = useTranslation('onboarding');
  usePageTitle(t('iep101.pageTitle'));
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
          <h1 className="font-serif text-2xl text-brand-slate-800">{t('iep101.heading')}</h1>
          <p className="text-sm text-brand-slate-500">
            {t('iep101.subtitle')}
          </p>
        </div>
      </div>

      {/* Knowledge Base link */}
      <Link
        to="/knowledge-base"
        className="flex items-center gap-2 text-sm font-medium text-brand-teal-500 hover:text-brand-teal-600 transition-colors"
      >
        {t('iep101.kbLink')}
        <ArrowRight size={16} strokeWidth={1.8} />
      </Link>

      {/* What is an IEP? */}
      <Card>
        <SectionHeader Icon={FileText} title={t('iep101.whatIsHeading')} />
        <p className="text-sm text-brand-slate-600 leading-relaxed">
          {t('iep101.whatIsBody')}
        </p>
      </Card>

      {/* Who Gets an IEP? */}
      <Card>
        <SectionHeader Icon={Users} title={t('iep101.whoGetsHeading')} />
        <p className="text-sm text-brand-slate-600 leading-relaxed">
          {t('iep101.whoGetsBody')}
        </p>
      </Card>

      {/* What's in an IEP? */}
      <Card>
        <SectionHeader Icon={FileText} title={t('iep101.whatsInHeading')} />
        <ul className="space-y-3 text-sm text-brand-slate-600">
          <li className="flex gap-2">
            <span className="font-medium text-brand-slate-800 shrink-0">
              {t('iep101.presentLevelsLabel')}
            </span>
            <span>
              {t('iep101.presentLevelsText')}
            </span>
          </li>
          <li className="flex gap-2">
            <span className="font-medium text-brand-slate-800 shrink-0">
              {t('iep101.annualGoalsLabel')}
            </span>
            <span>
              {t('iep101.annualGoalsText')}
            </span>
          </li>
          <li className="flex gap-2">
            <span className="font-medium text-brand-slate-800 shrink-0">
              {t('iep101.servicesLabel')}
            </span>
            <span>
              {t('iep101.servicesText')}
            </span>
          </li>
          <li className="flex gap-2">
            <span className="font-medium text-brand-slate-800 shrink-0">
              {t('iep101.accommodationsLabel')}
            </span>
            <span>
              {t('iep101.accommodationsText')}
            </span>
          </li>
          <li className="flex gap-2">
            <span className="font-medium text-brand-slate-800 shrink-0">
              {t('iep101.placementLabel')}
            </span>
            <span>
              {t('iep101.placementText')}
            </span>
          </li>
        </ul>
      </Card>

      {/* Your Rights as a Parent */}
      <Card>
        <SectionHeader Icon={Shield} title={t('iep101.rightsHeading')} />
        <ul className="space-y-2 text-sm text-brand-slate-600">
          <li className="flex items-start gap-2">
            <span className="text-brand-teal-500 mt-1 shrink-0">&#8226;</span>
            {t('iep101.right1')}
          </li>
          <li className="flex items-start gap-2">
            <span className="text-brand-teal-500 mt-1 shrink-0">&#8226;</span>
            {t('iep101.right2')}
          </li>
          <li className="flex items-start gap-2">
            <span className="text-brand-teal-500 mt-1 shrink-0">&#8226;</span>
            {t('iep101.right3')}
          </li>
          <li className="flex items-start gap-2">
            <span className="text-brand-teal-500 mt-1 shrink-0">&#8226;</span>
            {t('iep101.right4')}
          </li>
          <li className="flex items-start gap-2">
            <span className="text-brand-teal-500 mt-1 shrink-0">&#8226;</span>
            {t('iep101.right5')}
          </li>
        </ul>
      </Card>

      {/* What to Expect at an IEP Meeting */}
      <Card>
        <SectionHeader
          Icon={Calendar}
          title={t('iep101.meetingHeading')}
        />
        <p className="text-sm text-brand-slate-600 leading-relaxed">
          {t('iep101.meetingBody')}
        </p>
      </Card>

      {/* Common Terms */}
      <Card>
        <SectionHeader Icon={Library} title={t('iep101.termsHeading')} />
        <dl className="divide-y-0">
          <GlossaryTerm
            term={t('iep101.termFape')}
            definition={t('iep101.termFapeDef')}
          />
          <GlossaryTerm
            term={t('iep101.termLre')}
            definition={t('iep101.termLreDef')}
          />
          <GlossaryTerm
            term={t('iep101.termIdea')}
            definition={t('iep101.termIdeaDef')}
          />
          <GlossaryTerm
            term={t('iep101.termRelatedServices')}
            definition={t('iep101.termRelatedServicesDef')}
          />
          <GlossaryTerm
            term={t('iep101.termTransition')}
            definition={t('iep101.termTransitionDef')}
          />
          <GlossaryTerm
            term={t('iep101.termPwn')}
            definition={t('iep101.termPwnDef')}
          />
          <GlossaryTerm
            term={t('iep101.termDueProcess')}
            definition={t('iep101.termDueProcessDef')}
          />
        </dl>
      </Card>
    </div>
  );
}
