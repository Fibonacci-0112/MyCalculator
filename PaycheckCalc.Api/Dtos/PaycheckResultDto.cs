using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Api.Dtos;

/// <summary>
/// API-friendly projection of <see cref="PaycheckResult"/>. Uses plain
/// strings for enum values so the Angular client sees stable field names.
/// </summary>
public sealed class PaycheckResultDto
{
    public decimal GrossPay { get; init; }
    public decimal PreTaxDeductions { get; init; }
    public decimal PostTaxDeductions { get; init; }

    public string State { get; init; } = "";
    public decimal StateTaxableWages { get; init; }
    public decimal StateWithholding { get; init; }
    public decimal StateDisabilityInsurance { get; init; }
    public string StateDisabilityInsuranceLabel { get; init; } = "State Disability Insurance";

    public decimal SocialSecurityWithholding { get; init; }
    public decimal MedicareWithholding { get; init; }
    public decimal AdditionalMedicareWithholding { get; init; }

    public decimal FederalTaxableIncome { get; init; }
    public decimal FederalWithholding { get; init; }

    public decimal LocalTaxableWages { get; init; }
    public decimal LocalWithholding { get; init; }
    public decimal LocalHeadTax { get; init; }
    public string LocalHeadTaxLabel { get; init; } = "Local Services Tax";
    public string LocalityLabel { get; init; } = "";

    public IReadOnlyList<LocalBreakdownDto> LocalBreakdown { get; init; } = Array.Empty<LocalBreakdownDto>();

    public decimal TotalTaxes { get; init; }
    public decimal NetPay { get; init; }
}

public sealed record LocalBreakdownDto(
    string LocalityCode,
    string LocalityName,
    decimal TaxableWages,
    decimal Withholding,
    decimal HeadTax,
    string HeadTaxLabel,
    string? Description);
