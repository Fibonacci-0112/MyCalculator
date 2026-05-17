namespace PaycheckCalc.Blazor.Services;

/// <summary>
/// User-facing label helpers for enums that show up in the Blazor UI.
/// Mirror of <c>PaycheckCalc.App.Helpers.EnumDisplay</c> so the Blazor and
/// MAUI surfaces show the same strings for the same enum values.
/// </summary>
public static class EnumDisplay
{
    public static string FederalFilingStatus(string name) => name switch
    {
        "SingleOrMarriedSeparately" => "Single, Married Filing Separately",
        "MarriedFilingJointly" => "Married Filing Jointly, Qualifying Surviving Spouse",
        "HeadOfHousehold" => "Head of Household",
        _ => name
    };

    public static string SafeHarborBasis(PaycheckCalc.Core.Models.SafeHarborBasis basis) => basis switch
    {
        PaycheckCalc.Core.Models.SafeHarborBasis.NinetyPercentOfCurrentYear => "90% of current-year tax",
        PaycheckCalc.Core.Models.SafeHarborBasis.OneHundredPercentOfPriorYear => "100% of prior-year tax",
        PaycheckCalc.Core.Models.SafeHarborBasis.OneHundredTenPercentOfPriorYear => "110% of prior-year tax (high-income)",
        _ => basis.ToString()
    };

    public static string UsStateName(string abbreviation) => abbreviation switch
    {
        "AL" => "Alabama",
        "AK" => "Alaska",
        "AZ" => "Arizona",
        "AR" => "Arkansas",
        "CA" => "California",
        "CO" => "Colorado",
        "CT" => "Connecticut",
        "DC" => "District of Columbia",
        "DE" => "Delaware",
        "FL" => "Florida",
        "GA" => "Georgia",
        "HI" => "Hawaii",
        "ID" => "Idaho",
        "IL" => "Illinois",
        "IN" => "Indiana",
        "IA" => "Iowa",
        "KS" => "Kansas",
        "KY" => "Kentucky",
        "LA" => "Louisiana",
        "ME" => "Maine",
        "MD" => "Maryland",
        "MA" => "Massachusetts",
        "MI" => "Michigan",
        "MN" => "Minnesota",
        "MS" => "Mississippi",
        "MO" => "Missouri",
        "MT" => "Montana",
        "NE" => "Nebraska",
        "NV" => "Nevada",
        "NH" => "New Hampshire",
        "NJ" => "New Jersey",
        "NM" => "New Mexico",
        "NY" => "New York",
        "NC" => "North Carolina",
        "ND" => "North Dakota",
        "OH" => "Ohio",
        "OK" => "Oklahoma",
        "OR" => "Oregon",
        "PA" => "Pennsylvania",
        "RI" => "Rhode Island",
        "SC" => "South Carolina",
        "SD" => "South Dakota",
        "TN" => "Tennessee",
        "TX" => "Texas",
        "UT" => "Utah",
        "VT" => "Vermont",
        "VA" => "Virginia",
        "WA" => "Washington",
        "WV" => "West Virginia",
        "WI" => "Wisconsin",
        "WY" => "Wyoming",
        _ => abbreviation
    };
}
