namespace IepAssistant.Api.DTOs.EtrDocuments;

public class EtrDocumentListItemDto : EtrDocumentDto
{
    public int ChildId { get; set; }
    public string ChildFirstName { get; set; } = string.Empty;
    public string? ChildLastName { get; set; }
}
