namespace PaycheckCalc.Core.Tax.Local;

/// <summary>
/// A flexible bag of locality-specific input values keyed by the field
/// <see cref="PaycheckCalc.Core.Tax.State.StateFieldDefinition.Key"/> strings from the
/// calculator's schema.
/// <para>
/// Kept as a separate type from <see cref="PaycheckCalc.Core.Tax.State.StateInputValues"/>
/// so that state and local inputs are isolated even when both are active for the same paycheck.
/// </para>
/// </summary>
public sealed class LocalInputValues : Dictionary<string, object?>
{
    public LocalInputValues() : base(StringComparer.OrdinalIgnoreCase) { }

    public LocalInputValues(IDictionary<string, object?> source)
        : base(source, StringComparer.OrdinalIgnoreCase) { }

    /// <summary>Retrieve a typed value or <paramref name="fallback"/> when missing/null.</summary>
    public T GetValueOrDefault<T>(string key, T fallback = default!)
    {
        if (TryGetValue(key, out var raw) && raw is T typed)
            return typed;

        if (raw != null)
        {
            try
            {
                if (typeof(T) == typeof(decimal))
                    return (T)(object)Convert.ToDecimal(raw);
                if (typeof(T) == typeof(int))
                    return (T)(object)Convert.ToInt32(raw);
                if (typeof(T) == typeof(bool))
                    return (T)(object)Convert.ToBoolean(raw);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                return fallback;
            }
        }

        return fallback;
    }
}
