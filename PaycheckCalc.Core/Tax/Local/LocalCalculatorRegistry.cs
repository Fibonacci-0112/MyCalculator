using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.Local;

/// <summary>
/// Central registry that maps locality codes (e.g. <c>"PA-PSD-510101"</c>,
/// <c>"NY-NYC"</c>) to their <see cref="ILocalWithholdingCalculator"/>.
/// <para>
/// Extensible by design: new localities just call <see cref="Register"/>; no core
/// enum changes. Most states have no registered calculators, in which case
/// <see cref="IsSupported(UsState)"/> returns false and the UI should hide the
/// locality section entirely.
/// </para>
/// </summary>
public sealed class LocalCalculatorRegistry
{
    private readonly Dictionary<string, ILocalWithholdingCalculator> _byCode =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Register a calculator. Replaces any existing registration for the same <see cref="LocalityId.Code"/>.</summary>
    public void Register(ILocalWithholdingCalculator calculator)
    {
        _byCode[calculator.Locality.Code] = calculator;
    }

    /// <summary>Returns true when any locality calculator is registered for <paramref name="state"/>.</summary>
    public bool IsSupported(UsState state) =>
        _byCode.Values.Any(c => c.Locality.State == state);

    /// <summary>Tries to retrieve a calculator by locality <paramref name="localityCode"/>.</summary>
    public bool TryGetCalculator(string? localityCode, out ILocalWithholdingCalculator? calculator)
    {
        if (!string.IsNullOrWhiteSpace(localityCode)
            && _byCode.TryGetValue(localityCode, out var found))
        {
            calculator = found;
            return true;
        }

        calculator = null;
        return false;
    }

    /// <summary>
    /// Gets the calculator for <paramref name="localityCode"/>, throwing when none is registered.
    /// </summary>
    public ILocalWithholdingCalculator GetCalculator(string localityCode)
    {
        if (_byCode.TryGetValue(localityCode, out var calc))
            return calc;

        throw new KeyNotFoundException(
            $"No local withholding calculator registered for locality code '{localityCode}'.");
    }

    /// <summary>Returns every registered calculator whose locality is in <paramref name="state"/>, sorted by name.</summary>
    public IReadOnlyList<ILocalWithholdingCalculator> GetCalculatorsForState(UsState state) =>
        _byCode.Values
            .Where(c => c.Locality.State == state)
            .OrderBy(c => c.Locality.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Returns every registered calculator, sorted by state and then locality name.</summary>
    public IReadOnlyList<ILocalWithholdingCalculator> All =>
        _byCode.Values
            .OrderBy(c => c.Locality.State.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Locality.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
