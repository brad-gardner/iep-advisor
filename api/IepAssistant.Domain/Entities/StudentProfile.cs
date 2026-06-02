namespace IepAssistant.Domain.Entities;

/// <summary>
/// P7a. The single side-table for a Student-role user. Keyed one-per-user (unique <see cref="UserId"/>),
/// it captures consent (<see cref="ConsentAcceptedAt"/>) plus forward-looking <see cref="StateCode"/>/<see cref="DateOfBirth"/>
/// (age-of-majority hook, no per-state logic now). Because the profile is keyed to the accepting user, a
/// parent-initiated invite (<see cref="ChildProfileId"/>) and an educator-initiated invite
/// (<see cref="SchoolStudentId"/>) accepted by the SAME student both converge onto this one row — the student
/// is linked to at most one ChildProfile + one SchoolStudent ("exactly one pair").
/// </summary>
public class StudentProfile : BaseEntity, IAuditableEntity
{
    public int UserId { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? StateCode { get; set; }
    public DateTime? ConsentAcceptedAt { get; set; }

    // Parent-side link (set when a parent-initiated invite is accepted).
    public int? ChildProfileId { get; set; }

    // School-side link (set when an educator-initiated invite is accepted).
    public int? SchoolStudentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public User User { get; set; } = null!;
}
