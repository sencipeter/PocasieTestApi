using WeatherTestApp.Api.Models;

namespace WeatherTestApp.Api.Services
{
    public interface ITemperatureService
    {
        Task<TemperatureResponse?> GetTemperatureAsync(int cityId, CancellationToken ct = default);
    }
}
