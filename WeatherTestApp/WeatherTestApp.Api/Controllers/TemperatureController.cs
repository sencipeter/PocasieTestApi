using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WeatherTestApp.Api.Services;

namespace WeatherTestApp.Api.Controllers
{
    /// <summary>
    /// Provides current temperature for supported cities.
    /// </summary>
    [ApiController]
    [Route("api/temperature")]
    [Authorize]
    [Produces("application/json")]
    public class TemperatureController : ControllerBase
    {
        private readonly ITemperatureService _temperatureService;
        private readonly ILogger<TemperatureController> _logger;

        public TemperatureController(ITemperatureService temperatureService, ILogger<TemperatureController> logger)
        {
            _temperatureService = temperatureService;
            _logger = logger;
        }

        /// <summary>
        /// Returns the current temperature for the specified city.
        /// </summary>
        /// <param name="cityId">Supported city IDs: 1=bratislava, 2=praha, 3=budapest, 4=vieden</param>
        /// <param name="ct">Cancellation token</param>
        /// <response code="200">Temperature returned successfully</response>
        /// <response code="404">City not supported</response>
        /// <response code="503">Upstream WeatherAPI unavailable and no cached value exists</response>
        [HttpGet("{cityId:int}")]
        [ProducesResponseType(typeof(Models.TemperatureResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetTemperature(int cityId, CancellationToken ct)
        {
            _logger.LogInformation("GET /api/temperature/{CityId} requested", cityId);

            var result = await _temperatureService.GetTemperatureAsync(cityId, ct);

            if (result is null)
            {
                // Distinguish between unknown cityId and upstream failure
                if (!Configuration.CityRegistry.TryGetCityName(cityId, out _))
                {
                    return Problem(
                        title: "City not found",
                        detail: $"CityId '{cityId}' is not supported. Supported: 1=bratislava, 2=praha, 3=budapest, 4=vieden.",
                        statusCode: StatusCodes.Status404NotFound);
                }

                return Problem(
                    title: "Service unavailable",
                    detail: "Weather data is temporarily unavailable and no cached value exists.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Ok(result);
        }
    }
}
