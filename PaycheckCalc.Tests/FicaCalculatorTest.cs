using PaycheckCalc.Core.Tax.Fica;
using Xunit;

public class FicaCalculatorTest
{
    // ── No YTD wages ─────────────────────────────────────────────────

    [Fact]
    public void NoYtd_CalculatesSsAndMedicare()
    {
        var calc = new FicaCalculator();

        var (ss, medicare, addl) = calc.Calculate(
            medicareWagesThisPeriod: 3000m,
            ytdSsWages: 0m,
            ytdMedicareWages: 0m);

        // SS = 3000 * 6.2% = 186.00
        // Medicare = 3000 * 1.45% = 43.50
        // Additional = 0 (below $200,000 threshold)
        Assert.Equal(186.00m, ss);
        Assert.Equal(43.50m, medicare);
        Assert.Equal(0m, addl);
    }

    // ── Social Security wage base ─────────────────────────────────────

    [Fact]
    public void YtdNearSsWageBase_CapsSsAtRemainingBase()
    {
        var calc = new FicaCalculator();

        // Wage base = $184,500; YTD = $183,000 → only $1,500 remains
        var (ss, medicare, addl) = calc.Calculate(
            medicareWagesThisPeriod: 5000m,
            ytdSsWages: 183_000m,
            ytdMedicareWages: 183_000m);

        // SS taxable = min(5000, 184500 - 183000) = min(5000, 1500) = 1500
        // SS = 1500 * 6.2% = 93.00
        // Medicare = 5000 * 1.45% = 72.50
        Assert.Equal(93.00m, ss);
        Assert.Equal(72.50m, medicare);
        Assert.Equal(0m, addl);
    }

    [Fact]
    public void YtdAtSsWageBase_ZeroSs()
    {
        var calc = new FicaCalculator();

        // YTD already at wage base — no more SS withholding
        var (ss, medicare, addl) = calc.Calculate(
            medicareWagesThisPeriod: 2000m,
            ytdSsWages: 184_500m,
            ytdMedicareWages: 184_500m);

        // SS = 0 (remaining base = 0)
        // Medicare = 2000 * 1.45% = 29.00
        Assert.Equal(0m, ss);
        Assert.Equal(29.00m, medicare);
        Assert.Equal(0m, addl);
    }

    [Fact]
    public void YtdExceedsSsWageBase_ZeroSs()
    {
        var calc = new FicaCalculator();

        // Defensive: if YTD somehow exceeds wage base, SS should still be 0
        var (ss, medicare, addl) = calc.Calculate(
            medicareWagesThisPeriod: 1000m,
            ytdSsWages: 200_000m,
            ytdMedicareWages: 0m);

        Assert.Equal(0m, ss);
        Assert.Equal(14.50m, medicare);
        Assert.Equal(0m, addl);
    }

    // ── Additional Medicare tax ───────────────────────────────────────

    [Fact]
    public void CrossesAdditionalMedicareThreshold_ChargesPartialPeriod()
    {
        var calc = new FicaCalculator();

        // YTD = $175,000; wages this period = $50,000 → crosses $200,000 threshold
        // prior over threshold: max(0, 175000 - 200000) = 0
        // current over threshold: max(0, 225000 - 200000) = 25000
        // over = 25000 - 0 = 25000
        // Additional = 25000 * 0.9% = 225.00
        var (ss, medicare, addl) = calc.Calculate(
            medicareWagesThisPeriod: 50_000m,
            ytdSsWages: 0m,
            ytdMedicareWages: 175_000m);

        Assert.Equal(225.00m, addl);
        // Normal Medicare: 50000 * 1.45% = 725.00
        Assert.Equal(725.00m, medicare);
    }

    [Fact]
    public void AlreadyAboveAdditionalMedicareThreshold_ChargesFullPeriod()
    {
        var calc = new FicaCalculator();

        // YTD = $210,000 (already above $200,000); wages = $10,000
        // prior over: max(0, 210000 - 200000) = 10000
        // current over: max(0, 220000 - 200000) = 20000
        // over = 20000 - 10000 = 10000
        // Additional = 10000 * 0.9% = 90.00
        var (ss, medicare, addl) = calc.Calculate(
            medicareWagesThisPeriod: 10_000m,
            ytdSsWages: 0m,
            ytdMedicareWages: 210_000m);

        Assert.Equal(90.00m, addl);
        Assert.Equal(145.00m, medicare);
    }

    [Fact]
    public void BelowAdditionalMedicareThreshold_ZeroAdditional()
    {
        var calc = new FicaCalculator();

        var (ss, medicare, addl) = calc.Calculate(
            medicareWagesThisPeriod: 5000m,
            ytdSsWages: 0m,
            ytdMedicareWages: 100_000m);

        // current = 105,000 — still below $200,000
        Assert.Equal(0m, addl);
    }

    // ── Zero wages ───────────────────────────────────────────────────

    [Fact]
    public void ZeroWages_AllResultsAreZero()
    {
        var calc = new FicaCalculator();

        var (ss, medicare, addl) = calc.Calculate(
            medicareWagesThisPeriod: 0m,
            ytdSsWages: 0m,
            ytdMedicareWages: 0m);

        Assert.Equal(0m, ss);
        Assert.Equal(0m, medicare);
        Assert.Equal(0m, addl);
    }

    // ── Constants ────────────────────────────────────────────────────

    [Fact]
    public void Constants_HaveExpected2026Values()
    {
        Assert.Equal(0.062m, FicaCalculator.SocialSecurityRate);
        Assert.Equal(0.0145m, FicaCalculator.MedicareRate);
        Assert.Equal(0.009m, FicaCalculator.AdditionalMedicareRate);
        Assert.Equal(184_500m, new FicaCalculator().SocialSecurityWageBase);
        Assert.Equal(200_000m, new FicaCalculator().AdditionalMedicareEmployerThreshold);
    }
}
