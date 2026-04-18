using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Services;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// View model for the Credits flyout. Bound to the shared session's
/// credit-related fields plus a dynamic Form 8863 students collection.
/// </summary>
public partial class CreditsViewModel : ObservableObject
{
    public CreditsViewModel(AnnualTaxSession session)
    {
        Session = session;
    }

    public AnnualTaxSession Session { get; }

    [RelayCommand]
    private void AddStudent()
    {
        Session.EducationStudents.Add(new EducationStudentItemViewModel
        {
            Name = $"Student {Session.EducationStudents.Count + 1}",
            ClaimAmericanOpportunityCredit = true
        });
        if (!Session.UseStructuredEducationCredits)
            Session.UseStructuredEducationCredits = true;
    }

    [RelayCommand]
    private void RemoveStudent(EducationStudentItemViewModel? student)
    {
        if (student != null && Session.EducationStudents.Contains(student))
            Session.EducationStudents.Remove(student);
    }
}
