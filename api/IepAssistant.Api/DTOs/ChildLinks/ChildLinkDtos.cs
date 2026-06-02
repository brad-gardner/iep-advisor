using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.ChildLinks;

public class AcceptChildLinkRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    /// <summary>If set, link to an existing child the parent owns; otherwise a new child is created.</summary>
    public int? LinkToChildProfileId { get; set; }
}

public class LinkableChildDto
{
    public int ChildProfileId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
}

public class ChildLinkInvitePreviewDto
{
    public int SchoolStudentId { get; set; }
    public string StudentFirstName { get; set; } = string.Empty;
    public string? StudentLastName { get; set; }
    public string? SchoolName { get; set; }
    public List<LinkableChildDto> ExistingChildren { get; set; } = new();
}

public class AcceptedChildLinkDto
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public int? ChildProfileId { get; set; }
    public bool IsAccepted { get; set; }
    public DateTime? LinkedAt { get; set; }
}
