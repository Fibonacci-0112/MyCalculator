using CommunityToolkit.Mvvm.ComponentModel;
using PaycheckCalc.App.Services;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// View model for the Other Income &amp; Adjustments flyout. Thin facade
/// over the shared <see cref="AnnualTaxSession"/> — the page binds directly
/// to session properties for Schedule 1 income lines and above-the-line
/// adjustments. Commands live on <see cref="AnnualTaxViewModel"/>
/// (Calculate) and the Saved Scenarios flow.
/// </summary>
public partial class OtherIncomeAdjustmentsViewModel : ObservableObject
{
    public OtherIncomeAdjustmentsViewModel(AnnualTaxSession session)
    {
        Session = session;
    }

    public AnnualTaxSession Session { get; }
}
