using System;
using System.Collections.Generic;
using System.Text;

namespace PaycheckCalc.App.Helpers;

public static class EnumDisplay
{
    public static string PayFrequency(string name) => name switch
    {
        "BiWeekly" => "Bi-Weekly",
        "SemiMonthly" => "Semi-Monthly",
        "SemiAnnual" => "Semi-Annual",
        _ => SplitPascalCase(name)
    };

    public static string FederalFilingStatus(string name) => name switch
    {
        "SingleOrMarriedSeparately" => "Single, Married Filing Separately",
        "MarriedFilingJointly" => "Married Filing Jointly, Qualifying Surviving Spouse",
        "HeadOfHousehold" => "Head of Household",
        _ => SplitPascalCase(name)
    };

    private static string SplitPascalCase(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        var chars = new List<char>(s.Length + 8);

        for (int i = 0;i < s.Length;i++)
        {
            var c = s[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(s[i - 1]))
                chars.Add(' ');
            chars.Add(c);
        }
        return new string(chars.ToArray());
    }
}