using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for the <see cref="SavedPaycheck"/> model: construction, identity,
/// metadata, and JSON round-trip serialization including nested types.
/// </summary>
public sealed class SavedPaycheckTest
{
    // ── Construction & Identity ──────────────────────────────────

    [Fact]
    public void SavedPaycheck_DefaultId_IsNonEmpty()
    {
        var saved = CreateSample("My Paycheck");
        Assert.NotEqual(Guid.Empty, saved.Id);
    }

    [Fact]
    public void SavedPaycheck_PreservesName()
    {
        var saved = CreateSample("Biweekly – Main Job");
        Assert.Equal("Biweekly – Main Job", saved.Name);
    }

    [Fact]
    public void SavedPaycheck_PreservesInputAndResult()
    {
        var saved = CreateSample("Test");
        Assert.Equal(PayFrequency.Biweekly, saved.Input.Frequency);
        Assert.Equal(25m, saved.Input.HourlyRate);
        Assert.Equal(2187.50m, saved.Result.GrossPay);
        Assert.Equal(1500.00m, saved.Result.NetPay);
    }

    [Fact]
    public void SavedPaycheck_PreservesDeductions()
    {
        var saved = CreateSample("Test");
        Assert.Equal(2, saved.Input.Deductions.Count);
        Assert.Equal("401k", saved.Input.Deductions[0].Name);
        Assert.Equal(DeductionType.PreTax, saved.Input.Deductions[0].Type);
        Assert.Equal(200m, saved.Input.Deductions[0].Amount);
    }

    [Fact]
    public void SavedPaycheck_PreservesFederalW4()
    {
        var saved = CreateSample("Test");
        Assert.Equal(FederalFilingStatus.MarriedFilingJointly, saved.Input.FederalW4.FilingStatus);
        Assert.True(saved.Input.FederalW4.Step2Checked);
        Assert.Equal(2000m, saved.Input.FederalW4.Step3TaxCredits);
    }

    [Fact]
    public void SavedPaycheck_PreservesStateInputValues()
    {
        var saved = CreateSample("Test");
        Assert.NotNull(saved.Input.StateInputValues);
        Assert.Equal("Single", saved.Input.StateInputValues!.GetValueOrDefault<string>("FilingStatus"));
        Assert.Equal(2, saved.Input.StateInputValues!.GetValueOrDefault<int>("Allowances"));
    }

    [Fact]
    public void SavedPaycheck_Name_IsMutable()
    {
        var saved = CreateSample("Original");
        saved.Name = "Renamed";
        Assert.Equal("Renamed", saved.Name);
    }

    [Fact]
    public void SavedPaycheck_UpdatedAt_IsMutable()
    {
        var saved = CreateSample("Test");
        var newTime = DateTimeOffset.UtcNow.AddHours(1);
        saved.UpdatedAt = newTime;
        Assert.Equal(newTime, saved.UpdatedAt);
    }

    // ── JSON Round-Trip Serialization ────────────────────────────

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void SavedPaycheck_JsonRoundTrip_PreservesAllFields()
    {
        var original = CreateSample("Round Trip Test");

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SavedPaycheck>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized!.Id);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.CreatedAt.UtcDateTime, deserialized.CreatedAt.UtcDateTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SavedPaycheck_JsonRoundTrip_PreservesInput()
    {
        var original = CreateSample("Input Test");

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SavedPaycheck>(json, JsonOptions)!;

        Assert.Equal(original.Input.Frequency, deserialized.Input.Frequency);
        Assert.Equal(original.Input.HourlyRate, deserialized.Input.HourlyRate);
        Assert.Equal(original.Input.RegularHours, deserialized.Input.RegularHours);
        Assert.Equal(original.Input.OvertimeHours, deserialized.Input.OvertimeHours);
        Assert.Equal(original.Input.OvertimeMultiplier, deserialized.Input.OvertimeMultiplier);
        Assert.Equal(original.Input.State, deserialized.Input.State);
        Assert.Equal(original.Input.PaycheckNumber, deserialized.Input.PaycheckNumber);
    }

    [Fact]
    public void SavedPaycheck_JsonRoundTrip_PreservesResult()
    {
        var original = CreateSample("Result Test");

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SavedPaycheck>(json, JsonOptions)!;

        Assert.Equal(original.Result.GrossPay, deserialized.Result.GrossPay);
        Assert.Equal(original.Result.NetPay, deserialized.Result.NetPay);
        Assert.Equal(original.Result.FederalWithholding, deserialized.Result.FederalWithholding);
        Assert.Equal(original.Result.StateWithholding, deserialized.Result.StateWithholding);
        Assert.Equal(original.Result.SocialSecurityWithholding, deserialized.Result.SocialSecurityWithholding);
        Assert.Equal(original.Result.MedicareWithholding, deserialized.Result.MedicareWithholding);
        Assert.Equal(original.Result.State, deserialized.Result.State);
    }

