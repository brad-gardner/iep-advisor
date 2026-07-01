namespace IepAssistant.Domain.Entities;

/// <summary>
/// A staff member's org membership (renamed from <c>TeacherProfile</c>, user decision). One row per
/// staff <see cref="User"/>. <see cref="OrgRoleId"/> determines DistrictAdmin/SchoolAdmin/Teacher
/// (see <c>OrgRoles</c> lookup + <c>OrgRoleIds</c> constants). <see cref="DistrictId"/> is required;
/// <see cref="SchoolId"/> is nullable — <c>null</c> means a DistrictAdmin not bound to a single school.
/// <see cref="IsActive"/> is the deactivation flag (deactivating also bumps the user's SecurityStamp).
/// </summary>
public class StaffProfile : BaseEntity, IAuditableEntity
{
    public int UserId { get; set; }
    public int DistrictId { get; set; }

    /// <summary>Nullable — <c>null</c> for a DistrictAdmin not scoped to a single school.</summary>
    public int? SchoolId { get; set; }

    public int OrgRoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Title { get; set; }
    public string? Credentials { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public User User { get; set; } = null!;
    public District District { get; set; } = null!;
    public School? School { get; set; }
    public OrgRole OrgRole { get; set; } = null!;
}
