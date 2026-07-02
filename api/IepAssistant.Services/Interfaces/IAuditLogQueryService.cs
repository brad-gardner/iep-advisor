using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Read-only, paged, filterable query over the append-only <c>AccessAuditLog</c> for the district
/// audit-log viewer (Phase 2). This is a NEW read path and deliberately stays out of the write-only
/// <c>AuditLogger</c> channel — viewing the audit log is itself not audited (pilot decision).
///
/// Authorization is resolved per-request from the caller's active <see cref="StaffContext"/>
/// (never from JWT claims). Entries are actor-scoped: only rows whose <c>ActorUserId</c> maps to a
/// <c>StaffProfile</c> in the caller's district are returned, INCLUDING deactivated staff (their
/// history is what a FERPA reviewer asks for). A SchoolAdmin sees only own-school actors; a Teacher,
/// parent, or student is denied.
/// </summary>
public interface IAuditLogQueryService
{
    /// <summary>
    /// Returns a keyset page of enriched audit entries for the caller's scope. Teacher / null-context /
    /// non-staff callers get a permission failure (403). Invalid action, non-positive page size, or a
    /// negative cursor yield a bad-request failure (400).
    /// </summary>
    Task<ServiceResult<AuditLogPageModel>> QueryAsync(int userId, AuditLogQuery filters, CancellationToken ct = default);
}
