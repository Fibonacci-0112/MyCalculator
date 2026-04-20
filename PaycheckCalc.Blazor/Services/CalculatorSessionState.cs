using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Blazor.Services;

/// <summary>
/// Per-circuit session that holds the current Inputs form state and the most
/// recent <see cref="PaycheckResult"/>. Registered as <c>Scoped</c> so the
/// Inputs and Results pages share the same instance within a user's Blazor
/// circuit, similar to how the MAUI app's <c>CalculatorViewModel</c> is
/// shared across the Inputs and Results pages.
/// </summary>
public sealed class CalculatorSessionState
{
    // ── Pay &amp; hours ───────────────────────────────────────────────────────
    public decimal HourlyRate { get; set; } = 25m;
    public decimal RegularHours { get; set; } = 80m;
    public decimal OvertimeHours { get; set; }
    public decimal OvertimeMultiplier { get; set; } = 1.5m;
    public PayFrequency Frequency { get; set; } = PayFrequency.Biweekly;
    public int PaycheckNumber { get; set; } = 1;

    // ── Federal W-4 ─────────────────────────────────────────────────────────
    public FederalFilingStatus FederalFilingStatus { get; set; } =
        FederalFilingStatus.SingleOrMarriedSeparately;
    public bool FederalStep2Checked { get; set; }
    public decimal FederalStep3Credits { get; set; }
    public decimal FederalStep4aOtherIncome { get; set; }
    public decimal FederalStep4bDeductions { get; set; }
    public decimal FederalStep4cExtraWithholding { get; set; }

    // ── State ───────────────────────────────────────────────────────────────
    public UsState State { get; set; } = UsState.TX;

    /// <summary>
    /// Schema-driven state input values. Keys match
    /// <see cref="StateFieldDefinition.Key"/> from the selected state's
    /// calculator; values are primitives (string/decimal/int/bool) matching
    /// the field type.
    /// </summary>
    public StateInputValues StateInputValues { get; set; } = new();

    // ── Deductions ──────────────────────────────────────────────────────────
    public List<DeductionEntry> Deductions { get; } = new();

    // ── Last result ─────────────────────────────────────────────────────────
    public PaycheckResult? LastResult { get; set; }

    /// <summary>
    /// Build a <see cref="PaycheckInput"/> from the current session values.
    /// </summary>
    public PaycheckInput BuildInput() => new()
    {
        HourlyRate         = HourlyRate,
        RegularHours       = RegularHours,
        OvertimeHours      = OvertimeHours,
        OvertimeMultiplier = OvertimeMultiplier,
        Frequency          = Frequency,
        PaycheckNumber     = PaycheckNumber <= 0 ? 1 : PaycheckNumber,
        State              = State,
        StateInputValues   = StateInputValues.Count == 0 ? null : StateInputValues,
        FederalW4 = new FederalW4Input
        {
            FilingStatus           = FederalFilingStatus,
            Step2Checked           = FederalStep2Checked,
            Step3TaxCredits        = FederalStep3Credits,
            Step4aOtherIncome      = FederalStep4aOtherIncome,
            Step4bDeductions       = FederalStep4bDeductions,
            Step4cExtraWithholding = FederalStep4cExtraWithholding,
        },
        Deductions = Deductions
            .Where(d => d.Amount > 0)
            .Select(d => d.ToDomain())
            .ToList(),
    };
}

/// <summary>
/// UI row for a single deduction on the Deductions tab. Converted to the
/// immutable <see cref="Deduction"/> domain model when calculating.
/// </summary>
public sealed class DeductionEntry
{
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public DeductionAmountType AmountType { get; set; } = DeductionAmountType.Dollar;
    public DeductionType Type { get; set; } = DeductionType.PreTax;
    public bool ReducesStateTaxableWages { get; set; } = true;

    public Deduction ToDomain() => new()
    {
        Name                     = Name,
        Amount                   = Amount,
        AmountType               = AmountType,
        Type                     = Type,
        ReducesStateTaxableWages = ReducesStateTaxableWages,
    };
}
