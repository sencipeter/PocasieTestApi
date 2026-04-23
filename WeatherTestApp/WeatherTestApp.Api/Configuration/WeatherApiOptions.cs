namespace WeatherTestApp.Api.Configuration
{
    public class WeatherApiOptions
    {
        public const string SectionName = "WeatherApi";
        public string BaseUrl { get; set; } = string.Empty;
    }
}
