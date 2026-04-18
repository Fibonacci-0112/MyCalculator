using PaycheckCalc.App.Services;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Maps the shared <see cref="AnnualTaxSession"/> to and from a persistable
/// <see cref="SavedAnnualScenario"/>. Reuses the mappers for each sub-area
/// so the rehydration path follows the same policy as the forward path.
/// </summary>
public static class AnnualScenarioMapper
{
    /// <summary>
    /// Builds a new <see cref="SavedAnnualScenario"/> from the session.
    /// If <paramref name="existingId"/> is provided the returned scenario
    /// keeps the same Id (overwrite-on-save); otherwise a new Id is created.
    /// </summary>
    public static SavedAnnualScenario ToSaved(AnnualTaxSession s, string name, Guid? existingId = null)
    {
        var now = DateTimeOffset.UtcNow;
        var id = existingId ?? Guid.NewGuid();

        return new SavedAnnualScenario
        {
            Id = id,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now,
            Profile = AnnualTaxInputMapper.Map(s)
        };
    }

    /// <summary>
    /// Restores a saved scenario into the shared session. Clears prior
    /// result — callers should re-run the calculator to refresh it.
    /// </summary>
    public static void Restore(AnnualTaxSession s, SavedAnnualScenario scenario)
    {
        var p = scenario.Profile;

        s.TaxYear = p.TaxYear;
        s.FilingStatus = p.FilingStatus;
        s.SelectedFederalPickerItem = s.FederalStatuses
            .FirstOrDefault(f => f.Value == p.FilingStatus) ?? s.FederalStatuses[0];
        s.QualifyingChildren = p.QualifyingChildren;
        s.ItemizedDeductionsOverStandard = p.ItemizedDeductionsOverStandard;
        s.SelectedState = p.ResidenceState;
        s.SelectedStatePickerItem = s.StatePickerItems
            .FirstOrDefault(x => x.Value == p.ResidenceState);

        JobsAndYtdMapper.FromDomain(s.W2Jobs, p.W2Jobs);
        OtherIncomeAdjustmentsMapper.FromDomain(s, p.OtherIncome, p.Adjustments);
        CreditsMapper.FromDomain(s, p.Credits, p.OtherTaxes);

        s.EstimatedTaxPayments = p.EstimatedTaxPayments;
        s.AdditionalExpectedWithholding = p.AdditionalExpectedWithholding;

        if (p.PriorYearSafeHarbor is { } py)
        {
            s.UsePriorYearSafeHarbor = true;
            s.PriorYearTotalTax = py.PriorYearTotalTax;
            s.PriorYearAdjustedGrossIncome = py.PriorYearAdjustedGrossIncome;
            s.PriorYearWasFullYear = py.PriorYearWasFullYear;
        }
        else
        {
            s.UsePriorYearSafeHarbor = false;
            s.PriorYearTotalTax = 0m;
            s.PriorYearAdjustedGrossIncome = 0m;
            s.PriorYearWasFullYear = true;
        }

        s.ResultModel = null;
        s.LoadedScenarioId = scenario.Id;
        s.LoadedScenarioName = scenario.Name;
    }
}
