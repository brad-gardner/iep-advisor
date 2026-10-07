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
  // Counts every load attempt, including unmount, so a slower, superseded
  // call (e.g. a fast `reload()` double-click, or one still in flight at
  // unmount) can tell its response arrived after a newer attempt already
  // claimed the latest slot, and must not apply its own stale result.
  const reqRef = useRef(0);

  const load = useCallback(async () => {
    reqRef.current += 1;
    const id = reqRef.current;
    setIsLoading(true);
    setHasError(false);
    try {
      const response = await getChildren();
      if (id !== reqRef.current) return; // superseded — see reqRef
      if (response.success && response.data) {
        setChildren(response.data);
      }
    } catch {
      if (id === reqRef.current) setHasError(true);
    } finally {
      if (id === reqRef.current) setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
    return () => {
      reqRef.current += 1; // unmount supersedes any still-in-flight load — see reqRef
    };
  }, [load]);

  return { children, isLoading, error: hasError ? t("errors.loadFailed") : null, reload: load };
}
