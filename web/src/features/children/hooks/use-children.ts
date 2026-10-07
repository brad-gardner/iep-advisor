import { useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { ChildProfile } from "@/types/api";
import { getChildren } from "../api/children-api";

export function useChildren() {
  const { t } = useTranslation("children");
  const [children, setChildren] = useState<ChildProfile[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await getChildren();
      if (response.success && response.data) {
        setChildren(response.data);
      }
    } catch {
      setError(t("errors.loadFailed"));
    } finally {
      setIsLoading(false);
    }
  }, [t]);

  useEffect(() => {
    load();
  }, [load]);

  return { children, isLoading, error, reload: load };
}
