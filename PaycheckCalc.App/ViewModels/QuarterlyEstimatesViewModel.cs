using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Mappers;
using PaycheckCalc.App.Models;
using PaycheckCalc.App.Services;
using PaycheckCalc.Core.Tax.Federal.Annual;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// View model for the Quarterly Estimates flyout. Wraps the shared
/// <see cref="AnnualTaxSession"/> with two overridable inputs (projected
/// current-year tax and expected withholding), the prior-year safe-harbor
/// block (read from session), and a
/// <see cref="QuarterlyEstimatesCardModel"/> result.
///
/// <para>
/// Math is entirely in <see cref="Form1040ESCalculator"/>; this VM only
/// calls the calculator and maps the result through
/// <see cref="QuarterlyEstimatesResultMapper"/>.
/// </para>
/// </summary>
public partial class QuarterlyEstimatesViewModel : ObservableObject
{
    private readonly Form1040ESCalculator _calc;

    public QuarterlyEstimatesViewModel(AnnualTaxSession session, Form1040ESCalculator calc)
    {
        Session = session;
        _calc = calc;
    }

    public AnnualTaxSession Session { get; }

    /// <summary>
    /// When non-null, overrides the session's cached Form 1040 TotalTax.
    /// Users can enter their own "projected" tax number on the worksheet.
    /// </summary>
    [ObservableProperty] public partial decimal ProjectedTotalTaxOverride { get; set; }
    [ObservableProperty] public partial bool UseProjectedTotalTaxOverride { get; set; }

    /// <summary>
    /// When non-null, overrides the session's derived expected withholding
    /// (summed W-2 Box 2 + <see cref="AnnualTaxSession.AdditionalExpectedWithholding"/>).
    /// </summary>
    [ObservableProperty] public partial decimal ExpectedWithholdingOverride { get; set; }
    [ObservableProperty] public partial bool UseExpectedWithholdingOverride { get; set; }

    [ObservableProperty] public partial QuarterlyEstimatesCardModel? Result { get; set; }

    public bool HasResult => Result is not null;

    partial void OnResultChanged(QuarterlyEstimatesCardModel? value)
        => OnPropertyChanged(nameof(HasResult));

    /// <summary>
    /// Runs Form 1040-ES against the current inputs and publishes the
    /// result on <see cref="Result"/>. Exposed as a method (in addition to
    /// the relay command) to make unit testing easy.
    /// </summary>
    public QuarterlyEstimatesCardModel CalculateInternal()
    {
        var overrides = new QuarterlyEstimatesOverrides(
            UseProjectedTotalTaxOverride ? ProjectedTotalTaxOverride : (decimal?)null,
            UseExpectedWithholdingOverride ? ExpectedWithholdingOverride : (decimal?)null);

        var mapped = QuarterlyEstimatesInputMapper.Map(Session, overrides);
        var domain = _calc.Calculate(
            mapped.TaxYear,
            mapped.FilingStatus,
            mapped.CurrentYearProjectedTax,
            mapped.ExpectedWithholding,
            mapped.PriorYear);

        var card = QuarterlyEstimatesResultMapper.Map(domain);
        Result = card;
        return card;
    }

    [RelayCommand]
    private void Calculate() => CalculateInternal();
}
