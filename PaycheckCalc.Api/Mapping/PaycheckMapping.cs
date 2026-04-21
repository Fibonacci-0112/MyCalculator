using System.Text.Json;
using PaycheckCalc.Api.Dtos;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Api.Mapping;

/// <summary>
/// Maps between API DTOs and <c>PaycheckCalc.Core</c> domain types.
/// Keeps the API layer thin and the core library untouched.
/// </summary>
internal static class PaycheckMapping
{
    public static PaycheckInput ToDomain(this PaycheckInputDto dto)
    {
        var stateValues = dto.StateInputValues is { Count: > 0 }
            ? ToStateValues(dto.StateInputValues)
            : new StateInputValues();

        // Local values reuse the same bag semantics (case-insensitive dict of
        // object values). Core exposes a dedicated LocalInputValues type that
        // derives from the same base dictionary shape.
        var localValues = dto.LocalInputValues is { Count: > 0 }
            ? ToLocalValues(dto.LocalInputValues)
            : null;

        return new PaycheckInput
        {
            Frequency = dto.Frequency,
            HourlyRate = dto.HourlyRate,
            RegularHours = dto.RegularHours,
            OvertimeHours = dto.OvertimeHours,
            OvertimeMultiplier = dto.OvertimeMultiplier == 0m ? 1.5m : dto.OvertimeMultiplier,
            State = dto.State,
            StateInputValues = stateValues,
            HomeLocalityCode = dto.HomeLocalityCode,
            WorkLocalityCode = dto.WorkLocalityCode,
            LocalInputValues = localValues,
            FederalW4 = new FederalW4Input
            {
                FilingStatus = dto.FederalW4.FilingStatus,
                Step2Checked = dto.FederalW4.Step2Checked,
                Step3TaxCredits = dto.FederalW4.Step3TaxCredits,
                Step4aOtherIncome = dto.FederalW4.Step4aOtherIncome,
                Step4bDeductions = dto.FederalW4.Step4bDeductions,
                Step4cExtraWithholding = dto.FederalW4.Step4cExtraWithholding
            },
            Deductions = dto.Deductions
                .Select(d => new Deduction
                {
                    Name = d.Name,
                    Type = d.Type,
                    Amount = d.Amount,
                    AmountType = d.AmountType,
                    ReducesStateTaxableWages = d.ReducesStateTaxableWages
                })
                .ToArray(),
            YtdSocialSecurityWages = dto.YtdSocialSecurityWages,
            YtdMedicareWages = dto.YtdMedicareWages,
            PaycheckNumber = dto.PaycheckNumber <= 0 ? 1 : dto.PaycheckNumber
        };
    }

    public static PaycheckResultDto ToDto(this PaycheckResult r) => new()
    {
        GrossPay = r.GrossPay,
        PreTaxDeductions = r.PreTaxDeductions,
        PostTaxDeductions = r.PostTaxDeductions,
        State = r.State.ToString(),
        StateTaxableWages = r.StateTaxableWages,
        StateWithholding = r.StateWithholding,
        StateDisabilityInsurance = r.StateDisabilityInsurance,
        StateDisabilityInsuranceLabel = r.StateDisabilityInsuranceLabel,
        SocialSecurityWithholding = r.SocialSecurityWithholding,
        MedicareWithholding = r.MedicareWithholding,
        AdditionalMedicareWithholding = r.AdditionalMedicareWithholding,
        FederalTaxableIncome = r.FederalTaxableIncome,
        FederalWithholding = r.FederalWithholding,
        LocalTaxableWages = r.LocalTaxableWages,
        LocalWithholding = r.LocalWithholding,
        LocalHeadTax = r.LocalHeadTax,
        LocalHeadTaxLabel = r.LocalHeadTaxLabel,
        LocalityLabel = r.LocalityLabel,
        LocalBreakdown = r.LocalBreakdown
            .Select(l => new LocalBreakdownDto(
                l.LocalityCode,
                l.LocalityName,
                l.TaxableWages,
                l.Withholding,
                l.HeadTax,
                l.HeadTaxLabel,
                l.Description))
            .ToArray(),
        TotalTaxes = r.TotalTaxes,
        NetPay = r.NetPay
    };

    public static StateFieldDefinitionDto ToDto(this StateFieldDefinition def) => new()
    {
        Key = def.Key,
        Label = def.Label,
        FieldType = def.FieldType.ToString(),
        IsRequired = def.IsRequired,
        DefaultValue = def.DefaultValue,
        Options = def.Options
    };

    /// <summary>
    /// Normalizes a JSON-sourced value bag into primitive types understood by
    /// <see cref="StateInputValues.GetValueOrDefault{T}(string, T)"/>.
    /// System.Text.Json defaults <c>object</c> properties to <see cref="JsonElement"/>,
    /// which does not round-trip through <c>Convert.ToDecimal/ToInt32/ToBoolean</c>.
    /// </summary>
    private static StateInputValues ToStateValues(IDictionary<string, object?> source)
    {
        var dest = new StateInputValues();
        foreach (var kv in source)
            dest[kv.Key] = Normalize(kv.Value);
        return dest;
    }

    private static Core.Tax.Local.LocalInputValues ToLocalValues(IDictionary<string, object?> source)
    {
        var dest = new Core.Tax.Local.LocalInputValues();
        foreach (var kv in source)
            dest[kv.Key] = Normalize(kv.Value);
        return dest;
    }

    private static object? Normalize(object? raw)
    {
        if (raw is not JsonElement el) return raw;

        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Number when el.TryGetInt32(out var i) => i,
            JsonValueKind.Number when el.TryGetInt64(out var l) => l,
            JsonValueKind.Number when el.TryGetDecimal(out var d) => d,
            JsonValueKind.Number => el.GetDouble(),
            _ => el.ToString()
        };
    }
}
