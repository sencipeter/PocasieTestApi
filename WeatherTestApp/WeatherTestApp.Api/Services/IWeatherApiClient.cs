using WeatherTestApp.Api.Models;

namespace WeatherTestApp.Api.Services
{
    public interface IWeatherApiClient
    {
        Task<WeatherApiResponse?> GetTemperatureAsync(int cityId, CancellationToken ct = default);
    }
}
