namespace IepAssistant.Domain.Entities;

/// <summary>Lifecycle state of a <see cref="SchoolStudent"/> (plan 3, decision 3). Stored as a string.
/// <c>SchoolStudent.IsActive</c> is kept in sync (<c>Status == Active</c>) for existing callers.</summary>
public enum StudentStatus
{
    Active,
    Exited,
    Archived
}
