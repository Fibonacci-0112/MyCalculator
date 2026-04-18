using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Schema-stability checks for <see cref="SavedAnnualScenario"/>. The JSON
/// format is used by <c>JsonAnnualScenarioRepository</c> in the App, so a
/// regression in the Core record shape would break every user's on-device
/// scenario file. Tests use the exact JSON option set that the repository
/// uses to detect breakage early.
/// </summary>
public class SavedAnnualScenarioJsonTest
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void RoundTrip_PreservesAllTopLevelFields()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var created = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var updated = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);

        var scenario = new SavedAnnualScenario
        {
            Id = id,
            Name = "Baseline 2026",
            CreatedAt = created,
            UpdatedAt = updated,
            Profile = new TaxYearProfile
            {
                TaxYear = 2026,
                FilingStatus = FederalFilingStatus.MarriedFilingJointly,
                ResidenceState = UsState.CO,
                QualifyingChildren = 2,
                W2Jobs = new[]
                {
                    new W2JobInput
                    {
                        Name = "Employer A",
                        WagesBox1 = 120_000m,
                        FederalWithholdingBox2 = 15_000m,
                        SocialSecurityWagesBox3 = 120_000m,
                        SocialSecurityTaxBox4 = 7_440m,
                        MedicareWagesBox5 = 120_000m,
                        MedicareTaxBox6 = 1_740m,
                        StateWagesBox16 = 120_000m,
                        StateWithholdingBox17 = 5_000m,
                        Holder = W2JobHolder.Taxpayer
                    }
                },
                OtherIncome = new OtherIncomeInput
                {
                    TaxableInterest = 250m,
                    OrdinaryDividends = 400m,
                    QualifiedDividends = 350m
                },
                Adjustments = new AdjustmentsInput
                {
                    StudentLoanInterest = 1_000m,
                    HsaDeduction = 3_000m
                },
                EstimatedTaxPayments = 500m,
                AdditionalExpectedWithholding = 200m
            }
        };

        var json = JsonSerializer.Serialize(scenario, Options);
        var restored = JsonSerializer.Deserialize<SavedAnnualScenario>(json, Options);

        Assert.NotNull(restored);
        Assert.Equal(id, restored!.Id);
        Assert.Equal("Baseline 2026", restored.Name);
        Assert.Equal(created, restored.CreatedAt);
        Assert.Equal(updated, restored.UpdatedAt);

        // Profile schema — spot-check the fields most likely to drift.
        Assert.Equal(2026, restored.Profile.TaxYear);
        Assert.Equal(FederalFilingStatus.MarriedFilingJointly, restored.Profile.FilingStatus);
        Assert.Equal(UsState.CO, restored.Profile.ResidenceState);
        Assert.Equal(2, restored.Profile.QualifyingChildren);
        Assert.Single(restored.Profile.W2Jobs);
        Assert.Equal(120_000m, restored.Profile.W2Jobs[0].WagesBox1);
        Assert.Equal(W2JobHolder.Taxpayer, restored.Profile.W2Jobs[0].Holder);
        Assert.Equal(250m, restored.Profile.OtherIncome.TaxableInterest);
        Assert.Equal(3_000m, restored.Profile.Adjustments.HsaDeduction);
        Assert.Equal(500m, restored.Profile.EstimatedTaxPayments);
        Assert.Equal(200m, restored.Profile.AdditionalExpectedWithholding);
    }

    [Fact]
    public void EnumsSerializeAsStrings_NotOrdinals()
    {
        // Ordinal drift is the classic schema-break cause for saved files —
        // if someone adds a new enum value, existing files must still load.
        // JsonStringEnumConverter prevents that by writing the enum name.
        var scenario = new SavedAnnualScenario
        {
            Name = "n",
            Profile = new TaxYearProfile
            {
                FilingStatus = FederalFilingStatus.HeadOfHousehold,
                ResidenceState = UsState.TX
            }
        };

        var json = JsonSerializer.Serialize(scenario, Options);

        Assert.Contains("\"HeadOfHousehold\"", json);
        Assert.Contains("\"TX\"", json);
    }

    [Fact]
    public void List_DeserializeEmptyFile_YieldsEmptyList()
    {
        // Tolerance check for an empty repository file (new install). Echoes
        // the behavior JsonAnnualScenarioRepository falls back to when the
        // on-disk file is missing.
        var list = JsonSerializer.Deserialize<List<SavedAnnualScenario>>("[]", Options);
        Assert.NotNull(list);
        Assert.Empty(list!);
    }
}
