using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.State;

/// <summary>
/// 2026 state income tax withholding configurations for states that use the
/// annualized percentage method.  Data sourced from halfpricesoft.com and
/// official state revenue department publications.
/// </summary>
public static class StateTaxConfigs2026
{
    private static TaxBracket B(decimal floor, decimal? ceiling, decimal rate)
        => new() { Floor = floor, Ceiling = ceiling, Rate = rate };

    public static IReadOnlyDictionary<UsState, PercentageMethodConfig> Configs { get; } =
        new Dictionary<UsState, PercentageMethodConfig>
        {
            // ── Flat-rate states ─────────────────────────────────────────

            // Arizona uses a dedicated calculator (ArizonaWithholdingCalculator)
            // — Form A-4 percentage-election method (0.5%–3.5% on gross
            // taxable wages, 2.0% default when no A-4 is on file).

            // Colorado uses a dedicated calculator (ColoradoWithholdingCalculator)

            // Georgia uses a dedicated calculator (GeorgiaWithholdingCalculator)
            // — flat 5.19% with G-4 filing statuses A/B/C/D, $12,000/$24,000
            // standard deductions, $4,000 dependent allowance, and $3,000
            // additional allowance per HB 111 and the 2026 Employer's Tax Guide.

            // Idaho uses a dedicated calculator (IdahoWithholdingCalculator)
            // — flat 5.3% (HB 521, 2024) with filing-status standard
            // deduction ($16,100 / $32,200) and $3,300 per ID W-4 allowance.

            // Illinois uses a dedicated calculator (IllinoisWithholdingCalculator)

            [UsState.IN] = new()
            {
                StandardDeductionSingle = 0m,
                StandardDeductionMarried = 0m,
                AllowanceAmount = 1_000m,
                BracketsSingle = [B(0, null, 0.0305m)],
                BracketsMarried = [B(0, null, 0.0305m)]
            },

            [UsState.IA] = new()
            {
                StandardDeductionSingle = 0m,
                StandardDeductionMarried = 0m,
                BracketsSingle = [B(0, null, 0.0365m)],
                BracketsMarried = [B(0, null, 0.0365m)]
            },

            [UsState.KY] = new()
            {
                StandardDeductionSingle = 3_160m,
                StandardDeductionMarried = 3_160m,
                BracketsSingle = [B(0, null, 0.04m)],
                BracketsMarried = [B(0, null, 0.04m)]
            },

            [UsState.MA] = new()
            {
                StandardDeductionSingle = 0m,
                StandardDeductionMarried = 0m,
                AllowanceAmount = 4_400m,
                BracketsSingle = [B(0, null, 0.05m)],
                BracketsMarried = [B(0, null, 0.05m)]
            },

            // Michigan uses a dedicated calculator (MichiganWithholdingCalculator)
            // — flat 4.25% with MI-W4 exemptions at $5,900 per exemption per the
            // 2026 Form 446 Withholding Guide.

            [UsState.MS] = new()
            {
                StandardDeductionSingle = 2_300m,
                StandardDeductionMarried = 4_600m,
                AllowanceAmount = 6_000m,
                BracketsSingle = [B(0, null, 0.05m)],
                BracketsMarried = [B(0, null, 0.05m)]
            },

            [UsState.NC] = new()
            {
                StandardDeductionSingle = 12_750m,
                StandardDeductionMarried = 25_500m,
                BracketsSingle = [B(0, null, 0.045m)],
                BracketsMarried = [B(0, null, 0.045m)]
            },

            [UsState.UT] = new()
            {
                StandardDeductionSingle = 0m,
                StandardDeductionMarried = 0m,
                BracketsSingle = [B(0, null, 0.0465m)],
                BracketsMarried = [B(0, null, 0.0465m)]
            },

            // ── Graduated-bracket states ─────────────────────────────────

            // Arkansas — handled by ArkansasWithholdingCalculator (DFA formula method)

            // California — handled by CaliforniaWithholdingCalculator (Method B)

            // Connecticut — handled by ConnecticutWithholdingCalculator (TPG-211 table-driven)

            // District of Columbia uses a dedicated calculator
            // (DistrictOfColumbiaWithholdingCalculator) — D-4 annualized
            // percentage method with filing-status standard deduction
            // ($15,000 / $30,000), $1,675 per-allowance exemption, and the
            // 2026 FR-230 graduated brackets (4%–10.75%).

            // Delaware uses a dedicated calculator (DelawareWithholdingCalculator)

            // Hawaii uses a dedicated calculator (HawaiiWithholdingCalculator)
            // — Booklet A percentage method with filing-status standard
            // deduction ($2,200 / $4,400), $1,144 per HW-4 allowance, and
            // the 2026 annual graduated brackets (1.4%–11.0%).

            [UsState.KS] = new()
            {
                StandardDeductionSingle = 3_605m,
                StandardDeductionMarried = 8_240m,
                AllowanceAmount = 2_250m,
                BracketsSingle =
                [
                    B(0, 23_000m, 0.052m),
                    B(23_000m, null, 0.0558m)
                ],
                BracketsMarried =
                [
                    B(0, 46_000m, 0.052m),
                    B(46_000m, null, 0.0558m)
                ]
            },

            [UsState.LA] = new()
            {
                StandardDeductionSingle = 4_500m,
                StandardDeductionMarried = 9_000m,
                BracketsSingle =
                [
                    B(0, 12_500m, 0.0185m),
                    B(12_500m, 50_000m, 0.035m),
                    B(50_000m, null, 0.0425m)
                ],
                BracketsMarried =
                [
                    B(0, 25_000m, 0.0185m),
                    B(25_000m, 100_000m, 0.035m),
                    B(100_000m, null, 0.0425m)
                ]
            },

            [UsState.ME] = new()
            {
                StandardDeductionSingle = 15_300m,
                StandardDeductionMarried = 30_600m,
                AllowanceAmount = 5_300m,
                BracketsSingle =
                [
                    B(0, 27_400m, 0.058m),
                    B(27_400m, 64_850m, 0.0675m),
                    B(64_850m, null, 0.0715m)
                ],
                BracketsMarried =
                [
                    B(0, 54_850m, 0.058m),
                    B(54_850m, 129_750m, 0.0675m),
                    B(129_750m, null, 0.0715m)
                ]
            },

            [UsState.MD] = new()
            {
                StandardDeductionSingle = 2_550m,
                StandardDeductionMarried = 5_100m,
                AllowanceAmount = 3_200m,
                BracketsSingle =
                [
                    B(0, 1_000m, 0.02m),
                    B(1_000m, 2_000m, 0.03m),
                    B(2_000m, 3_000m, 0.04m),
                    B(3_000m, 100_000m, 0.0475m),
                    B(100_000m, 125_000m, 0.05m),
                    B(125_000m, 150_000m, 0.0525m),
                    B(150_000m, 250_000m, 0.055m),
                    B(250_000m, 500_000m, 0.0575m),
                    B(500_000m, 1_000_000m, 0.0625m),
                    B(1_000_000m, null, 0.065m)
                ],
                BracketsMarried =
                [
                    B(0, 1_000m, 0.02m),
                    B(1_000m, 2_000m, 0.03m),
                    B(2_000m, 3_000m, 0.04m),
                    B(3_000m, 150_000m, 0.0475m),
                    B(150_000m, 175_000m, 0.05m),
                    B(175_000m, 225_000m, 0.0525m),
                    B(225_000m, 300_000m, 0.055m),
                    B(300_000m, 600_000m, 0.0575m),
                    B(600_000m, 1_200_000m, 0.0625m),
                    B(1_200_000m, null, 0.065m)
                ]
            },

            [UsState.MN] = new()
            {
                StandardDeductionSingle = 15_300m,
                StandardDeductionMarried = 30_600m,
                AllowanceAmount = 5_300m,
                BracketsSingle =
                [
                    B(0, 33_310m, 0.0535m),
                    B(33_310m, 109_430m, 0.068m),
                    B(109_430m, 203_150m, 0.0785m),
                    B(203_150m, null, 0.0985m)
                ],
                BracketsMarried =
                [
                    B(0, 48_700m, 0.0535m),
                    B(48_700m, 193_480m, 0.068m),
                    B(193_480m, 337_930m, 0.0785m),
                    B(337_930m, null, 0.0985m)
                ]
            },

            [UsState.MO] = new()
            {
                StandardDeductionSingle = 15_750m,
                StandardDeductionMarried = 31_500m,
                BracketsSingle =
                [
                    B(0, 1_313m, 0m),
                    B(1_313m, 2_626m, 0.02m),
                    B(2_626m, 3_939m, 0.025m),
                    B(3_939m, 5_252m, 0.03m),
                    B(5_252m, 6_565m, 0.035m),
                    B(6_565m, 7_878m, 0.04m),
                    B(7_878m, 9_191m, 0.045m),
                    B(9_191m, null, 0.047m)
                ],
                BracketsMarried =
                [
                    B(0, 1_313m, 0m),
                    B(1_313m, 2_626m, 0.02m),
                    B(2_626m, 3_939m, 0.025m),
                    B(3_939m, 5_252m, 0.03m),
                    B(5_252m, 6_565m, 0.035m),
                    B(6_565m, 7_878m, 0.04m),
                    B(7_878m, 9_191m, 0.045m),
                    B(9_191m, null, 0.047m)
                ]
            },

            [UsState.MT] = new()
            {
                StandardDeductionSingle = 5_310m,
                StandardDeductionMarried = 10_620m,
                BracketsSingle =
                [
                    B(0, 23_800m, 0.047m),
                    B(23_800m, null, 0.059m)
                ],
                BracketsMarried =
                [
                    B(0, 23_800m, 0.047m),
                    B(23_800m, null, 0.059m)
                ]
            },

            [UsState.NE] = new()
            {
                StandardDeductionSingle = 8_600m,
                StandardDeductionMarried = 17_200m,
                AllowanceCreditAmount = 171m,
                BracketsSingle =
                [
                    B(0, 4_030m, 0.0246m),
                    B(4_030m, 24_120m, 0.0351m),
                    B(24_120m, 38_870m, 0.0501m),
                    B(38_870m, null, 0.052m)
                ],
                BracketsMarried =
                [
                    B(0, 8_040m, 0.0246m),
                    B(8_040m, 48_250m, 0.0351m),
                    B(48_250m, 77_730m, 0.0501m),
                    B(77_730m, null, 0.052m)
                ]
            },

            [UsState.NJ] = new()
            {
                StandardDeductionSingle = 0m,
                StandardDeductionMarried = 0m,
                AllowanceAmount = 1_000m,
                BracketsSingle =
                [
                    B(0, 20_000m, 0.014m),
                    B(20_000m, 35_000m, 0.0175m),
                    B(35_000m, 40_000m, 0.035m),
                    B(40_000m, 75_000m, 0.0553m),
                    B(75_000m, 500_000m, 0.0637m),
                    B(500_000m, 1_000_000m, 0.0897m),
                    B(1_000_000m, null, 0.1075m)
                ],
                BracketsMarried =
                [
                    B(0, 20_000m, 0.014m),
                    B(20_000m, 50_000m, 0.0175m),
                    B(50_000m, 70_000m, 0.0245m),
                    B(70_000m, 80_000m, 0.035m),
                    B(80_000m, 150_000m, 0.0553m),
                    B(150_000m, 500_000m, 0.0637m),
                    B(500_000m, 1_000_000m, 0.0897m),
                    B(1_000_000m, null, 0.1075m)
                ]
            },

            [UsState.NM] = new()
            {
                StandardDeductionSingle = 15_750m,
                StandardDeductionMarried = 31_500m,
                BracketsSingle =
                [
                    B(0, 5_500m, 0.017m),
                    B(5_500m, 11_000m, 0.032m),
                    B(11_000m, 16_000m, 0.047m),
                    B(16_000m, 210_000m, 0.049m),
                    B(210_000m, null, 0.059m)
                ],
                BracketsMarried =
                [
                    B(0, 8_000m, 0.017m),
                    B(8_000m, 16_000m, 0.032m),
                    B(16_000m, 24_000m, 0.047m),
                    B(24_000m, 315_000m, 0.049m),
                    B(315_000m, null, 0.059m)
                ]
            },

            [UsState.NY] = new()
            {
                StandardDeductionSingle = 8_000m,
                StandardDeductionMarried = 16_050m,
                BracketsSingle =
                [
                    B(0, 8_500m, 0.04m),
                    B(8_500m, 11_700m, 0.045m),
                    B(11_700m, 13_900m, 0.0525m),
                    B(13_900m, 21_400m, 0.059m),
                    B(21_400m, 80_650m, 0.0609m),
                    B(80_650m, 215_400m, 0.0641m),
                    B(215_400m, 1_077_550m, 0.0685m),
                    B(1_077_550m, 5_000_000m, 0.0965m),
                    B(5_000_000m, 25_000_000m, 0.103m),
                    B(25_000_000m, null, 0.109m)
                ],
                BracketsMarried =
                [
                    B(0, 17_150m, 0.04m),
                    B(17_150m, 23_600m, 0.045m),
                    B(23_600m, 27_900m, 0.0525m),
                    B(27_900m, 43_000m, 0.059m),
                    B(43_000m, 161_550m, 0.0609m),
                    B(161_550m, 323_200m, 0.0641m),
                    B(323_200m, 2_155_350m, 0.0685m),
                    B(2_155_350m, 5_000_000m, 0.0965m),
                    B(5_000_000m, 25_000_000m, 0.103m),
                    B(25_000_000m, null, 0.109m)
                ]
            },

            [UsState.ND] = new()
            {
                StandardDeductionSingle = 15_750m,
                StandardDeductionMarried = 31_500m,
                BracketsSingle =
                [
                    B(0, 46_500m, 0.011m),
                    B(46_500m, 113_750m, 0.0204m),
                    B(113_750m, null, 0.0264m)
                ],
                BracketsMarried =
                [
                    B(0, 78_650m, 0.011m),
                    B(78_650m, 197_550m, 0.0204m),
                    B(197_550m, null, 0.0264m)
                ]
            },

            [UsState.OH] = new()
            {
                StandardDeductionSingle = 0m,
                StandardDeductionMarried = 0m,
                BracketsSingle =
                [
                    B(0, 26_050m, 0m),
                    B(26_050m, null, 0.0275m)
                ],
                BracketsMarried =
                [
                    B(0, 26_050m, 0m),
                    B(26_050m, null, 0.0275m)
                ]
            },

            [UsState.OR] = new()
            {
                StandardDeductionSingle = 2_835m,
                StandardDeductionMarried = 5_670m,
                BracketsSingle =
                [
                    B(0, 4_300m, 0.0475m),
                    B(4_300m, 10_750m, 0.0675m),
                    B(10_750m, 125_000m, 0.0875m),
                    B(125_000m, null, 0.099m)
                ],
                BracketsMarried =
                [
                    B(0, 8_600m, 0.0475m),
                    B(8_600m, 21_500m, 0.0675m),
                    B(21_500m, 250_000m, 0.0875m),
                    B(250_000m, null, 0.099m)
                ]
            },

            [UsState.RI] = new()
            {
                StandardDeductionSingle = 10_550m,
                StandardDeductionMarried = 10_550m,
                AllowanceAmount = 4_700m,
                BracketsSingle =
                [
                    B(0, 77_450m, 0.0375m),
                    B(77_450m, 176_050m, 0.0475m),
                    B(176_050m, null, 0.0599m)
                ],
                BracketsMarried =
                [
                    B(0, 77_450m, 0.0375m),
                    B(77_450m, 176_050m, 0.0475m),
                    B(176_050m, null, 0.0599m)
                ]
            },

            [UsState.SC] = new()
            {
                StandardDeductionSingle = 0m,
                StandardDeductionMarried = 0m,
                AllowanceAmount = 5_000m,
                BracketsSingle =
                [
                    B(0, 3_640m, 0m),
                    B(3_640m, 18_230m, 0.03m),
                    B(18_230m, null, 0.06m)
                ],
                BracketsMarried =
                [
                    B(0, 3_640m, 0m),
                    B(3_640m, 18_230m, 0.03m),
                    B(18_230m, null, 0.06m)
                ]
            },

            [UsState.VA] = new()
            {
                StandardDeductionSingle = 8_750m,
                StandardDeductionMarried = 17_500m,
                AllowanceAmount = 930m,
                BracketsSingle =
                [
                    B(0, 3_000m, 0.02m),
                    B(3_000m, 5_000m, 0.03m),
                    B(5_000m, 17_000m, 0.05m),
                    B(17_000m, null, 0.0575m)
                ],
                BracketsMarried =
                [
                    B(0, 3_000m, 0.02m),
                    B(3_000m, 5_000m, 0.03m),
                    B(5_000m, 17_000m, 0.05m),
                    B(17_000m, null, 0.0575m)
                ]
            },

            [UsState.VT] = new()
            {
                StandardDeductionSingle = 15_750m,
                StandardDeductionMarried = 31_500m,
                BracketsSingle =
                [
                    B(0, 47_900m, 0.0335m),
                    B(47_900m, 116_000m, 0.066m),
                    B(116_000m, 242_000m, 0.076m),
                    B(242_000m, null, 0.0875m)
                ],
                BracketsMarried =
                [
                    B(0, 79_950m, 0.0335m),
                    B(79_950m, 193_300m, 0.066m),
                    B(193_300m, 294_600m, 0.076m),
                    B(294_600m, null, 0.0875m)
                ]
            },

            [UsState.WI] = new()
            {
                StandardDeductionSingle = 12_760m,
                StandardDeductionMarried = 23_170m,
                BracketsSingle =
                [
                    B(0, 13_810m, 0.0354m),
                    B(13_810m, 27_630m, 0.0465m),
                    B(27_630m, 304_170m, 0.053m),
                    B(304_170m, null, 0.0765m)
                ],
                BracketsMarried =
                [
                    B(0, 18_410m, 0.0354m),
                    B(18_410m, 36_820m, 0.0465m),
                    B(36_820m, 405_550m, 0.053m),
                    B(405_550m, null, 0.0765m)
                ]
            },

            [UsState.WV] = new()
            {
                StandardDeductionSingle = 0m,
                StandardDeductionMarried = 0m,
                AllowanceAmount = 2_000m,
                BracketsSingle =
                [
                    B(0, 10_000m, 0.03m),
                    B(10_000m, 25_000m, 0.04m),
                    B(25_000m, 40_000m, 0.045m),
                    B(40_000m, 60_000m, 0.06m),
                    B(60_000m, null, 0.065m)
                ],
                BracketsMarried =
                [
                    B(0, 10_000m, 0.03m),
                    B(10_000m, 25_000m, 0.04m),
                    B(25_000m, 40_000m, 0.045m),
                    B(40_000m, 60_000m, 0.06m),
                    B(60_000m, null, 0.065m)
                ]
            },
        };
}