    [Fact]
    public void SavedPaycheck_JsonRoundTrip_PreservesFederalW4()
    {
        var original = CreateSample("W4 Test");

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SavedPaycheck>(json, JsonOptions)!;

        Assert.Equal(original.Input.FederalW4.FilingStatus, deserialized.Input.FederalW4.FilingStatus);
        Assert.Equal(original.Input.FederalW4.Step2Checked, deserialized.Input.FederalW4.Step2Checked);
        Assert.Equal(original.Input.FederalW4.Step3TaxCredits, deserialized.Input.FederalW4.Step3TaxCredits);
        Assert.Equal(original.Input.FederalW4.Step4aOtherIncome, deserialized.Input.FederalW4.Step4aOtherIncome);
        Assert.Equal(original.Input.FederalW4.Step4bDeductions, deserialized.Input.FederalW4.Step4bDeductions);
        Assert.Equal(original.Input.FederalW4.Step4cExtraWithholding, deserialized.Input.FederalW4.Step4cExtraWithholding);
    }

    [Fact]
    public void SavedPaycheck_JsonRoundTrip_PreservesDeductions()
    {
        var original = CreateSample("Deduction Test");

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SavedPaycheck>(json, JsonOptions)!;

        Assert.Equal(original.Input.Deductions.Count, deserialized.Input.Deductions.Count);
        Assert.Equal(original.Input.Deductions[0].Name, deserialized.Input.Deductions[0].Name);
        Assert.Equal(original.Input.Deductions[0].Type, deserialized.Input.Deductions[0].Type);
        Assert.Equal(original.Input.Deductions[0].Amount, deserialized.Input.Deductions[0].Amount);
        Assert.Equal(original.Input.Deductions[0].AmountType, deserialized.Input.Deductions[0].AmountType);
        Assert.Equal(original.Input.Deductions[0].ReducesStateTaxableWages, deserialized.Input.Deductions[0].ReducesStateTaxableWages);
        Assert.Equal(original.Input.Deductions[1].Name, deserialized.Input.Deductions[1].Name);
        Assert.Equal(original.Input.Deductions[1].Type, deserialized.Input.Deductions[1].Type);
    }

    [Fact]
    public void SavedPaycheck_JsonRoundTrip_PreservesEnumsAsStrings()
    {
        var original = CreateSample("Enum Test");

        var json = JsonSerializer.Serialize(original, JsonOptions);

        // Verify enums are written as strings, not integers
        Assert.Contains("\"Biweekly\"", json);
        Assert.Contains("\"MarriedFilingJointly\"", json);
        Assert.Contains("\"PreTax\"", json);
        Assert.Contains("\"OK\"", json);
    }

    [Fact]
    public void SavedPaycheck_JsonRoundTrip_ListOfPaychecks()
    {
        var list = new List<SavedPaycheck>
        {
            CreateSample("Paycheck A"),
            CreateSample("Paycheck B"),
            CreateSample("Paycheck C")
        };

        var json = JsonSerializer.Serialize(list, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<List<SavedPaycheck>>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized!.Count);
        Assert.Equal("Paycheck A", deserialized[0].Name);
        Assert.Equal("Paycheck B", deserialized[1].Name);
        Assert.Equal("Paycheck C", deserialized[2].Name);
    }

    [Fact]
    public void SavedPaycheck_JsonRoundTrip_StateDisabilityInsuranceLabel()
    {
        var saved = new SavedPaycheck
        {
            Name = "CA Paycheck",
            Input = new PaycheckInput
            {
                Frequency = PayFrequency.Monthly,
                HourlyRate = 50m,
                RegularHours = 160m,
                State = UsState.CA
            },
            Result = new PaycheckResult
            {
                GrossPay = 8000m,
                StateDisabilityInsurance = 88m,
                StateDisabilityInsuranceLabel = "State Disability Insurance (SDI)",
                NetPay = 6000m
            }
        };

        var json = JsonSerializer.Serialize(saved, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SavedPaycheck>(json, JsonOptions)!;

        Assert.Equal("State Disability Insurance (SDI)", deserialized.Result.StateDisabilityInsuranceLabel);
    }

    // ── Helper ───────────────────────────────────────────────────

    private static SavedPaycheck CreateSample(string name) => new()
    {
        Name = name,
        Input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 25m,
            RegularHours = 80m,
            OvertimeHours = 5m,
            OvertimeMultiplier = 1.5m,
            State = UsState.OK,
            PaycheckNumber = 3,
            StateInputValues = new StateInputValues
            {
                ["FilingStatus"] = "Single",
                ["Allowances"] = 2
            },
            FederalW4 = new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.MarriedFilingJointly,
                Step2Checked = true,
                Step3TaxCredits = 2000m,
                Step4aOtherIncome = 500m,
                Step4bDeductions = 1000m,
                Step4cExtraWithholding = 50m
            },
            Deductions =
            [
                new Deduction { Name = "401k", Type = DeductionType.PreTax, Amount = 200m, AmountType = DeductionAmountType.Dollar, ReducesStateTaxableWages = true },
                new Deduction { Name = "Roth IRA", Type = DeductionType.PostTax, Amount = 100m, AmountType = DeductionAmountType.Dollar, ReducesStateTaxableWages = false }
            ]
        },
        Result = new PaycheckResult
        {
            GrossPay = 2187.50m,
            PreTaxDeductions = 200m,
            PostTaxDeductions = 100m,
            State = UsState.OK,
            StateTaxableWages = 1987.50m,
            StateWithholding = 75.00m,
            StateDisabilityInsurance = 0m,
            SocialSecurityWithholding = 135.63m,
            MedicareWithholding = 31.72m,
            AdditionalMedicareWithholding = 0m,
            FederalTaxableIncome = 1987.50m,
            FederalWithholding = 100.00m,
            NetPay = 1500.00m
        }
    };
}
