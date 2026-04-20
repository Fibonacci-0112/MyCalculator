namespace PaycheckCalc.App.Models;

/// <summary>
/// Presentation model describing why a single paycheck line item
/// (FICA, federal, or state) has its value. Populated by the domain-to-
/// presentation mapper so the UI never binds to raw domain types.
/// </summary>
public sealed class LineItemExplanationModel
{
    public string Title { get; init; } = "";
    public string Method { get; init; } = "";
    public string? Table { get; init; }
    public IReadOnlyList<ExplanationInputModel> Inputs { get; init; } = Array.Empty<ExplanationInputModel>();
    public string? Note { get; init; }

    /// <summary>
    /// Flattened multi-line text suitable for a platform <c>DisplayAlert</c>
    /// or copy/paste. UI bindings can use this directly.
    /// </summary>
    public string DisplayText
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(Method);
            if (!string.IsNullOrWhiteSpace(Table))
            {
                sb.AppendLine();
                sb.Append("Table: ");
                sb.AppendLine(Table);
            }
            if (Inputs.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Key inputs:");
                foreach (var input in Inputs)
                {
                    sb.Append("• ");
                    sb.Append(input.Label);
                    sb.Append(": ");
                    sb.AppendLine(input.Value);
                }
            }
            if (!string.IsNullOrWhiteSpace(Note))
            {
                sb.AppendLine();
                sb.AppendLine(Note);
            }
            return sb.ToString().TrimEnd();
        }
    }
}

/// <summary>One labelled input in a <see cref="LineItemExplanationModel"/>.</summary>
public sealed class ExplanationInputModel
{
    public string Label { get; init; } = "";
    public string Value { get; init; } = "";
}
