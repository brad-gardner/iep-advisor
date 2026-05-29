import { useEffect, useState } from "react";
import { getConfig, type FeatureFlagMap } from "@/features/config/api/config-api";

// Module-level cache: the /api/config call happens at most once per page load.
// Subscribers are notified once the single in-flight request resolves.
let cachedFlags: FeatureFlagMap | null = null;
let inFlight: Promise<FeatureFlagMap> | null = null;
const subscribers = new Set<(flags: FeatureFlagMap) => void>();

function loadFlags(): Promise<FeatureFlagMap> {
  if (cachedFlags) return Promise.resolve(cachedFlags);
  if (inFlight) return inFlight;

  inFlight = getConfig()
    .then((res) => {
      cachedFlags = res.success && res.data ? res.data : {};
      subscribers.forEach((notify) => notify(cachedFlags!));
      return cachedFlags;
    })
    .catch(() => {
      cachedFlags = {};
      subscribers.forEach((notify) => notify(cachedFlags!));
      return cachedFlags;
    });

  return inFlight;
}

// Subscribes to the module-level flag cache and re-renders once loaded.
// Returns null until the single /api/config call resolves.
function useFlags(): FeatureFlagMap | null {
  // Seed from the cache at mount; if it's already loaded we never touch state.
  const [flags, setFlags] = useState<FeatureFlagMap | null>(() => cachedFlags);

  useEffect(() => {
    if (cachedFlags) return;

    let active = true;
    const notify = (next: FeatureFlagMap) => {
      if (active) setFlags(next);
    };
    subscribers.add(notify);
    loadFlags();

    return () => {
      active = false;
      subscribers.delete(notify);
    };
  }, []);

  return flags;
}

/**
 * Returns whether a named feature flag is enabled. Flags load lazily and are
 * cached at the module level, so the underlying /api/config call fires once.
 * Returns false until the flags have loaded.
 */
export function useFeatureFlag(name: string): boolean {
  return useFlags()?.[name] ?? false;
}

/**
 * Loading-aware variant for route guards: `loaded` is false until /api/config
 * resolves, so callers can avoid redirecting on a not-yet-known flag.
 */
export function useFeatureFlagStatus(name: string): {
  enabled: boolean;
  loaded: boolean;
} {
  const flags = useFlags();
  return { enabled: flags?.[name] ?? false, loaded: flags !== null };
}
