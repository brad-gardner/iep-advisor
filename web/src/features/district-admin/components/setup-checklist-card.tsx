import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { CheckCircle, Circle } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { getDistrict } from '../api/district-api';
import type { DistrictOverview } from '../types';

interface ChecklistItem {
  key: string;
  label: string;
  done: boolean;
  to: string;
}

// "Finish setting up" nudge for DistrictAdmins. Self-contained: fetches the
// district overview and derives completion from active school/staff counts. Hides
// itself entirely once both counts are non-zero. No schema — purely derived.
//
// `district-admin` is a staff-only namespace (multilingual plan phase 6) —
// this card renders only behind the lazy staff route chunk (inside
// `StaffHomePage` → `AdminHome`), never eagerly, so using it here is safe;
// see `docs/i18n/README.md`'s "Staff and admin namespaces".
export function SetupChecklistCard() {
  const { t } = useTranslation('district-admin');
  const [overview, setOverview] = useState<DistrictOverview | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const response = await getDistrict();
        if (!cancelled) {
          setOverview(response.success && response.data ? response.data : null);
        }
      } catch {
        if (!cancelled) setOverview(null);
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  if (isLoading || !overview) {
    return null;
  }

  const hasSchool = overview.activeSchoolCount > 0;
  const hasStaff = overview.activeStaffCount > 0;

  // Both done -> nothing left to nudge.
  if (hasSchool && hasStaff) {
    return null;
  }

  const items: ChecklistItem[] = [
    {
      key: 'school',
      label: t('setupChecklist.schoolItem'),
      done: hasSchool,
      to: '/educator/admin/schools',
    },
    {
      key: 'staff',
      label: t('setupChecklist.staffItem'),
      done: hasStaff,
      to: '/educator/admin/staff',
    },
  ];

  return (
    <Card className="max-w-lg" accent data-testid="district-setup-checklist">
      <h2 className="font-serif text-xl mb-1">{t('setupChecklist.title')}</h2>
      <p className="text-sm text-brand-slate-500 mb-4">
        {t('setupChecklist.subtitle')}
      </p>
      <ul className="space-y-2">
        {items.map((item) => (
          <li
            key={item.key}
            className="flex items-center justify-between gap-3"
            data-testid={`district-setup-checklist-${item.key}`}
          >
            <span className="flex items-center gap-2 text-sm">
              {item.done ? (
                <CheckCircle
                  className="text-brand-teal-500 shrink-0"
                  size={18}
                  strokeWidth={1.8}
                  aria-hidden="true"
                />
              ) : (
                <Circle
                  className="text-brand-slate-300 shrink-0"
                  size={18}
                  strokeWidth={1.8}
                  aria-hidden="true"
                />
              )}
              <span
                className={
                  item.done
                    ? 'text-brand-slate-500 line-through'
                    : 'text-brand-slate-800'
                }
              >
                {item.label}
              </span>
            </span>
            {!item.done && (
              <Link to={item.to}>
                <Button
                  variant="secondary"
                  data-testid={`district-setup-checklist-${item.key}-link`}
                >
                  {t('setupChecklist.start')}
                </Button>
              </Link>
            )}
          </li>
        ))}
      </ul>
    </Card>
  );
}
