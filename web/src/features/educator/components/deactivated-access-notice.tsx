import { useTranslation } from 'react-i18next';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';

// Shown in place of the dashboard when a staff member's profile has been
// deactivated. Their JWT is also invalidated server-side on the next request;
// this is the in-app state while the current page is still mounted.
export function DeactivatedAccessNotice() {
  const { t } = useTranslation('educator');
  return (
    <Card className="max-w-lg" data-testid="educator-deactivated-notice">
      <Notice variant="warning" title={t('deactivatedNotice.title')}>
        {t('deactivatedNotice.body')}
      </Notice>
    </Card>
  );
}
