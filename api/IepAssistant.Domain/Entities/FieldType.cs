namespace IepAssistant.Domain.Entities;

/// <summary>
/// The palette of primitive field types an admin can place on a <see cref="TemplateSection"/>
/// (State Document Template Engine). Each type is bound to render logic in the React editor and the
/// QuestPDF composer, so the set is a code enum (the field <em>content</em> is admin-authored; the
/// <em>palette</em> is code). Serialized as a string (JsonStringEnumConverter) and stored as a string
/// column via HasConversion&lt;string&gt;().
///
/// <para><see cref="Table"/> is a repeating group whose columns are themselves typed fields — but a
/// column may only be a non-<see cref="Table"/>, non-<see cref="RichText"/> type (see the per-type
/// config validation in the authoring service).</para>
/// </summary>
public enum FieldType
{
    Text = 0,
    RichText = 1,
    Date = 2,
    Select = 3,
    Checkbox = 4,
    Table = 5
}
