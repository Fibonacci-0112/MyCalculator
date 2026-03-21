using System.Text.Json.Serialization;

namespace PaycheckCalc.Core.Tax.California;

internal sealed class CaliforniaMethodBData
{
    [JsonPropertyName("lowIncomeExemption")]
    public Dictionary<string, LowHighThreshold> LowIncomeExemption { get; set; } = new();

    [JsonPropertyName("estimatedDeductionPerAllowance")]
    public Dictionary<string, decimal> EstimatedDeductionPerAllowance { get; set; } = new();

    [JsonPropertyName("standardDeduction")]
    public Dictionary<string, LowHighThreshold> StandardDeduction { get; set; } = new();

    [JsonPropertyName("exemptionAllowanceCreditPerAllowance")]
    public Dictionary<string, decimal> ExemptionAllowanceCreditPerAllowance { get; set; } = new();

    [JsonPropertyName("annualBrackets")]
    public BracketsByFilingStatus AnnualBrackets { get; set; } = new();
}

internal sealed class LowHighThreshold
{
    [JsonPropertyName("low")]
    public decimal Low { get; set; }

    [JsonPropertyName("high")]
    public decimal High { get; set; }
}

internal sealed class BracketsByFilingStatus
{
    [JsonPropertyName("single")]
    public List<CaliforniaBracket> Single { get; set; } = new();

    [JsonPropertyName("married")]
    public List<CaliforniaBracket> Married { get; set; } = new();

    [JsonPropertyName("headOfHousehold")]
    public List<CaliforniaBracket> HeadOfHousehold { get; set; } = new();
}

internal sealed class CaliforniaBracket
{
    [JsonPropertyName("over")]
    public decimal Over { get; set; }

    [JsonPropertyName("notOver")]
    public decimal? NotOver { get; set; }

    [JsonPropertyName("rate")]
    public decimal Rate { get; set; }
}
