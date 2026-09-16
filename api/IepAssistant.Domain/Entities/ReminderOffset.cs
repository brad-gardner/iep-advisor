namespace IepAssistant.Domain.Entities;

/// <summary>How far ahead of a meeting a <see cref="MeetingReminder"/> fires (plan 4, decision 6). Stored as a string.</summary>
public enum ReminderOffset
{
    SevenDays,
    OneDay,
    OneHour
}
