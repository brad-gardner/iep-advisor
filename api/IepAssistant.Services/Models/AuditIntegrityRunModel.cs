namespace IepAssistant.Services.Models;

public class AuditIntegrityRunModel
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RowsChecked { get; set; }
    public int? FirstBrokenId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Detail { get; set; }
}
