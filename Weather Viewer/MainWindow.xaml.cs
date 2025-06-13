using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Windows;

namespace WeatherApp
{
    public partial class MainWindow : Window
    {
        private bool isMetric = true;
        private bool isKnots = false;
        private double lastTempC, lastPressureHpa, lastWindKmh, lastWindDirectionDegrees, lastDewPoint;
        private string lastWindDirectionText, lastVisibility, lastCloudCover, lastTafDecoded, lastRawMetar, lastRawTaf;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void FetchWeather(object sender, RoutedEventArgs e)
        {
            string icaoCode = IcaoInput.Text.Trim();
            if (string.IsNullOrEmpty(icaoCode)) return;

            JObject data = await FetchAviationWeather(icaoCode);

            if (data.ContainsKey("error"))
            {
                RawMetarOutput.Text = data["error"].ToString();
                RawTafOutput.Text = "";
                return;
            }

            // Extract METAR Data
            lastRawMetar = data["rawOb"]?.ToString() ?? "Unavailable";
            lastTempC = data["temp"]?.ToObject<double>() ?? double.NaN;
            lastDewPoint = data["dewp"]?.ToObject<double>() ?? double.NaN;
            lastPressureHpa = data["altim"]?.ToObject<double>() ?? double.NaN;
            lastVisibility = data["visib"]?.ToString() ?? "Unknown";
            lastWindKmh = data["wspd"]?.ToObject<double>() ?? double.NaN;
            lastWindDirectionDegrees = data["wdir"]?.ToObject<double>() ?? double.NaN;
            lastCloudCover = data["clouds"]?[0]?["cover"]?.ToString() ?? "Clear";
            lastWindDirectionText = ConvertWindDirection(lastWindDirectionDegrees);

            // Extract TAF Data
            lastRawTaf = data["rawTaf"]?.ToString() ?? "Unavailable";
            lastTafDecoded = $"Forecast: {lastRawTaf}"; // Could be formatted further

            UpdateWeatherDisplay();
        }

        private async Task<JObject> FetchAviationWeather(string icaoCode)
        {
            string apiUrl = $"https://aviationweather.gov/api/data/metar?ids={icaoCode}&format=json&taf=true";
            using HttpClient client = new HttpClient();

            try
            {
                client.DefaultRequestHeaders.Add("User-Agent", "WeatherApp/1.0 (contact@example.com)");
                var response = await client.GetStringAsync(apiUrl);
                JArray jsonArray = JArray.Parse(response);
                return jsonArray.Count > 0 ? (JObject)jsonArray[0] : new JObject { ["error"] = "No data available." };
            }
            catch (Exception ex)
            {
                return new JObject { ["error"] = $"Error fetching METAR/TAF: {ex.Message}" };
            }
        }

        private string ConvertWindDirection(double degrees)
        {
            if (double.IsNaN(degrees)) return "Unknown";

            string[] directions = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
                                    "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW", "N" };

            int index = (int)Math.Round(degrees / 22.5) % 16;
            return directions[index];
        }

        private void ToggleUnitFormat(object sender, RoutedEventArgs e)
        {
            isMetric = !isMetric;
            UpdateWeatherDisplay();
        }

        private void ToggleKnots(object sender, RoutedEventArgs e)
        {
            isKnots = !isKnots;
            UpdateWeatherDisplay();
        }

        private void UpdateWeatherDisplay()
        {
            VisibilityOutput.Text = $"Visibility: {lastVisibility}";
            RawMetarOutput.Text = $"METAR: {lastRawMetar}";
            RawTafOutput.Text = $"TAF: {lastRawTaf}";
            TafDecodedOutput.Text = lastTafDecoded;
        }
    }
}