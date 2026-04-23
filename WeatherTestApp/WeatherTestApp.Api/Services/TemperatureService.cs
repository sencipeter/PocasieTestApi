using Microsoft.Extensions.Caching.Memory;
using WeatherTestApp.Api.Configuration;
using WeatherTestApp.Api.Models;

namespace WeatherTestApp.Api.Services
{
    /// <summary>
    /// Fetches temperature from WeatherAPI and caches the result until the next scheduled
    /// refresh window (09:00 or 16:00 UTC). If WeatherAPI is unavailable, the last known
    /// value is returned from cache.
    /// </summary>
    public class TemperatureService : ITemperatureService
    {
        // Scheduled refresh times (UTC)
        private static readonly TimeOnly[] RefreshTimes =
        [
            new TimeOnly(9, 0),
            new TimeOnly(16, 0)
        ];

        private readonly IWeatherApiClient _weatherApiClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TemperatureService> _logger;

        // Separate lock per city to avoid blocking all cities at once
        private static readonly Dictionary<string, SemaphoreSlim> Locks =
            CityRegistry.SupportedCities.ToDictionary(
                c => c,
                _ => new SemaphoreSlim(1, 1),
                StringComparer.OrdinalIgnoreCase);

        public TemperatureService(
            IWeatherApiClient weatherApiClient,
            IMemoryCache cache,
            ILogger<TemperatureService> logger)
        {
            _weatherApiClient = weatherApiClient;
            _cache = cache;
            _logger = logger;
        }

        public async Task<TemperatureResponse?> GetTemperatureAsync(int cityId, CancellationToken ct = default)
        {
            if (!CityRegistry.TryGetCityName(cityId, out var city))
            {
                _logger.LogWarning("Unsupported cityId requested: {CityId}", cityId);
                return null;
            }

            var cacheKey = $"temp:{city.ToLowerInvariant()}";

            // Fast path — cache hit
            if (_cache.TryGetValue(cacheKey, out TemperatureResponse? cached))
            {
                _logger.LogDebug("Cache hit for city={City}", city);
                return cached;
            }

            var semaphore = Locks[city];
            await semaphore.WaitAsync(ct);
            try
            {
                // Double-check after acquiring lock
                if (_cache.TryGetValue(cacheKey, out cached))
                    return cached;

                var apiResponse = await _weatherApiClient.GetTemperatureAsync(cityId, ct);

                if (apiResponse is null)
                {
                    _logger.LogWarning("WeatherAPI unavailable for city={City}, no cached value available", city);
                    return null;
                }

                var response = new TemperatureResponse
                {
                    City = city.ToLowerInvariant(),
                    TemperatureC = Math.Round(apiResponse.TemperatureC, 2),
                    MeasuredAtUtc = apiResponse.MeasuredAtUtc
                };

                var expiry = GetNextRefreshTime();
                _logger.LogInformation("Caching temperature for city={City} until {Expiry} UTC", city, expiry);

                _cache.Set(cacheKey, response, expiry);
                return response;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Calculates cache expiry as the next 09:00 or 16:00 UTC from now.
        /// </summary>
        private static DateTimeOffset GetNextRefreshTime()
        {
            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var currentTime = TimeOnly.FromDateTime(now);

            foreach (var t in RefreshTimes)
            {
                if (currentTime < t)
                    return new DateTimeOffset(today.ToDateTime(t), TimeSpan.Zero);
            }

            // Past 16:00 — next is 09:00 tomorrow
            return new DateTimeOffset(today.AddDays(1).ToDateTime(RefreshTimes[0]), TimeSpan.Zero);
        }
    }
}
