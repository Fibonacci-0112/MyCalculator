using System.Text.Json;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Connecticut;

public sealed class ConnecticutWithholdingCalculator : IStateWithholdingCalculator
{
    private static readonly IReadOnlyList<string> FilingStatusOptions =
    [   
        "Single",
        "Married Filing Jointly",
        "Married Filing Separately",
        "Head of Household",
        "Qualifying Surviving Spouse"
    ];

    private static readonly IReadOnlyList<string> WithholdingCodeOptions = 
    [
        "Code A",
        "Code B",
        "Code C",
        "Code D",
        "Code E",
        "Code F",
        "No Form CT-W4"
    ];
    
    private static readonly IReadOnlyList<StateFieldDefinition> Schema =
    [
        new()
        {
            Key = "FilingStatus",
            Label = "Filing Status (from CT Form W-4P Step 1c)",
            FieldType = StateFieldType.Picker,
            IsRequired = true,
            DefaultValue = "Single",
            Options = FilingStatusOptions
        },
        new()
        {
            Key = "WithholdingCode",
            Label = "Withholding Code (from CT Form W-4P Step 2b)",
            FieldType = StateFieldType.Picker,
            IsRequired = true,
            DefaultValue = "Code A",
            Options = WithholdingCodeOptions
        },
        new()
        {
            Key = "AdditionalWithholding",
            Label = "Additional Withholding",
            FieldType = StateFieldType.Decimal,
            DefaultValue = 0m
        },
        new()
        {
            Key = "ReducedWithholding",
            Label = "Reduced Withholding",
            FieldType = StateFieldType.Decimal,
            DefaultValue = 0m
        }
    ];
    
    public UsState State => UsState.CT;
    
    public IReadOnlyList<StateFieldDefinition> GetSchema() => Schema;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();
        
        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (!FilingStatusOptions.Contains(status))
            errors.Add($"Invalid Filing Status. Valid options are: {string.Join(", ", FilingStatusOptions)}.");
        
        var code = values.GetValueOrDefault<string>("WithholdingCode", "");
        if (!WithholdingCodeOptions.Contains(code))
            errors.Add($"Invalid Withholding Code. Valid options are: {string.Join(", ", WithholdingCodeOptions)}.");
        
        return errors;
    }
}
