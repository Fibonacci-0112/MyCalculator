using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="YtdSummaryCalculator"/>, which powers the YTD-actual
/// tile on the dashboard landing page. Expected values are hand-computed
/// from the literal inputs and are not derived from production helpers.
/// </summary>
public sealed class YtdSummaryCalculatorTest
{
    // ── Empty ────────────────────────────────────────────────

    [Fact]
    public void EmptyList_ReturnsZeroesAndIsEmpty()
    {
        var sut = new YtdSummaryCalculator();

        var summary = sut.Calculate(Array.Empty<SavedPaycheck>(), 2026);

        Assert.Equal(2026, summary.Year);
        Assert.Equal(0, summary.PaycheckCount);
        Assert.Equal(0m, summary.TotalGross);
        Assert.Equal(0m, summary.TotalTaxes);
        Assert.Equal(0m, summary.TotalNet);
        Assert.True(summary.IsEmpty);
    }

    [Fact]
    public void OnlyOtherYearPaychecks_AreIgnored()
    {
        var sut = new YtdSummaryCalculator();
        var paychecks = new[]
        {
            Make(year: 2025, gross: 5_000m, taxes: 1_000m, net: 4_000m),
            Make(year: 2024, gross: 6_000m, taxes: 1_200m, net: 4_800m)
        };

        var summary = sut.Calculate(paychecks, 2026);

        Assert.Equal(0, summary.PaycheckCount);
        Assert.True(summary.IsEmpty);
        Assert.Equal(0m, summary.TotalGross);
    }

    // ── Aggregation ──────────────────────────────────────────

    [Fact]
    public void ThreeInTargetYear_SumsLiteralValues()
    {
        var sut = new YtdSummaryCalculator();
        // 1000 + 1500 + 2000 = 4500 gross
        // 250 + 375 + 500 = 1125 taxes
        // 750 + 1125 + 1500 = 3375 net
        var paychecks = new[]
        {
            Make(year: 2026, gross: 1_000m, taxes: 250m, net:   750m),
            Make(year: 2026, gross: 1_500m, taxes: 375m, net: 1_125m),
            Make(year: 2026, gross: 2_000m, taxes: 500m, net: 1_500m)
        };

        var summary = sut.Calculate(paychecks, 2026);

        Assert.Equal(3, summary.PaycheckCount);
        Assert.Equal(4_500m, summary.TotalGross);
        Assert.Equal(1_125m, summary.TotalTaxes);
        Assert.Equal(3_375m, summary.TotalNet);
        Assert.False(summary.IsEmpty);
    }

    [Fact]
    public void MixedYears_OnlyTargetYearCounted()
    {
        var sut = new YtdSummaryCalculator();
        var paychecks = new[]
        {
            // Two paychecks in the target year — sum: gross 3300, taxes 900, net 2400
            Make(year: 2026, gross: 1_200m, taxes: 300m, net: 900m),
            Make(year: 2026, gross: 2_100m, taxes: 600m, net: 1_500m),
            // One paycheck in the prior year — must be excluded
            Make(year: 2025, gross: 9_999m, taxes: 9_999m, net: 9_999m)
        };

        var summary = sut.Calculate(paychecks, 2026);

        Assert.Equal(2, summary.PaycheckCount);
        Assert.Equal(3_300m, summary.TotalGross);
        Assert.Equal(900m, summary.TotalTaxes);
        Assert.Equal(2_400m, summary.TotalNet);
    }

    // ── PaycheckResult.TotalTaxes wiring ─────────────────────

    [Fact]
    public void TotalTaxes_IncludesAllTaxComponents()
    {
        // Confirms we sum PaycheckResult.TotalTaxes (federal + state + SDI +
        // FICA components + local + LST) rather than just FederalWithholding.
        var sut = new YtdSummaryCalculator();
        var paycheck = new SavedPaycheck
        {
            Name = "PA + Philadelphia",
            UpdatedAt = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero),
            Input = MakeInput(),
            Result = new PaycheckResult
            {
                GrossPay                      = 2_000m,
                FederalWithholding            =   180m,
                StateWithholding              =    60m,
                StateDisabilityInsurance      =     5m,
                SocialSecurityWithholding     =   124m,
                MedicareWithholding           =    29m,
                AdditionalMedicareWithholding =     0m,
                LocalWithholding              =    79m,  // PA Philadelphia EIT
                LocalHeadTax                  =     2m,  // PA LST flat
                NetPay                        = 1_521m
            }
        };

        var summary = sut.Calculate(new[] { paycheck }, 2026);

        // Expected total: 180 + 60 + 5 + 124 + 29 + 0 + 79 + 2 = 479
        Assert.Equal(1, summary.PaycheckCount);
        Assert.Equal(479m, summary.TotalTaxes);
        Assert.Equal(2_000m, summary.TotalGross);
        Assert.Equal(1_521m, summary.TotalNet);
    }

    // ── Year-boundary fixture (UTC) ──────────────────────────

    [Fact]
    public void YearBoundary_UtcMidnight_FallsIntoOwnYear()
    {
        var sut = new YtdSummaryCalculator();
        var lastOfPrior = new SavedPaycheck
        {
            Name = "Dec 31 2025",
            UpdatedAt = new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero),
            Input = MakeInput(),
            Result = MakeResult(gross: 100m, taxes: 10m, net: 90m)
        };
        var firstOfTarget = new SavedPaycheck
        {
            Name = "Jan 1 2026",
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Input = MakeInput(),
            Result = MakeResult(gross: 200m, taxes: 20m, net: 180m)
        };

        var summary = sut.Calculate(new[] { lastOfPrior, firstOfTarget }, 2026);

        Assert.Equal(1, summary.PaycheckCount);
        Assert.Equal(200m, summary.TotalGross);
        Assert.Equal(20m, summary.TotalTaxes);
        Assert.Equal(180m, summary.TotalNet);
    }

    // ── Helpers ─────────────────────────────────────────────

    private static SavedPaycheck Make(int year, decimal gross, decimal taxes, decimal net) =>
        new()
        {
            Name = $"P-{year}",
            UpdatedAt = new DateTimeOffset(year, 6, 15, 12, 0, 0, TimeSpan.Zero),
            Input = MakeInput(),
            Result = MakeResult(gross, taxes, net)
        };

    /// <summary>
    /// Build a <see cref="PaycheckResult"/> whose computed <c>TotalTaxes</c>
    /// equals the supplied <paramref name="taxes"/> literal. The Federal
    /// withholding field carries the whole amount; other tax components are
    /// zero — the dashboard sums <c>TotalTaxes</c> so the breakdown is moot.
    /// </summary>
    private static PaycheckResult MakeResult(decimal gross, decimal taxes, decimal net) =>
        new()
        {
            GrossPay = gross,
            FederalWithholding = taxes,
            NetPay = net,
            State = UsState.TX
        };

    private static PaycheckInput MakeInput() => new()
    {
        Frequency = PayFrequency.Biweekly,
        HourlyRate = 25m,
        RegularHours = 80m,
        State = UsState.TX
    };
}
