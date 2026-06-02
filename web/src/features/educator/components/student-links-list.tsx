import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import type { ChildLink } from '../types';

interface StudentLinksListProps {
  links: ChildLink[];
  revokingId: number | null;
  onRevoke: (link: ChildLink) => void;
}

function formatDate(value?: string | null): string | null {
  if (!value) return null;
  return new Date(value).toLocaleDateString();
}

export function StudentLinksList({ links, revokingId, onRevoke }: StudentLinksListProps) {
  if (links.length === 0) {
    return (
      <p className="text-brand-slate-400 text-sm" data-testid="student-links-empty">
        No parent links yet.
      </p>
    );
  }

  return (
    <ul className="space-y-2" data-testid="student-links-list">
      {links.map((link) => {
        const linkedDate = formatDate(link.linkedAt);
        const createdDate = formatDate(link.createdAt);
        return (
          <li key={link.id}>
            <Card className="flex justify-between items-start gap-4" data-testid={`student-link-${link.id}`}>
              <div className="space-y-1">
                <div className="flex items-center gap-2">
                  {link.isAccepted ? (
                    <Badge variant="success">Linked</Badge>
                  ) : (
                    <Badge variant="warning">Pending</Badge>
                  )}
                  {link.inviteEmail && (
                    <span className="text-sm text-brand-slate-700">{link.inviteEmail}</span>
                  )}
                </div>
                <p className="text-xs text-brand-slate-400">
                  {link.isAccepted && linkedDate
                    ? `Linked ${linkedDate}`
                    : createdDate
                      ? `Invited ${createdDate}`
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
                  {revokingId === link.id ? 'Revoking...' : 'Revoke'}
                </Button>
              )}
            </Card>
          </li>
        );
      })}
    </ul>
  );
}
