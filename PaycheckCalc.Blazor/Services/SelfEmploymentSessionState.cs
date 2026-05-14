using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Blazor.Services;

/// <summary>
/// Per-circuit session that mirrors <see cref="CalculatorSessionState"/> for
/// the Self-Employment flow. Holds the in-flight <see cref="SelfEmploymentInput"/>
/// fields and the most recent <see cref="SelfEmploymentResult"/> so the SE Inputs
/// and SE Results pages can share data across navigations within a Blazor circuit.
/// </summary>
public sealed class SelfEmploymentSessionState
{
    // ── Schedule C ──────────────────────────────────────────────────────────
    public decimal GrossRevenue { get; set; }
    public decimal CostOfGoodsSold { get; set; }
    public decimal TotalBusinessExpenses { get; set; }

    // ── Other income & W-2 coordination ─────────────────────────────────────
    public decimal OtherIncome { get; set; }
    public decimal W2SocialSecurityWages { get; set; }
    public decimal W2MedicareWages { get; set; }

    // ── Federal filing ──────────────────────────────────────────────────────
    public FederalFilingStatus FilingStatus { get; set; } =
        FederalFilingStatus.SingleOrMarriedSeparately;

    // ── State ───────────────────────────────────────────────────────────────
    public UsState State { get; set; } = UsState.TX;
    public StateInputValues StateInputValues { get; set; } = new();

    // ── Deductions ──────────────────────────────────────────────────────────
    public decimal ItemizedDeductionsOverStandard { get; set; }

    // ── QBI ─────────────────────────────────────────────────────────────────
    public bool IsSpecifiedServiceBusiness { get; set; }
    public decimal QualifiedBusinessW2Wages { get; set; }
    public decimal QualifiedPropertyUbia { get; set; }

    // ── Estimated payments ──────────────────────────────────────────────────
    public decimal EstimatedTaxPayments { get; set; }

    // ── Last result ─────────────────────────────────────────────────────────
    public SelfEmploymentResult? LastResult { get; set; }

    /// <summary>
    /// Build a <see cref="SelfEmploymentInput"/> from the current session values.
    /// </summary>
    public SelfEmploymentInput BuildInput() => new()
    {
        GrossRevenue                  = GrossRevenue,
        CostOfGoodsSold               = CostOfGoodsSold,
        TotalBusinessExpenses         = TotalBusinessExpenses,
        OtherIncome                   = OtherIncome,
        W2SocialSecurityWages         = W2SocialSecurityWages,
        W2MedicareWages               = W2MedicareWages,
        FilingStatus                  = FilingStatus,
        State                         = State,
        StateInputValues              = StateInputValues.Count == 0 ? null : StateInputValues,
        ItemizedDeductionsOverStandard = ItemizedDeductionsOverStandard,
        IsSpecifiedServiceBusiness    = IsSpecifiedServiceBusiness,
        QualifiedBusinessW2Wages      = QualifiedBusinessW2Wages,
        QualifiedPropertyUbia         = QualifiedPropertyUbia,
        EstimatedTaxPayments          = EstimatedTaxPayments,
    };
}
