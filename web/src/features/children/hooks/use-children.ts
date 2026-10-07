import { useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type { ChildProfile } from "@/types/api";
import { getChildren } from "../api/children-api";

export function useChildren() {
  const { t } = useTranslation("children");
  const [children, setChildren] = useState<ChildProfile[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  // A flag, not the translated string — translated at render, below, so a
  // language switch after a failed load shows the new language immediately
  // rather than a stale snapshot (phase 2 review). This also lets `load`
  // drop `t` from its own dependencies, so a language switch mid-mount never
  // re-triggers a redundant refetch.
  const [hasError, setHasError] = useState(false);
  // Guards against a slower, superseded call (e.g. unmount, or a fast
  // `reload()` double-click) applying its result after a newer one already
  // has — same idiom as `useHome`'s `active` flag.
  const activeRef = useRef(true);

  const load = useCallback(async () => {
    activeRef.current = true;
    setIsLoading(true);
    setHasError(false);
    try {
      const response = await getChildren();
      if (!activeRef.current) return;
      if (response.success && response.data) {
        setChildren(response.data);
      }
    } catch {
      if (activeRef.current) setHasError(true);
    } finally {
      if (activeRef.current) setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
    return () => {
      activeRef.current = false;
    };
  }, [load]);

  return { children, isLoading, error: hasError ? t("errors.loadFailed") : null, reload: load };
}
