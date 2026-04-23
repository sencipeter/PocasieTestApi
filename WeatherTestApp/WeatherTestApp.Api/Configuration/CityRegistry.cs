namespace WeatherTestApp.Api.Configuration
{
    public class CityRegistry
    {
        private static readonly Dictionary<int, string> Cities = new()
        {
            { 1, "bratislava" },
            { 2, "praha"      },
            { 3, "budapest"   },
            { 4, "vieden"     }
        };      
        

        public static bool TryGetCityName(int cityId, out string cityName)
            => Cities.TryGetValue(cityId, out cityName!);

        public static IEnumerable<string> SupportedCities => Cities.Values;
    }
}
