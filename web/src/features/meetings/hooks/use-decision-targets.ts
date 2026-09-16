import { useEffect, useState } from 'react';
import { getDocument } from '@/features/document-authoring/api/documents-api';
import { buildDecisionTargets, type DecisionTargetOption } from '../lib/decision-targets';

/** Goals/services row options from a meeting's linked draft, for the decision
 *  target picker. Empty (not an error) when there is no linked draft, the
 *  draft has no card-row fields, or the load fails — the picker degrades to
 *  free-text in every case. */
export function useDecisionTargets(documentInstanceId: number | null): DecisionTargetOption[] {
  const [options, setOptions] = useState<DecisionTargetOption[]>([]);

  useEffect(() => {
    let active = true;
    (async () => {
      if (!documentInstanceId) {
        if (active) setOptions([]);
        return;
      }
      try {
        const res = await getDocument(documentInstanceId);
        if (active) setOptions(res.success && res.data ? buildDecisionTargets(res.data) : []);
      } catch {
        if (active) setOptions([]);
      }
    })();
    return () => {
      active = false;
    };
  }, [documentInstanceId]);

  return options;
}
