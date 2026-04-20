using System.Globalization;

namespace PaycheckCalc.App.Helpers;

/// <summary>
/// Converter that returns <c>true</c> when the bound value is non-null,
/// used to toggle visibility of UI affordances that depend on optional
/// presentation-model properties (e.g., drill-down explanations).
/// </summary>
public sealed class NotNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
