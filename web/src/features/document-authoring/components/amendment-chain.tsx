import { Link } from 'react-router-dom';

interface AmendmentChainProps {
  studentId: number;
  amendsVersionId: number | null;
  amendsVersionNumber: number | null;
  amendedByVersionIds: number[];
  /** Version numbers for `amendedByVersionIds`, in the same order (when known). */
  amendedByVersionNumbers?: (number | null)[];
  'data-testid'?: string;
}

function versionHref(studentId: number, versionId: number): string {
  return `/educator/students/${studentId}/authored-versions/${versionId}`;
}

/**
 * The amendment chain for a finalized version (plan 7, decision 5): a link to
 * the version it amends, and links to any versions that amend it in turn.
 * Renders nothing when the version has no amendment relationships at all.
 */
export function AmendmentChain({
  studentId,
  amendsVersionId,
  amendsVersionNumber,
  amendedByVersionIds,
  amendedByVersionNumbers,
  'data-testid': testId,
}: AmendmentChainProps) {
  if (amendsVersionId == null && amendedByVersionIds.length === 0) return null;

  return (
    <div className="flex flex-wrap items-center gap-3 text-sm text-brand-slate-600" data-testid={testId}>
      {amendsVersionId != null && (
        <Link
          to={versionHref(studentId, amendsVersionId)}
          className="text-brand-teal-600 underline"
          data-testid={testId ? `${testId}-amends` : undefined}
        >
          Amends v{amendsVersionNumber ?? amendsVersionId}
        </Link>
      )}
      {amendedByVersionIds.length > 0 && (
        <span data-testid={testId ? `${testId}-amended-by` : undefined}>
          Amended by{' '}
          {amendedByVersionIds.map((id, i) => (
            <span key={id}>
              {i > 0 && ', '}
              <Link to={versionHref(studentId, id)} className="text-brand-teal-600 underline">
                v{amendedByVersionNumbers?.[i] ?? id}
              </Link>
            </span>
          ))}
        </span>
      )}
    </div>
  );
}
