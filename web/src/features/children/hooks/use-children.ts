import { useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type { ChildProfile } from "@/types/api";
import { type LoadError, loadErrorText } from "@/lib/api-error";
import { getChildren } from "../api/children-api";

export function useChildren() {
  const { t } = useTranslation("children");
  const [children, setChildren] = useState<ChildProfile[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  // A flag, not the translated string — translated at render, below, so a
  // language switch after a failed load shows the new language immediately
  // rather than a stale snapshot (phase 2 review). This also lets `load`
  // drop `t` from its own dependencies, so a language switch mid-mount never
  // re-triggers a redundant refetch. Always `{ kind: 'generic' }` on failure
  // here (never `toLoadError`) — a business-logic failure response carries no
  // server message check today (`response.success === false` is silently
  // ignored, same as before this swap), and a caught error's own message is
  // deliberately discarded, matching this hook's pre-existing behavior
  // exactly; only the shared `LoadError`/`loadErrorText` plumbing is new.
  const [loadError, setLoadError] = useState<LoadError | null>(null);
  // Counts every load attempt, including unmount, so a slower, superseded
  // call (e.g. a fast `reload()` double-click, or one still in flight at
  // unmount) can tell its response arrived after a newer attempt already
  // claimed the latest slot, and must not apply its own stale result.
  const reqRef = useRef(0);

  const load = useCallback(async () => {
    reqRef.current += 1;
    const id = reqRef.current;
    setIsLoading(true);
    setLoadError(null);
    try {
      const response = await getChildren();
      if (id !== reqRef.current) return; // superseded — see reqRef
      if (response.success && response.data) {
        setChildren(response.data);
      }
    } catch {
      if (id === reqRef.current) setLoadError({ kind: 'generic' });
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

  return { children, isLoading, error: loadErrorText(loadError, t("errors.loadFailed")), reload: load };
}
