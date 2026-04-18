namespace PaycheckCalc.App.Models;

/// <summary>
/// Read-only rollup of the W-2 jobs list used by the Jobs &amp; YTD
/// summary card. Pure summation — no tax policy here.
/// </summary>
public sealed class JobsYtdSummaryModel
{
    public int JobCount { get; init; }
    public decimal TotalBox1Wages { get; init; }
    public decimal TotalFederalWithholding { get; init; }
    public decimal TotalSocialSecurityWages { get; init; }
    public decimal TotalSocialSecurityTax { get; init; }
    public decimal TotalMedicareWages { get; init; }
    public decimal TotalMedicareTax { get; init; }
    public decimal TotalStateWages { get; init; }
    public decimal TotalStateWithholding { get; init; }

    public bool HasJobs => JobCount > 0;
    public decimal TotalFicaTax => TotalSocialSecurityTax + TotalMedicareTax;
}
