import { useTranslation } from 'react-i18next';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { formatDate } from '@/lib/format-date';
import type { ChildLink } from '../types';

interface StudentLinksListProps {
  links: ChildLink[];
  revokingId: number | null;
  onRevoke: (link: ChildLink) => void;
}

export function StudentLinksList({ links, revokingId, onRevoke }: StudentLinksListProps) {
  const { t } = useTranslation('educator');

  if (links.length === 0) {
    return (
      <p className="text-brand-slate-500 text-sm" data-testid="student-links-empty">
        {t('studentLinks.empty')}
      </p>
    );
  }

  return (
    <ul className="space-y-2" data-testid="student-links-list">
      {links.map((link) => {
        const linkedDate = link.linkedAt ? formatDate(link.linkedAt) : null;
        const createdDate = link.createdAt ? formatDate(link.createdAt) : null;
        return (
          <li key={link.id}>
            <Card className="flex justify-between items-start gap-4" data-testid={`student-link-${link.id}`}>
              <div className="space-y-1">
                <div className="flex items-center gap-2">
                  {link.isAccepted ? (
                    <Badge variant="success">{t('studentLinks.linkedBadge')}</Badge>
                  ) : (
                    <Badge variant="warning">{t('studentLinks.pendingBadge')}</Badge>
                  )}
                  {link.inviteEmail && (
                    <span className="text-sm text-brand-slate-700">{link.inviteEmail}</span>
                  )}
                </div>
                <p className="text-xs text-brand-slate-500">
                  {link.isAccepted && linkedDate
                    ? t('studentLinks.linked', { date: linkedDate })
                    : createdDate
                      ? t('studentLinks.invited', { date: createdDate })
                      : null}
                </p>
              </div>
              {link.isActive && (
                <Button
                  variant="danger"
                  onClick={() => onRevoke(link)}
                  disabled={revokingId === link.id}
                  data-testid={`student-link-revoke-${link.id}`}
                >
                  {revokingId === link.id ? t('studentLinks.revoking') : t('studentLinks.revoke')}
                </Button>
              )}
            </Card>
          </li>
        );
      })}
    </ul>
  );
}
