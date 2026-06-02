namespace IepAssistant.Services.Models;

/// <summary>
/// A parent<->school ChildLink, returned to both educator and parent flows.
/// </summary>
public class ChildLinkModel
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public int? ChildProfileId { get; set; }
    public string? InviteEmail { get; set; }
    public bool IsActive { get; set; }
    public bool IsAccepted { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? LinkedAt { get; set; }
    public DateTime? InviteExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// One of the parent's own (Owner) ChildProfiles offered as a "link existing" candidate
/// on the invite-preview screen.
/// </summary>
public class LinkableChildModel
{
    public int ChildProfileId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
}

/// <summary>
/// Preview shown to the invited parent before they accept: the school student they'd be
/// linking to plus their existing owned children as "link existing" candidates.
/// </summary>
public class ChildLinkInvitePreviewModel
{
    public int SchoolStudentId { get; set; }
    public string StudentFirstName { get; set; } = string.Empty;
    public string? StudentLastName { get; set; }
    public string? SchoolName { get; set; }
    public List<LinkableChildModel> ExistingChildren { get; set; } = new();
}
