using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

/// <summary>An active IEP team member with their functional role and effective permission tier.</summary>
public class StudentTeamMemberModel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int StaffProfileId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string OrgRoleName { get; set; } = string.Empty;
    public TeamRole TeamRole { get; set; }
    public bool IsLead { get; set; }

    /// <summary>Effective <c>SchoolStudentAccess</c> role (Viewer when no active access row exists).</summary>
    public AccessRole AccessRole { get; set; }
    public bool IsActive { get; set; }
    public DateTime AddedAt { get; set; }
}

public class AddTeamMemberModel
{
    public int StaffProfileId { get; set; }
    public TeamRole TeamRole { get; set; } = TeamRole.Other;
    public bool? IsLead { get; set; }

    /// <summary>Explicit permission override; null applies the role default.</summary>
    public AccessRole? AccessRole { get; set; }
}

public class UpdateTeamMemberModel
{
    public TeamRole? TeamRole { get; set; }
    public AccessRole? AccessRole { get; set; }
}
