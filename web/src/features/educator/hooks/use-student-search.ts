import { useEffect, useMemo, useState } from 'react';
import { searchStudents } from '../api/educator-api';
import type { PagedResult, SchoolStudent, StudentSearchParams } from '../types';

const EMPTY_PAGE: PagedResult<SchoolStudent> = { items: [], total: 0, page: 1, pageSize: 50 };

interface UseStudentSearchResult {
  page: PagedResult<SchoolStudent>;
  isLoading: boolean;
  failed: boolean;
  // Re-run the current request (after a mutation).
  refresh: () => void;
}

// Server-driven roster fetch. Loading is *derived* — the last completed
// request's key differs from the wanted one — so no setState runs
// synchronously inside the effect and a stale response can never overwrite a
// newer one (each response is stamped with the key it answered).
export function useStudentSearch(params: StudentSearchParams): UseStudentSearchResult {
  const [refreshToken, setRefreshToken] = useState(0);
  const requestKey = useMemo(
    () => `${JSON.stringify(params)}#${refreshToken}`,
    [params, refreshToken]
  );
  const [loaded, setLoaded] = useState<{
    key: string;
    page: PagedResult<SchoolStudent>;
    failed: boolean;
  } | null>(null);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await searchStudents(params);
        if (!active) return;
        const ok = response.success && !!response.data;
        setLoaded({ key: requestKey, page: ok ? response.data! : EMPTY_PAGE, failed: !ok });
      } catch {
        if (active) setLoaded({ key: requestKey, page: EMPTY_PAGE, failed: true });
      }
    })();
    return () => {
      active = false;
    };
  }, [params, requestKey]);

  const isLoading = loaded?.key !== requestKey;
  return {
    page: loaded?.page ?? EMPTY_PAGE,
    isLoading,
    failed: !isLoading && Boolean(loaded?.failed),
    refresh: () => setRefreshToken((t) => t + 1),
  };
}
