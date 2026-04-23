using System.Net;
using System.Text.Json;
using Polly.CircuitBreaker;
using WeatherTestApp.Api.Models;

namespace WeatherTestApp.Api.Services
{
    public class WeatherApiClient : IWeatherApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WeatherApiClient> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public WeatherApiClient(HttpClient httpClient, ILogger<WeatherApiClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<WeatherApiResponse?> GetTemperatureAsync(int cityId, CancellationToken ct = default)
        {
            _logger.LogInformation("Calling WeatherAPI for cityId={CityId}", cityId);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync($"{cityId}", ct);
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(ex, "Circuit breaker is OPEN for cityId={CityId} — WeatherAPI requests are blocked", cityId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WeatherAPI call failed for cityId={CityId}", cityId);
                return null;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("WeatherAPI returned 404 for cityId={CityId}", cityId);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("WeatherAPI returned {StatusCode} for cityId={CityId}", response.StatusCode, cityId);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<WeatherApiResponse>(content, JsonOptions);

            _logger.LogInformation("WeatherAPI response for cityId={CityId}: {TempC}°C at {MeasuredAt}",
                cityId, result?.TemperatureC, result?.MeasuredAtUtc);

            return result;
        }
    }
}
