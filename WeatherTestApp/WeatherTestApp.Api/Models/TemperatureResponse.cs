namespace WeatherTestApp.Api.Models
{
    public class TemperatureResponse
    {
        public string City { get; init; } = string.Empty;
        public double TemperatureC { get; init; }
        public DateTime MeasuredAtUtc { get; init; }
    }
}
