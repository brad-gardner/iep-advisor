namespace IepAssistant.Domain.Entities;

public enum AuditAction
{
    View = 0,
    Edit = 1,
    Share = 2,
    Export = 3,
    Finalize = 4,
    // Admin published/forked a document template version — a governance action that determines the
    // schema every future student document of that (state, type) is pinned to. Templates carry no
    // student PII, but the action is recorded for a tamper-evident authoring trail (cross-cutting G-e.4).
    Publish = 5
}
