using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Api.Dtos;

/// <summary>
/// Serializable projection of <see cref="StateFieldDefinition"/> used by
/// the Angular front end to dynamically render per-state input controls.
/// </summary>
public sealed class StateFieldDefinitionDto
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";

    /// <summary>Control type name, e.g. <c>"Text"</c>, <c>"Integer"</c>, <c>"Picker"</c>.</summary>
    public string FieldType { get; init; } = "";

    public bool IsRequired { get; init; }
    public object? DefaultValue { get; init; }
    public IReadOnlyList<string>? Options { get; init; }
}
