using Microsoft.AspNetCore.Mvc;
using PaycheckCalc.Api.Dtos;
using PaycheckCalc.Api.Mapping;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Api.Controllers;

/// <summary>
/// Read-only reference endpoints the Angular front end uses to populate
/// pickers and dynamically render state-specific input forms. All data is
/// derived from <c>PaycheckCalc.Core</c> so the API and UI cannot drift.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class ReferenceController : ControllerBase
{
    private readonly StateCalculatorRegistry _stateRegistry;

    public ReferenceController(StateCalculatorRegistry stateRegistry)
    {
        _stateRegistry = stateRegistry;
    }

    /// <summary>Returns all supported US states (USPS two-letter codes).</summary>
    [HttpGet("states")]
    public ActionResult<IEnumerable<string>> States()
        => Ok(Enum.GetNames<UsState>());

    /// <summary>Returns the supported pay frequencies.</summary>
    [HttpGet("pay-frequencies")]
    public ActionResult<IEnumerable<string>> PayFrequencies()
        => Ok(Enum.GetNames<PayFrequency>());

    /// <summary>Returns the supported federal W-4 filing statuses.</summary>
    [HttpGet("federal-filing-statuses")]
    public ActionResult<IEnumerable<string>> FederalFilingStatuses()
        => Ok(Enum.GetNames<FederalFilingStatus>());

    /// <summary>Returns the supported deduction types and amount types.</summary>
    [HttpGet("deduction-options")]
    public ActionResult<object> DeductionOptions() => Ok(new
    {
        Types = Enum.GetNames<DeductionType>(),
        AmountTypes = Enum.GetNames<DeductionAmountType>()
    });

    /// <summary>
    /// Returns the dynamic input schema for the given state so the Angular
    /// client can render the correct controls (pickers, integers, toggles,
    /// etc.) for state-specific withholding inputs.
    /// </summary>
    [HttpGet("states/{state}/schema")]
    [ProducesResponseType(typeof(IEnumerable<StateFieldDefinitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IEnumerable<StateFieldDefinitionDto>> StateSchema(string state)
    {
        if (!Enum.TryParse<UsState>(state, ignoreCase: true, out var parsed))
            return NotFound($"Unknown state '{state}'.");

        var calculator = _stateRegistry.GetCalculator(parsed);
        var schema = calculator.GetInputSchema()
            .Select(f => f.ToDto())
            .ToArray();
        return Ok(schema);
    }
}
