using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Mappers;
using PaycheckCalc.App.Models;
using PaycheckCalc.App.Services;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// View model for the Jobs &amp; YTD flyout. Bound to the shared session's
/// <see cref="AnnualTaxSession.W2Jobs"/> collection with Add/Remove
/// commands and a rollup summary card.
/// </summary>
public partial class JobsAndYtdViewModel : ObservableObject
{
    public JobsAndYtdViewModel(AnnualTaxSession session)
    {
        Session = session;
        Session.W2Jobs.CollectionChanged += (_, _) => RebuildSummary();
        RebuildSummary();
    }

    public AnnualTaxSession Session { get; }

    [ObservableProperty] public partial JobsYtdSummaryModel? Summary { get; set; }

    [RelayCommand]
    private void AddW2Job()
    {
        Session.W2Jobs.Add(new W2JobItemViewModel
        {
            Name = $"Employer {Session.W2Jobs.Count + 1}"
        });
    }

    [RelayCommand]
    private void RemoveW2Job(W2JobItemViewModel? job)
    {
        if (job != null && Session.W2Jobs.Contains(job))
            Session.W2Jobs.Remove(job);
    }

    /// <summary>Manually triggers a summary rebuild (e.g. after a row's amounts change).</summary>
    [RelayCommand]
    public void RebuildSummary()
    {
        Summary = JobsAndYtdMapper.Summarize(Session.W2Jobs);
    }
}
