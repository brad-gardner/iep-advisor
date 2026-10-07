import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Send } from 'lucide-react';
import { createInvite } from '../api/sharing-api';
import { Button } from '@/components/ui/button';
import { Input, Select } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { useToast } from '@/components/ui/toast';

interface ShareChildDialogProps {
  childId: number;
  onInvited: () => void;
  onCancel: () => void;
}

export function ShareChildDialog({ childId, onInvited, onCancel }: ShareChildDialogProps) {
  const { t } = useTranslation('sharing');
  const [email, setEmail] = useState('');
  const [role, setRole] = useState('viewer');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { show } = useToast();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim()) return;

    setIsSubmitting(true);
    setError(null);

    try {
      const response = await createInvite(childId, { email: email.trim(), role });
      if (response.success) {
        setEmail('');
        show({ message: t('shareDialog.successToast'), variant: 'success' });
        onInvited();
      } else {
        setError(response.message || t('shareDialog.sendFailed'));
      }
    } catch {
      setError(t('shareDialog.sendError'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="bg-brand-slate-50 rounded-card p-4 border border-brand-slate-200">
      <h3 className="font-serif text-brand-slate-800 mb-3">{t('shareDialog.heading')}</h3>

      {error && (
        <div className="mb-3">
          <Notice variant="error" title={error} />
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-3">
        <Input
          label={t('shareDialog.emailLabel')}
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder={t('shareDialog.emailPlaceholder')}
          required
          maxLength={256}
          data-testid="share-email"
        />

        <Select
          label={t('shareDialog.roleLabel')}
          value={role}
          onChange={(e) => setRole(e.target.value)}
          data-testid="share-role"
        >
          <option value="viewer">{t('role.viewer')}</option>
          <option value="collaborator">{t('role.collaborator')}</option>
        </Select>

        <div className="flex gap-2 pt-1">
          <Button
            type="submit"
            loading={isSubmitting}
            disabled={!email.trim()}
            data-testid="share-submit"
          >
            <Send className="w-4 h-4 mr-1.5" strokeWidth={1.8} aria-hidden="true" />
            {t('shareDialog.sendInvite')}
          </Button>
          <Button variant="ghost" type="button" onClick={onCancel}>
            {t('common:ui.cancel')}
          </Button>
        </div>
      </form>
    </div>
  );
}
