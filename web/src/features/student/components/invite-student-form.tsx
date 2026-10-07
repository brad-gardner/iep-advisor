import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Notice } from "@/components/ui/notice";
import { useToast } from "@/components/ui/toast";

interface InviteStudentFormProps {
  // Parameterized so the same form serves both the parent and educator flows.
  onInvite: (email: string) => Promise<{ success: boolean; message?: string }>;
  description?: string;
  /** When hosted inside a Modal/Drawer, drop the self-`Card` + heading/description. */
  embedded?: boolean;
}

export function InviteStudentForm({
  onInvite,
  description,
  embedded = false,
}: InviteStudentFormProps) {
  const { t } = useTranslation("student");
  const { show } = useToast();
  const [email, setEmail] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Click-triggered (never a mount effect), so translating inline here is
  // safe — see `AcknowledgeControl` (shared-drafts) for the same reasoning.
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError(null);

    const result = await onInvite(email.trim());

    if (result.success) {
      // Transient success → toast; the form resets for the next invite.
      show({
        message: result.message || t("inviteForm.successDefault"),
        variant: "success",
      });
      setEmail("");
    } else {
      setError(result.message ?? t("inviteForm.failureDefault"));
    }

    setIsSubmitting(false);
  };

  const form = (
    <form
      onSubmit={handleSubmit}
      className="space-y-4"
      data-testid="invite-student-form"
    >
      {error && <Notice variant="error" title={error} />}
      {embedded && description && (
        <p className="text-sm text-brand-slate-500">{description}</p>
      )}

      <Input
        id="invite-student-email"
        label={t("inviteForm.emailLabel")}
        type="email"
        required
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        maxLength={256}
        data-testid="invite-student-email"
      />

      <Button
        type="submit"
        loading={isSubmitting}
        className="w-full"
        data-testid="invite-student-submit"
      >
        {t("inviteForm.submit")}
      </Button>
    </form>
  );

  if (embedded) return form;
  return (
    <Card className="max-w-lg">
      <h2 className="font-serif text-lg mb-2">{t("inviteForm.cardHeading")}</h2>
      <p className="text-sm text-brand-slate-500 mb-4">
        {description ?? t("inviteForm.cardDescriptionDefault")}
      </p>
      {form}
    </Card>
  );
}
