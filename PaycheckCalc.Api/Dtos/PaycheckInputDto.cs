using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;

namespace PaycheckCalc.Api.Dtos;

/// <summary>
/// Request body for <c>POST /api/paycheck/calculate</c>. Mirrors
/// <see cref="Core.Models.PaycheckInput"/> but is expressed as a
/// simple DTO so JSON input from the Angular front end deserializes
/// predictably (enums by name, decimals as numbers).
/// </summary>
public sealed class PaycheckInputDto
{
    public PayFrequency Frequency { get; init; } = PayFrequency.Biweekly;

    public decimal HourlyRate { get; init; }
    public decimal RegularHours { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal OvertimeMultiplier { get; init; } = 1.5m;

    public UsState State { get; init; } = UsState.OK;

    /// <summary>
    /// State-specific input values keyed by the field keys returned from
    /// <c>GET /api/reference/states/{state}/schema</c>. Values are passed
    /// through to <see cref="Core.Tax.State.StateInputValues"/> which
    /// performs safe numeric/boolean conversions.
    /// </summary>
    public Dictionary<string, object?>? StateInputValues { get; init; }

    /// <summary>Optional locality code for the employee's home jurisdiction.</summary>
    public string? HomeLocalityCode { get; init; }

    /// <summary>Optional locality code for the work jurisdiction.</summary>
    public string? WorkLocalityCode { get; init; }

    /// <summary>Locality-specific input values (same bag semantics as state values).</summary>
    public Dictionary<string, object?>? LocalInputValues { get; init; }

    public FederalW4Dto FederalW4 { get; init; } = new();

    public IReadOnlyList<DeductionDto> Deductions { get; init; } = Array.Empty<DeductionDto>();

    public decimal YtdSocialSecurityWages { get; init; }
    public decimal YtdMedicareWages { get; init; }

    public int PaycheckNumber { get; init; } = 1;
}

/// <summary>DTO mirror of <see cref="FederalW4Input"/>.</summary>
public sealed class FederalW4Dto
{
    public FederalFilingStatus FilingStatus { get; init; } = FederalFilingStatus.SingleOrMarriedSeparately;
    public bool Step2Checked { get; init; }
    public decimal Step3TaxCredits { get; init; }
    public decimal Step4aOtherIncome { get; init; }
    public decimal Step4bDeductions { get; init; }
    public decimal Step4cExtraWithholding { get; init; }
}

/// <summary>DTO mirror of <see cref="Deduction"/>.</summary>
public sealed class DeductionDto
{
    public string Name { get; init; } = "";
    public DeductionType Type { get; init; }
    public decimal Amount { get; init; }
    public DeductionAmountType AmountType { get; init; } = DeductionAmountType.Dollar;
    public bool ReducesStateTaxableWages { get; init; } = true;
}
