import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useParams, Link } from "react-router-dom";
import { ArrowLeft } from "lucide-react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Notice } from "@/components/ui/notice";
import { Spinner } from "@/components/ui/spinner";
import { Select } from "@/components/ui/input";
import { PageLayout } from "@/components/ui/page-layout";
import { DetailLayout } from "@/components/ui/detail-layout";
import { useToast } from "@/components/ui/toast";
import { usePageTitle } from "@/hooks/use-page-title";
import { formatDate } from "@/lib/format-date";
import type { AdminUser } from "@/types/api";
import { getUser, updateUser } from "../api/admin-api";

export function AdminUserDetail() {
  const { t } = useTranslation(['admin', 'common']);
  const { id } = useParams<{ id: string }>();
  const { show: showToast } = useToast();
  const [user, setUser] = useState<AdminUser | null>(null);
  usePageTitle(user ? `${user.firstName} ${user.lastName}` : t('userDetail.pageTitleFallback'));
  const [isLoading, setIsLoading] = useState(true);
  // The LOAD effect's own failure is a flag, not the translated string —
  // translated at render, below, so a language switch after a failed load
  // shows the new language immediately rather than a stale snapshot (see
  // `docs/i18n/README.md`'s note on never putting `t` in a mount-effect's
  // dependency array). An action failure (save/toggle), by contrast, is set
  // fresh inside its own click handler, which always has the CURRENT `t` —
  // no flag needed there.
  const [hasLoadError, setHasLoadError] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const error = actionError ?? (hasLoadError ? t('userDetail.loadFailed') : null);
  // Which mutation is in flight, so only the pressed button shows its spinner
  // while both stay disabled to prevent concurrent edits.
  const [savingAction, setSavingAction] = useState<null | "save" | "toggle">(
    null,
  );
  const saving = savingAction !== null;

  // Editable fields
  const [role, setRole] = useState("");
  const [isActive, setIsActive] = useState(true);
  // Bumped by the retry button to re-run the fetch effect. The effect body is an
  // inline async IIFE that only setStates after an await, keeping it effect-safe.
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (!id) return;
    let active = true;
    (async () => {
      try {
        const data = await getUser(Number(id));
        if (!active) return;
        setUser(data);
        setRole(data.role);
        setIsActive(data.isActive);
        setHasLoadError(false);
      } catch {
        if (active) setHasLoadError(true);
      } finally {
        if (active) setIsLoading(false);
      }
    })();
    return () => {
      active = false;
    };
    // `t` deliberately excluded — see the `hasLoadError` comment above.
  }, [id, reloadKey]);

  const retry = () => {
    setIsLoading(true);
    setHasLoadError(false);
    setReloadKey((k) => k + 1);
  };

  const handleSave = async () => {
    if (!user) return;
    setSavingAction("save");
    setActionError(null);
    try {
      const updated = await updateUser(user.id, { role, isActive });
      setUser(updated);
      setRole(updated.role);
      setIsActive(updated.isActive);
      showToast({ message: t('userDetail.savedToast'), variant: "success" });
    } catch {
      setActionError(t('userDetail.saveFailed'));
    } finally {
      setSavingAction(null);
    }
  };

  const handleToggleActive = async () => {
    if (!user) return;
    setSavingAction("toggle");
    setActionError(null);
    try {
      const newActive = !user.isActive;
      const updated = await updateUser(user.id, { isActive: newActive });
      setUser(updated);
      setRole(updated.role);
      setIsActive(updated.isActive);
      showToast({
        message: newActive ? t('userDetail.reactivatedToast') : t('userDetail.deactivatedToast'),
        variant: "success",
      });
    } catch {
      setActionError(t('userDetail.statusToggleFailed'));
    } finally {
      setSavingAction(null);
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label={t('userDetail.loading')} />
      </div>
    );
  }

  if (error && !user) {
    return (
      <div>
        <Notice variant="error" title={error}>
          <Button
            variant="secondary"
            size="sm"
            onClick={retry}
            className="mt-3"
          >
            {t('common:ui.tryAgain')}
          </Button>
        </Notice>
        <Link
          to="/admin/users"
          className="inline-flex items-center gap-1.5 text-sm text-brand-teal-500 hover:text-brand-teal-600 mt-4"
        >
          <ArrowLeft size={14} strokeWidth={1.8} aria-hidden="true" />
          {t('userDetail.backToUsers')}
        </Link>
      </div>
    );
  }

  if (!user) return null;

  return (
    <PageLayout
      title={`${user.firstName} ${user.lastName}`}
      breadcrumb={[
        { label: t('users.pageTitle'), to: "/admin/users" },
        { label: `${user.firstName} ${user.lastName}` },
      ]}
    >
      {error && <Notice variant="error" title={error} />}

      <DetailLayout
        main={
          <Card>
            <h2 className="text-sm font-medium text-brand-slate-800 mb-4">
              {t('userDetail.editUser')}
            </h2>
            <div className="space-y-4">
              <Select
                label={t('userDetail.roleLabel')}
                value={role}
                onChange={(e) => setRole((e.target as HTMLSelectElement).value)}
                data-testid="admin-user-role"
              >
                <option value="User">{t('userDetail.roleUser')}</option>
                <option value="Admin">{t('userDetail.roleAdmin')}</option>
              </Select>

              <div className="flex items-center gap-3">
                <span className="text-[13px] font-medium text-brand-slate-600">
                  {t('userDetail.activeLabel')}
                </span>
                <button
                  type="button"
                  role="switch"
                  aria-checked={isActive}
                  aria-label={t('userDetail.activeLabel')}
                  onClick={() => setIsActive(!isActive)}
                  data-testid="admin-user-active"
                  className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors ${
                    isActive ? "bg-brand-teal-500" : "bg-brand-slate-300"
                  }`}
                >
                  <span
                    className={`pointer-events-none inline-block h-4 w-4 rounded-full bg-white shadow transform transition-transform ${
                      isActive ? "translate-x-4" : "translate-x-0"
                    }`}
                  />
                </button>
              </div>

              <div className="pt-2">
                <Button
                  onClick={handleSave}
                  loading={savingAction === "save"}
                  disabled={saving}
                  data-testid="admin-user-save"
                >
                  {t('userDetail.saveChanges')}
                </Button>
              </div>
            </div>
          </Card>
        }
        sidebar={
          <>
            <Card>
              <h2 className="text-sm font-medium text-brand-slate-800 mb-4">
                {t('userDetail.details')}
              </h2>
              <div className="space-y-4">
                <InfoRow label={t('common.column.email')} value={user.email} />
                <InfoRow label={t('userDetail.stateLabel')} value={user.state ?? t('common:ui.notSet')} />
                <InfoRow
                  label={t('common.column.status')}
                  value={
                    <Badge variant={user.isActive ? "success" : "error"}>
                      {user.isActive ? t('common.status.active') : t('common.status.inactive')}
                    </Badge>
                  }
                />
                <InfoRow
                  label={t('common.column.joined')}
                  value={formatDate(user.createdAt)}
                />
              </div>
            </Card>

            <Card>
              <h2 className="text-sm font-medium text-brand-slate-800 mb-3">
                {t('userDetail.actions')}
              </h2>
              {user.isActive ? (
                <Button
                  variant="danger"
                  className="w-full"
                  onClick={handleToggleActive}
                  loading={savingAction === "toggle"}
                  disabled={saving}
                >
                  {t('userDetail.deactivate')}
                </Button>
              ) : (
                <Button
                  variant="secondary"
                  className="w-full"
                  onClick={handleToggleActive}
                  loading={savingAction === "toggle"}
                  disabled={saving}
                >
                  {t('userDetail.reactivate')}
                </Button>
              )}
            </Card>
          </>
        }
      />
    </PageLayout>
  );
}

function InfoRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-[13px] text-brand-slate-500">{label}</span>
      <span className="text-sm text-brand-slate-800">{value}</span>
    </div>
  );
}
