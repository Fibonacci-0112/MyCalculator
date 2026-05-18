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
    /// Replace the current session values with the data from a previously
    /// saved <see cref="PaycheckInput"/> (e.g. when loading a saved paycheck
    /// from the Saved Paychecks page back into the calculator).
    /// </summary>
    public void LoadFromInput(PaycheckInput input)
    {
        HourlyRate         = input.HourlyRate;
        RegularHours       = input.RegularHours;
        OvertimeHours      = input.OvertimeHours;
        OvertimeMultiplier = input.OvertimeMultiplier;
        Frequency          = input.Frequency;
        PaycheckNumber     = input.PaycheckNumber <= 0 ? 1 : input.PaycheckNumber;
        State              = input.State;
        StateInputValues   = input.StateInputValues is null
            ? new StateInputValues()
            : new StateInputValues(input.StateInputValues);

        FederalFilingStatus           = input.FederalW4.FilingStatus;
        FederalStep2Checked           = input.FederalW4.Step2Checked;
        FederalStep3Credits           = input.FederalW4.Step3TaxCredits;
        FederalStep4aOtherIncome      = input.FederalW4.Step4aOtherIncome;
        FederalStep4bDeductions       = input.FederalW4.Step4bDeductions;
        FederalStep4cExtraWithholding = input.FederalW4.Step4cExtraWithholding;

        Deductions.Clear();
        foreach (var d in input.Deductions)
        {
            Deductions.Add(new DeductionEntry
            {
                Name                       = d.Name,
                Amount                     = d.Amount,
                AmountType                 = d.AmountType,
                Type                       = d.Type,
                ReducesFederalTaxableWages = d.ReducesFederalTaxableWages,
                ReducesStateTaxableWages   = d.ReducesStateTaxableWages,
                ReducesFicaWages           = d.ReducesFicaWages,
            });
        }
    }

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

    /// <summary>
    /// Clears all session fields back to defaults. Called by
    /// <c>SessionStateLifecycle</c> when the authenticated user changes
    /// inside a circuit so the previous user's W-4 / hourly rate / state
    /// fields can never leak into the next user's view.
    /// </summary>
    public void Reset()
    {
        HourlyRate                    = 25m;
        RegularHours                  = 80m;
        OvertimeHours                 = 0m;
        OvertimeMultiplier            = 1.5m;
        Frequency                     = PayFrequency.Biweekly;
        PaycheckNumber                = 1;
        FederalFilingStatus           = FederalFilingStatus.SingleOrMarriedSeparately;
        FederalStep2Checked           = false;
        FederalStep3Credits           = 0m;
        FederalStep4aOtherIncome      = 0m;
        FederalStep4bDeductions       = 0m;
        FederalStep4cExtraWithholding = 0m;
        State                         = UsState.TX;
        StateInputValues              = new StateInputValues();
        Deductions.Clear();
        LastResult                    = null;
    }
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
    public bool ReducesFederalTaxableWages { get; set; } = true;
    public bool ReducesStateTaxableWages { get; set; } = true;
    public bool ReducesFicaWages { get; set; } = true;

    public Deduction ToDomain() => new()
    {
        Name                       = Name,
        Amount                     = Amount,
        AmountType                 = AmountType,
        Type                       = Type,
        ReducesFederalTaxableWages = ReducesFederalTaxableWages,
        ReducesStateTaxableWages   = ReducesStateTaxableWages,
        ReducesFicaWages           = ReducesFicaWages,
    };
}
