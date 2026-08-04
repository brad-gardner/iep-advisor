using System.Text.Json;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Coverage for <see cref="TemplateFieldConfigValidator"/> (State Document Template Engine): the
/// per-<see cref="FieldType"/> ConfigJson validation that runs on every field save and again at publish.
/// One branch per test: Text MaxLength bounds, Date format validity, malformed JSON, an unsupported
/// field type, and Table row-count bounds. A null return means valid.
/// </summary>
public sealed class TemplateFieldConfigValidatorTests
{
    private static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, TemplateFieldConfigValidator.JsonOptions);

    // ---------------------------------------------------------------- Text

    [Fact]
    public void Text_NegativeMaxLength_IsRejected()
    {
        var error = TemplateFieldConfigValidator.Validate(FieldType.Text, Serialize(new TextFieldConfig { MaxLength = -1 }));

        Assert.NotNull(error);
        Assert.Contains("0 or greater", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(500)]
    public void Text_NonNegativeMaxLength_IsValid(int maxLength)
    {
        Assert.Null(TemplateFieldConfigValidator.Validate(FieldType.Text, Serialize(new TextFieldConfig { MaxLength = maxLength })));
    }

    [Fact]
    public void Text_NoConfig_IsValid()
    {
        Assert.Null(TemplateFieldConfigValidator.Validate(FieldType.Text, null));
    }

    // ---------------------------------------------------------------- Date

    [Theory]
    [InlineData("yyyy-MM-dd")]
    [InlineData("MM/dd/yyyy")]
    [InlineData("MMMM d, yyyy")]
    public void Date_ValidFormat_IsValid(string format)
    {
        Assert.Null(TemplateFieldConfigValidator.Validate(FieldType.Date, Serialize(new DateFieldConfig { Format = format })));
    }

    [Fact]
    public void Date_InvalidFormat_IsRejected()
    {
        // A lone trailing escape backslash is not a valid custom date format (throws FormatException).
        var error = TemplateFieldConfigValidator.Validate(FieldType.Date, Serialize(new DateFieldConfig { Format = "\\" }));

        Assert.NotNull(error);
        Assert.Contains("not a valid date format", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Date_NoFormat_IsValid()
    {
        Assert.Null(TemplateFieldConfigValidator.Validate(FieldType.Date, Serialize(new DateFieldConfig { Format = null })));
    }

    // ---------------------------------------------------------------- Malformed JSON

    [Theory]
    [InlineData(FieldType.Text)]
    [InlineData(FieldType.Date)]
    [InlineData(FieldType.Select)]
    [InlineData(FieldType.Table)]
    public void MalformedJson_IsRejected(FieldType type)
    {
        var error = TemplateFieldConfigValidator.Validate(type, "{ not valid json ");

        Assert.NotNull(error);
        Assert.Contains("not valid JSON", error!, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- Unsupported field type

    [Fact]
    public void UnsupportedFieldType_IsRejected()
    {
        // An out-of-range enum value hits the switch's default arm.
        var error = TemplateFieldConfigValidator.Validate((FieldType)999, null);

        Assert.NotNull(error);
        Assert.Contains("Unsupported field type", error!, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- Table row bounds

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(0, -1)]
    public void Table_NegativeRowBounds_IsRejected(int minRows, int maxRows)
    {
        var config = new TableFieldConfig
        {
            Columns = { new TableColumn { ColumnKey = Guid.NewGuid(), Type = FieldType.Text, Label = "Service" } },
            MinRows = minRows,
            MaxRows = maxRows
        };

        var error = TemplateFieldConfigValidator.Validate(FieldType.Table, Serialize(config));

        Assert.NotNull(error);
        Assert.Contains("0 or greater", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Table_ValidConfig_IsValid()
    {
        var config = new TableFieldConfig
        {
            Columns = { new TableColumn { ColumnKey = Guid.NewGuid(), Type = FieldType.Text, Label = "Service" } },
            MinRows = 1,
            MaxRows = 5
        };

        Assert.Null(TemplateFieldConfigValidator.Validate(FieldType.Table, Serialize(config)));
    }
}
