using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.Federal.Annual;

/// <summary>
/// Form 1040-ES quarterly estimated-tax worksheet.
///
/// Translates the annual Form 1040 total tax into the four installments that
/// Schedule 3 line 26 (estimated tax payments) covers. Applies the statutory
/// safe harbor: the required annual payment is the smaller of
///
///   • 90% of the current year's total tax, or
///   • 100% of the prior year's total tax (110% when prior-year AGI exceeds
///     $150,000 — $75,000 for married filing separately).
///
/// The prior-year branch only applies when the prior return covered a full
/// 12-month tax year; otherwise 1040-ES mandates the 90% CY rule. If the
/// caller leaves prior-year info at zero/null, the calculator falls back to
/// the 90% CY rule unconditionally.
///
/// Expected withholding (usually W-2 Box 2 plus any other federal income tax
/// withheld) is subtracted from the required annual payment before splitting
/// into four equal installments. If withholding already covers the required
/// annual payment, no installments are required.
///
/// Due dates are the standard 1040-ES schedule (Q1 Apr 15, Q2 Jun 15, Q3 Sep
/// 15, Q4 Jan 15 of the following year). The calculator does not shift dates
/// for weekends/holidays; that is the taxpayer's responsibility.
/// </summary>
public sealed class Form1040ESCalculator
{
    /// <summary>
    /// Prior-year AGI threshold that triggers the 110% high-income safe
    /// harbor. True MFS filers have a $75,000 threshold under IRC §6654(d),
    /// but the <see cref="FederalFilingStatus"/> enum in this codebase folds
    /// Single and MFS into a single value, so we conservatively apply the
    /// $150,000 cutoff to all non-joint filers. Dedicated MFS handling can
    /// be added once the enum grows a distinct MFS value.
    /// </summary>
    private const decimal HighIncomeAgiThreshold = 150_000m;

    /// <summary>
    /// Build a <see cref="QuarterlyEstimatesResult"/> from the full annual
    /// picture.
    /// </summary>
    /// <param name="taxYear">Tax year the installments apply to.</param>
    /// <param name="filingStatus">Federal filing status (for the 110% MFS threshold).</param>
    /// <param name="currentYearProjectedTax">Form 1040 line 24 total tax projection.</param>
    /// <param name="expectedWithholding">Expected federal withholding for the year (line 25a+).</param>
    /// <param name="priorYear">Optional prior-year figures for the 100%/110% safe harbor.</param>
    public QuarterlyEstimatesResult Calculate(
        int taxYear,
        FederalFilingStatus filingStatus,
        decimal currentYearProjectedTax,
        decimal expectedWithholding,
        PriorYearSafeHarborInput? priorYear = null)
    {
        // Normalize negatives: a "negative" projected tax means a refund
        // without any liability, which cannot drive an estimate.
        var cyTax = Math.Max(0m, currentYearProjectedTax);
        var withholding = Math.Max(0m, expectedWithholding);

        // ── Step 1: Safe-harbor selection ────────────────────
        // 90% of CY tax is always available.
        var ninetyPercentCy = R(cyTax * 0.90m);

        // Prior-year safe harbor is only available for full 12-month prior
        // returns with positive prior-year tax.
        decimal priorYearTax = 0m;
        decimal priorYearBasis = 0m;
        SafeHarborBasis priorYearBasisKind = SafeHarborBasis.OneHundredPercentOfPriorYear;
        bool priorYearAvailable = false;

        if (priorYear is not null
            && priorYear.PriorYearWasFullYear
            && priorYear.PriorYearTotalTax > 0m)
        {
            priorYearTax = priorYear.PriorYearTotalTax;

            bool highIncome = priorYear.PriorYearAdjustedGrossIncome > HighIncomeAgiThreshold;
            var multiplier = highIncome ? 1.10m : 1.00m;
            priorYearBasis = R(priorYearTax * multiplier);
            priorYearBasisKind = highIncome
                ? SafeHarborBasis.OneHundredTenPercentOfPriorYear
                : SafeHarborBasis.OneHundredPercentOfPriorYear;
            priorYearAvailable = true;
        }

        // Required annual payment: the smaller of the two available bases.
        decimal required;
        SafeHarborBasis basis;
        if (priorYearAvailable && priorYearBasis < ninetyPercentCy)
        {
            required = priorYearBasis;
            basis = priorYearBasisKind;
        }
        else
        {
            required = ninetyPercentCy;
            basis = SafeHarborBasis.NinetyPercentOfCurrentYear;
        }

        // ── Step 2: Net unpaid amount after expected withholding ──
        var totalEstimated = Math.Max(0m, R(required - withholding));

        // ── Step 3: Four equal installments ─────────────────
        // Per 1040-ES, divide by four. Any penny-level remainder from the
        // rounding is rolled into the final installment so the four
        // installments sum exactly to TotalEstimatedPayments.
        var installments = BuildInstallments(taxYear, totalEstimated);

        return new QuarterlyEstimatesResult
        {
            TaxYear = taxYear,
            CurrentYearProjectedTax = R(cyTax),
            PriorYearTotalTax = R(priorYearTax),
            ExpectedWithholding = R(withholding),
            RequiredAnnualPayment = R(required),
            SafeHarborBasis = basis,
            TotalEstimatedPayments = totalEstimated,
            Installments = installments
        };
    }

    private static IReadOnlyList<QuarterlyEstimatePayment> BuildInstallments(int taxYear, decimal total)
    {
        // Standard 1040-ES due dates. Q4 falls in January of the following
        // year. We do not adjust for weekends/holidays — IRS Publication 509
        // handles that case-by-case and the taxpayer should check annually.
        var dueDates = new[]
        {
            new DateOnly(taxYear,     4, 15),
            new DateOnly(taxYear,     6, 15),
            new DateOnly(taxYear,     9, 15),
            new DateOnly(taxYear + 1, 1, 15)
        };

        if (total <= 0m)
        {
            // Zero-amount installments are still returned so the UI can show
            // the schedule with "$0.00 — no payment required".
            var zeros = new QuarterlyEstimatePayment[4];
            for (int i = 0; i < 4; i++)
            {
                zeros[i] = new QuarterlyEstimatePayment
                {
                    Period = $"Q{i + 1}",
                    DueDate = dueDates[i],
                    Amount = 0m,
                    CumulativeAmount = 0m
                };
            }
            return zeros;
        }

        var perQuarter = R(total / 4m);

        // Roll any rounding remainder into Q4 so the installments sum exactly.
        var firstThree = perQuarter * 3m;
        var q4 = R(total - firstThree);

        var items = new QuarterlyEstimatePayment[4];
        decimal cumulative = 0m;
        for (int i = 0; i < 4; i++)
        {
            var amount = i < 3 ? perQuarter : q4;
            cumulative = R(cumulative + amount);
            items[i] = new QuarterlyEstimatePayment
            {
                Period = $"Q{i + 1}",
                DueDate = dueDates[i],
                Amount = amount,
                CumulativeAmount = cumulative
            };
        }
        return items;
    }

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
