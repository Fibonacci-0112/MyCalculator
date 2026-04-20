namespace PaycheckCalc.Core.Models;

/// <summary>
/// UI-agnostic description of <i>why</i> a single line item on a paycheck
/// (FICA Social Security, Medicare, federal income tax, state income tax, etc.)
/// has the value it has. Produced by the tax engine so the UI never has to
/// re-derive tax knowledge to render "how was this number computed?" drill-downs.
/// </summary>
/// <param name="Title">Short human-readable title for the line item (e.g., "Federal Income Tax").</param>
/// <param name="Method">Name of the method or formula used (e.g., "IRS Pub 15-T Annual Percentage Method, Worksheet 1A").</param>
/// <param name="Table">Optional table identifier (e.g., "Biweekly Single/MFS" or "DE 44 Method B").</param>
/// <param name="Inputs">Ordered list of key inputs that drove the computation.</param>
/// <param name="Note">Optional short note or caveat (e.g., "Exempt — no tax due").</param>
public sealed record LineItemExplanation(
    string Title,
    string Method,
    string? Table,
    IReadOnlyList<ExplanationInput> Inputs,
    string? Note = null);

/// <summary>
/// One labelled input that contributed to a <see cref="LineItemExplanation"/>.
/// Values are pre-formatted strings so presentation layers don't have to
/// interpret domain types (e.g., "Biweekly", "$2,000.00", "Single").
/// </summary>
/// <param name="Label">Display label (e.g., "Filing Status").</param>
/// <param name="Value">Display value (e.g., "Married Filing Jointly").</param>
public sealed record ExplanationInput(string Label, string Value);
