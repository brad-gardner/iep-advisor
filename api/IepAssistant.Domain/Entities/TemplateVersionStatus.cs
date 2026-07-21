namespace IepAssistant.Domain.Entities;

/// <summary>
/// Lifecycle of a <see cref="DocumentTemplateVersion"/> (State Document Template Engine).
/// A template always has exactly one <see cref="Draft"/> working copy; publishing freezes it
/// into an immutable <see cref="Published"/> version. Serialized as a string
/// (JsonStringEnumConverter) and stored as a string column via HasConversion&lt;string&gt;().
/// </summary>
public enum TemplateVersionStatus
{
    Draft = 0,
    Published = 1
}
