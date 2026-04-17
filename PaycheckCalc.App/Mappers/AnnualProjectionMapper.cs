using PaycheckCalc.App.Models;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Maps a domain <see cref="AnnualProjection"/> to an
/// <see cref="AnnualProjectionModel"/> presentation model for the UI.
/// </summary>
public static class AnnualProjectionMapper
{
    public static AnnualProjectionModel Map(AnnualProjection projection)
    {
        return new AnnualProjectionModel
        {
            PayPeriodsPerYear = projection.PayPeriodsPerYear,
            CurrentPaycheckNumber = projection.CurrentPaycheckNumber,
            RemainingPaychecks = projection.RemainingPaychecks,

            AnnualizedGrossPay = projection.AnnualizedGrossPay,
            AnnualizedPreTaxDeductions = projection.AnnualizedPreTaxDeductions,
            AnnualizedPostTaxDeductions = projection.AnnualizedPostTaxDeductions,
            AnnualizedFederalTaxableWages = projection.AnnualizedFederalTaxableWages,
            AnnualizedFicaTaxableWages = projection.AnnualizedFicaTaxableWages,
            AnnualizedStateTaxableWages = projection.AnnualizedStateTaxableWages,
            AnnualizedFederalWithholding = projection.AnnualizedFederalWithholding,
            AnnualizedStateWithholding = projection.AnnualizedStateWithholding,
            AnnualizedFica = projection.AnnualizedFica,
            AnnualizedNetPay = projection.AnnualizedNetPay,

            ProjectedYtdGrossPay = projection.ProjectedYtdGrossPay,
            ProjectedYtdFederalWithholding = projection.ProjectedYtdFederalWithholding,
            ProjectedYtdStateWithholding = projection.ProjectedYtdStateWithholding,
            ProjectedYtdFica = projection.ProjectedYtdFica,
            ProjectedYtdNetPay = projection.ProjectedYtdNetPay,

            EstimatedAnnualFederalLiability = projection.EstimatedAnnualFederalLiability,
            EstimatedAnnualFicaLiability = projection.EstimatedAnnualFicaLiability,
            AnnualizedTotalWithholding = projection.AnnualizedTotalWithholding,
            EstimatedTotalLiability = projection.EstimatedTotalLiability,
            OverUnderWithholding = projection.OverUnderWithholding
        };
    }
}
