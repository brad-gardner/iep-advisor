using System.Linq.Expressions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// EF-translatable "needs attention" predicates over <see cref="SchoolStudent"/> (plan 5, deliverable C).
/// The SAME <see cref="Expression{TDelegate}"/> objects back both the roster search filter
/// (<see cref="EducatorService.SearchStudentsAsync"/>) and the district compliance board
/// (<see cref="DistrictService"/>'s compliance/adoption/engagement reads) — via a plain
/// <c>IQueryable&lt;SchoolStudent&gt;.Where(...)</c> for the paged roster and a scoped
/// <c>IQueryable&lt;SchoolStudent&gt;.Count(...)</c> per school for the board — so a board count and its
/// roster drilldown can never numerically drift apart; they are the identical expression tree.
///
/// <para>Date-driven predicates resolve the SAME effective due date <see cref="ObligationRules"/> uses
/// (annual review = <see cref="SchoolStudent.AnnualReviewDueDate"/> or, when absent, IEP date + 365 days;
/// re-evaluation = <see cref="SchoolStudent.ReevaluationDueDate"/> or ETR date + 3 years), so a student's
/// board bucket always matches their <see cref="ObligationService"/> card. An unresolved effective date
/// is represented by the max/min DateTime sentinel so it never satisfies "overdue" or "due within N
/// days" — Unknown is never treated as healthy.</para>
/// </summary>
public static class StudentAttentionRules
{
    public static Expression<Func<SchoolStudent, bool>> OverdueAnnual(DateTime today) => s =>
        (s.AnnualReviewDueDate ?? (s.IepDate.HasValue ? s.IepDate.Value.AddDays(ObligationRules.AnnualReviewFallbackDays) : (DateTime?)null) ?? DateTime.MaxValue) < today;

    public static Expression<Func<SchoolStudent, bool>> OverdueReeval(DateTime today) => s =>
        (s.ReevaluationDueDate ?? (s.EtrDate.HasValue ? s.EtrDate.Value.AddYears(ObligationRules.ReevaluationFallbackYears) : (DateTime?)null) ?? DateTime.MaxValue) < today;

    /// <summary>
    /// The annual-review OR re-evaluation effective due date falls within [<paramref name="from"/>,
    /// <paramref name="to"/>] (inclusive of both ends; an unresolved date never matches). Cumulative by
    /// construction — call with a wider <paramref name="to"/> for a "due within 60 days" bucket that is
    /// naturally a superset of a "due within 30 days" one.
    /// </summary>
    public static Expression<Func<SchoolStudent, bool>> DueWithin(DateTime from, DateTime to) => s =>
        ((s.AnnualReviewDueDate ?? (s.IepDate.HasValue ? s.IepDate.Value.AddDays(ObligationRules.AnnualReviewFallbackDays) : (DateTime?)null) ?? DateTime.MinValue) >= from
            && (s.AnnualReviewDueDate ?? (s.IepDate.HasValue ? s.IepDate.Value.AddDays(ObligationRules.AnnualReviewFallbackDays) : (DateTime?)null) ?? DateTime.MinValue) <= to)
        ||
        ((s.ReevaluationDueDate ?? (s.EtrDate.HasValue ? s.EtrDate.Value.AddYears(ObligationRules.ReevaluationFallbackYears) : (DateTime?)null) ?? DateTime.MinValue) >= from
            && (s.ReevaluationDueDate ?? (s.EtrDate.HasValue ? s.EtrDate.Value.AddYears(ObligationRules.ReevaluationFallbackYears) : (DateTime?)null) ?? DateTime.MinValue) <= to);

    /// <summary>Either obligation's effective due date is unresolvable (no due date AND no fallback
    /// source date) — surfaced as "Unknown", never as healthy.</summary>
    public static Expression<Func<SchoolStudent, bool>> UnknownDates() => s =>
        (s.AnnualReviewDueDate == null && !s.IepDate.HasValue) || (s.ReevaluationDueDate == null && !s.EtrDate.HasValue);

    /// <summary>No active lead team member whose user still holds an ACTIVE StaffProfile in
    /// <paramref name="districtId"/> — a student whose lead was deactivated still counts (mirrors
    /// <see cref="DistrictService.GetDashboardAsync"/>'s StudentsWithoutStaff predicate).</summary>
    public static Expression<Func<SchoolStudent, bool>> NoLead(ApplicationDbContext context, int districtId) => s =>
        !context.StudentTeamMembers.Any(m => m.SchoolStudentId == s.Id && m.IsActive && m.IsLead
            && context.StaffProfiles.Any(p => p.UserId == m.UserId && p.IsActive && p.DistrictId == districtId));

    /// <summary>An active, accepted ChildLink bound to a ChildProfile exists for this student.</summary>
    public static Expression<Func<SchoolStudent, bool>> HasFamily(ApplicationDbContext context) => s =>
        context.ChildLinks.Any(l => l.SchoolStudentId == s.Id && l.IsActive && l.AcceptedAt != null && l.ChildProfileId != null);

    /// <summary>No active, accepted ChildLink bound to a ChildProfile.</summary>
    public static Expression<Func<SchoolStudent, bool>> NoFamily(ApplicationDbContext context) => s =>
        !context.ChildLinks.Any(l => l.SchoolStudentId == s.Id && l.IsActive && l.AcceptedAt != null && l.ChildProfileId != null);
}
