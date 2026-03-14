namespace IepAssistant.Api.DTOs.MeetingPrep;

public class CheckItemDto
{
    public string Section { get; set; } = string.Empty;
    public int Index { get; set; }
    public bool IsChecked { get; set; }
}
