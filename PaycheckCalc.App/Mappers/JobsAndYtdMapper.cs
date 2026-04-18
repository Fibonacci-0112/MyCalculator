using PaycheckCalc.App.Models;
using PaycheckCalc.App.ViewModels;
using PaycheckCalc.Core.Models;
using System.Collections.ObjectModel;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Maps the W-2 jobs list between the UI
/// (<see cref="ObservableCollection{T}"/> of <see cref="W2JobItemViewModel"/>)
/// and the domain (<see cref="List{T}"/> of <see cref="W2JobInput"/>).
/// Also produces a read-only <see cref="JobsYtdSummaryModel"/> rollup used
/// by the Jobs &amp; YTD page.
/// </summary>
public static class JobsAndYtdMapper
{
    public static List<W2JobInput> ToDomain(IEnumerable<W2JobItemViewModel> rows)
        => rows.Select(j => new W2JobInput
        {
            Name = j.Name,
            Holder = j.IsSpouse ? W2JobHolder.Spouse : W2JobHolder.Taxpayer,
            WagesBox1 = Math.Max(0m, j.WagesBox1),
            FederalWithholdingBox2 = Math.Max(0m, j.FederalWithholdingBox2),
            SocialSecurityWagesBox3 = Math.Max(0m, j.SocialSecurityWagesBox3),
            SocialSecurityTaxBox4 = Math.Max(0m, j.SocialSecurityTaxBox4),
            MedicareWagesBox5 = Math.Max(0m, j.MedicareWagesBox5),
            MedicareTaxBox6 = Math.Max(0m, j.MedicareTaxBox6),
            StateWagesBox16 = Math.Max(0m, j.StateWagesBox16),
            StateWithholdingBox17 = Math.Max(0m, j.StateWithholdingBox17)
        }).ToList();

    public static void FromDomain(
        ObservableCollection<W2JobItemViewModel> target,
        IEnumerable<W2JobInput>? source)
    {
        target.Clear();
        if (source is null) return;
        foreach (var j in source)
        {
            target.Add(new W2JobItemViewModel
            {
                Name = j.Name,
                IsSpouse = j.Holder == W2JobHolder.Spouse,
                WagesBox1 = j.WagesBox1,
                FederalWithholdingBox2 = j.FederalWithholdingBox2,
                SocialSecurityWagesBox3 = j.SocialSecurityWagesBox3,
                SocialSecurityTaxBox4 = j.SocialSecurityTaxBox4,
                MedicareWagesBox5 = j.MedicareWagesBox5,
                MedicareTaxBox6 = j.MedicareTaxBox6,
                StateWagesBox16 = j.StateWagesBox16,
                StateWithholdingBox17 = j.StateWithholdingBox17
            });
        }
    }

    /// <summary>
    /// Cross-job YTD rollup for the summary card. Pure summation — no tax
    /// math, to keep the mapper policy-free.
    /// </summary>
    public static JobsYtdSummaryModel Summarize(IEnumerable<W2JobItemViewModel> rows)
    {
        decimal wages = 0m, fedWh = 0m, ssWages = 0m, ssTax = 0m,
                medWages = 0m, medTax = 0m, stateWages = 0m, stateWh = 0m;
        int count = 0;

        foreach (var j in rows)
        {
            wages      += Math.Max(0m, j.WagesBox1);
            fedWh      += Math.Max(0m, j.FederalWithholdingBox2);
            ssWages    += Math.Max(0m, j.SocialSecurityWagesBox3);
            ssTax      += Math.Max(0m, j.SocialSecurityTaxBox4);
            medWages   += Math.Max(0m, j.MedicareWagesBox5);
            medTax     += Math.Max(0m, j.MedicareTaxBox6);
            stateWages += Math.Max(0m, j.StateWagesBox16);
            stateWh    += Math.Max(0m, j.StateWithholdingBox17);
            count++;
        }

        return new JobsYtdSummaryModel
        {
            JobCount = count,
            TotalBox1Wages = wages,
            TotalFederalWithholding = fedWh,
            TotalSocialSecurityWages = ssWages,
            TotalSocialSecurityTax = ssTax,
            TotalMedicareWages = medWages,
            TotalMedicareTax = medTax,
            TotalStateWages = stateWages,
            TotalStateWithholding = stateWh
        };
    }
}
