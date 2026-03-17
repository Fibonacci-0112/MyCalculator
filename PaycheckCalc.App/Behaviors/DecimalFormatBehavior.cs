using System.Globalization;

namespace PaycheckCalc.App.Behaviors;

public class DecimalFormatBehavior : Behavior<Entry>
{
    public static readonly BindableProperty IsCurrencyProperty =
        BindableProperty.Create(nameof(IsCurrency), typeof(bool), typeof(DecimalFormatBehavior), false);

    public bool IsCurrency
    {
        get => (bool)GetValue(IsCurrencyProperty);
        set => SetValue(IsCurrencyProperty, value);
    }

    protected override void OnAttachedTo(Entry entry)
    {
        entry.Unfocused += OnUnfocused;
        entry.Focused += OnFocused;
        base.OnAttachedTo(entry);
    }

    protected override void OnDetachingFrom(Entry entry)
    {
        entry.Unfocused -= OnUnfocused;
        entry.Focused -= OnFocused;
        base.OnDetachingFrom(entry);
    }

    private void OnFocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry || !IsCurrency)
            return;

        if (decimal.TryParse(entry.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var value))
        {
            entry.Text = value.ToString("0.00", CultureInfo.CurrentCulture);
        }
    }

    private void OnUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry)
            return;

        if (decimal.TryParse(entry.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var value))
        {
            entry.Text = IsCurrency
                ? value.ToString("C2", CultureInfo.CurrentCulture)
                : value.ToString("0.00", CultureInfo.CurrentCulture);
        }
    }
}

