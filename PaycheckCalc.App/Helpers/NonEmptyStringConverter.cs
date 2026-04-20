using System.Globalization;

namespace PaycheckCalc.App.Helpers;

/// <summary>
/// XAML value converter that returns true when the bound string is
/// non-null and non-whitespace. Used to hide optional compare rows
/// (state filing status, state allowances) when the scenario's state
/// calculator does not surface that field.
/// </summary>
public sealed class NonEmptyStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrWhiteSpace(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
