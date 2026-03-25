namespace PaycheckCalc.Core.Models;

public sealed class PaycheckResult
{
    public decimal GrossPay { get; init; }
    public decimal PreTaxDeductions { get; init; }
    public decimal PostTaxDeductions { get; init; }

    public UsState State { get; init; }
    public decimal StateTaxableWages { get; init; }
    public decimal StateWithholding { get; init; }
    public decimal StateDisabilityInsurance { get; init; }
    public string StateDisabilityInsuranceLabel { get; init; } = "State Disability Insurance";

    public decimal SocialSecurityWithholding { get; init; }
    public decimal MedicareWithholding { get; init; }
    public decimal AdditionalMedicareWithholding { get; init; }

    public decimal FederalTaxableIncome { get; init; }
    public decimal FederalWithholding { get; init; }

    public decimal TotalTaxes => StateWithholding + StateDisabilityInsurance + SocialSecurityWithholding + MedicareWithholding + AdditionalMedicareWithholding + FederalWithholding;
    public decimal NetPay { get; init; }
}
