import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Notice } from "@/components/ui/notice";

interface InviteParentFormProps {
  onInvite: (email: string) => Promise<{ success: boolean; message?: string }>;
  /** When hosted inside a Modal/Drawer, drop the self-`Card` + heading. */
  embedded?: boolean;
}

export function InviteParentForm({
  onInvite,
  embedded = false,
}: InviteParentFormProps) {
  const { t } = useTranslation('educator');
  const [email, setEmail] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError(null);
    setSuccessMessage(null);

    const result = await onInvite(email.trim());

    if (result.success) {
      setSuccessMessage(result.message || t('inviteParentForm.successDefault'));
      setEmail("");
    } else {
      setError(result.message ?? t('inviteParentForm.errorDefault'));
    }

    setIsSubmitting(false);
  };

  const form = (
    <form
      onSubmit={handleSubmit}
      className="space-y-4"
      data-testid="invite-parent-form"
    >
      {error && <Notice variant="error" title={error} />}
      {successMessage && (
        <Notice variant="success" title={t('inviteParentForm.successTitle')}>
          {successMessage}
        </Notice>
      )}

      <Input
        label={t('inviteParentForm.emailLabel')}
        type="email"
        required
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        maxLength={256}
        data-testid="invite-parent-email"
      />

      <Button
        type="submit"
        disabled={isSubmitting}
        className="w-full"
        data-testid="invite-parent-submit"
      >
        {isSubmitting ? t('inviteParentForm.sending') : t('inviteParentForm.submit')}
      </Button>
    </form>
  );

  if (embedded) return form;
  return (
    <Card className="max-w-lg">
      <h2 className="font-serif text-lg mb-4">{t('familyLinks.inviteModalTitle')}</h2>
      {form}
    </Card>
  );
}
