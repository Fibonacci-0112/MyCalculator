namespace PaycheckCalc.Core.Tax.Fica;

public sealed class FicaCalculator
{
    public const decimal SocialSecurityRate = 0.062m;
    public const decimal MedicareRate = 0.0145m;
    public const decimal AdditionalMedicareRate = 0.009m;

    public decimal SocialSecurityWageBase { get; init; } = 184_500m;
    public decimal AdditionalMedicareEmployerThreshold { get; init; } = 200_000m;

    public (decimal ss, decimal medicare, decimal addlMedicare) Calculate(decimal medicareWagesThisPeriod, decimal ytdSsWages, decimal ytdMedicareWages)
    {
        var remainingSsBase = Math.Max(0m, SocialSecurityWageBase - ytdSsWages);
        var ssTaxable = Math.Min(medicareWagesThisPeriod, remainingSsBase);
        var ss = ssTaxable * SocialSecurityRate;

        var medicare = medicareWagesThisPeriod * MedicareRate;

        var prior = ytdMedicareWages;
        var current = ytdMedicareWages + medicareWagesThisPeriod;
        var over = Math.Max(0m, current - AdditionalMedicareEmployerThreshold) - Math.Max(0m, prior - AdditionalMedicareEmployerThreshold);
        var addl = over * AdditionalMedicareRate;

        return (ss, medicare, addl);
    }
}
