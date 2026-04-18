using CommunityToolkit.Mvvm.ComponentModel;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// Single entry in the annual W-2 jobs list. Kept as a separate
/// <see cref="ObservableObject"/> so the view can bind each row
/// independently and so removal/addition keeps PropertyChanged semantics.
/// </summary>
public partial class W2JobItemViewModel : ObservableObject
{
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial decimal WagesBox1 { get; set; }
    [ObservableProperty] public partial decimal FederalWithholdingBox2 { get; set; }
    [ObservableProperty] public partial decimal SocialSecurityWagesBox3 { get; set; }
    [ObservableProperty] public partial decimal SocialSecurityTaxBox4 { get; set; }
    [ObservableProperty] public partial decimal MedicareWagesBox5 { get; set; }
    [ObservableProperty] public partial decimal MedicareTaxBox6 { get; set; }
    [ObservableProperty] public partial decimal StateWagesBox16 { get; set; }
    [ObservableProperty] public partial decimal StateWithholdingBox17 { get; set; }

    [ObservableProperty] public partial bool IsSpouse { get; set; }
}
