import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Ticket } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { useToast } from '@/components/ui/toast';
import { usePageTitle } from '@/hooks/use-page-title';
import { redeemInvite } from '../api/subscription-api';

export function RedeemInvitePage() {
  const { t } = useTranslation('subscription');
  usePageTitle(t('redeemPage.pageTitle'));
  const { show } = useToast();
  const [code, setCode] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!code.trim()) return;

    setIsSubmitting(true);
    setError(null);

    try {
      const response = await redeemInvite(code.trim());
      if (response.success) {
        // Transient success → toast; inline space stays for decisions/errors.
        show({
          message: t('redeemPage.successToast'),
          variant: 'success',
        });
        setCode('');
      } else {
        setError(response.message || t('redeemPage.invalidCode'));
      }
    } catch {
      setError(t('redeemPage.invalidCode'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <PageLayout title={t('redeemPage.pageLayoutTitle')}>
      <Card className="max-w-md">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-10 h-10 rounded-full bg-brand-teal-50 flex items-center justify-center">
            <Ticket className="w-5 h-5 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
          </div>
          <p className="text-sm text-brand-slate-500">
            {t('redeemPage.description')}
          </p>
        </div>

        {error && (
          <div className="mb-4">
            <Notice variant="error" title={error} />
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <Input
            label={t('redeemPage.codeLabel')}
            value={code}
            onChange={(e) => setCode(e.target.value.toUpperCase().slice(0, 8))}
            placeholder={t('redeemPage.codePlaceholder')}
            maxLength={8}
            required
            data-testid="redeem-code"
          />
          <Button
            type="submit"
            disabled={code.length < 8}
            loading={isSubmitting}
            className="w-full"
            data-testid="redeem-submit"
          >
            {t('redeemPage.submit')}
          </Button>
        </form>
      </Card>
    </PageLayout>
  );
}
