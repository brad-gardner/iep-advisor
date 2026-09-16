import { formatDate } from '@/lib/format-date';
import { DueSoonSection } from './due-soon-section';
import { DraftsSection } from './drafts-section';
import { ProviderRequestsSection } from './provider-requests-section';
import { SharedDraftListSection } from './shared-draft-list-section';
import { ThisWeekSection } from './this-week-section';
import type { StaffHomeDto } from '../types';

/**
 * Case manager / provider / general-educator home body. The Provider variant
 * puts "what I owe" first (plan decision 2); every other variant keeps this
 * week → due soon → drafts → shared-with-family sections in that order.
 */
export function CaseloadHome({ staff }: { staff: StaffHomeDto }) {
  const thisWeek = (
    <ThisWeekSection
      key="this-week"
      title="This week"
      subtitle={`${formatDate(staff.weekStart)} – ${formatDate(staff.weekEnd)}`}
      meetings={staff.meetingsThisWeek}
    />
  );
  const dueSoon = <DueSoonSection key="due-soon" obligations={staff.obligations} />;
  const drafts = <DraftsSection key="drafts" drafts={staff.drafts} />;
  const sharedAwaitingFamily = (
    <SharedDraftListSection
      key="shared-awaiting-family"
      title="Shared drafts awaiting family"
      emptyHint="No drafts shared with families yet."
      items={staff.sharedDraftsAwaitingFamily}
      dateField="sharedAt"
      data-testid="home-shared-awaiting-family"
    />
  );
  const familyResponses = (
    <SharedDraftListSection
      key="family-responses"
      title="Family responses to review"
      emptyHint="No family responses waiting on your review."
      items={staff.familyResponsesToReview}
      dateField="respondedAt"
      data-testid="home-family-responses"
    />
  );
  const providerRequests = (
    <ProviderRequestsSection key="provider-requests" items={staff.providerRequestsIOwe} />
  );

  const sections =
    staff.variant === 'Provider'
      ? [providerRequests, thisWeek, dueSoon, drafts, sharedAwaitingFamily, familyResponses]
      : [thisWeek, dueSoon, drafts, sharedAwaitingFamily, familyResponses, providerRequests];

  return (
    <div className="space-y-6" data-testid="staff-home-caseload">
      {sections}
    </div>
  );
}
