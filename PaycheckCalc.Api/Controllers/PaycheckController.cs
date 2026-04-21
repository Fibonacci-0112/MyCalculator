using Microsoft.AspNetCore.Mvc;
using PaycheckCalc.Api.Dtos;
using PaycheckCalc.Api.Mapping;
using PaycheckCalc.Core.Pay;

namespace PaycheckCalc.Api.Controllers;

/// <summary>
/// Exposes <see cref="PayCalculator"/> as a single HTTP endpoint so the
/// Angular client can request a full paycheck calculation for one pay period.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class PaycheckController : ControllerBase
{
    private readonly PayCalculator _calculator;
    private readonly ILogger<PaycheckController> _logger;

    public PaycheckController(PayCalculator calculator, ILogger<PaycheckController> logger)
    {
        _calculator = calculator;
        _logger = logger;
    }

    /// <summary>
    /// Runs the full paycheck calculation pipeline (gross, FICA, federal,
    /// state, local, net) for the supplied inputs.
    /// </summary>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(PaycheckResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<PaycheckResultDto> Calculate([FromBody] PaycheckInputDto input)
    {
        if (input is null) return BadRequest("Request body is required.");

        try
        {
            var domain = input.ToDomain();
            var result = _calculator.Calculate(domain);
            return Ok(result.ToDto());
        }
        catch (ArgumentException ex)
        {
            // Core calculators raise ArgumentException for invalid enum / domain
            // values (e.g. unknown state). Surface those as 400s rather than 500s.
            _logger.LogWarning(ex, "Invalid paycheck input");
            return BadRequest(ex.Message);
        }
    }
}
