using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Windows;
using System.Windows.Media;

namespace WeatherApp
{
    public partial class MainWindow : Window
    {
        private bool isMetric = true;
        private bool isKnots = false;
        private double lastTempC, lastPressureHpa, lastWindKmh, lastWindDirectionDegrees, lastDewPoint;
        private string lastAirportName, lastReportTime, lastWindDirectionText, lastVisibility, lastCloudCover, lastRawMetar, lastRawTaf, lastWindGust;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void FetchWeather(object sender, RoutedEventArgs e)
        {
            string icaoCode = IcaoInput.Text.Trim();
            if (string.IsNullOrEmpty(icaoCode))
                return;

            JObject data = await FetchAviationWeather(icaoCode);
            if (data.ContainsKey("error"))
            {
                RawMetarOutput.Text = data["error"].ToString();
                RawTafOutput.Text = "";
                return;
            }

            // Extract METAR Data
            lastAirportName = data["name"]?.ToString() ?? "Unknown Airport";
            lastReportTime = data["reportTime"]?.ToString() ?? "Unknown Time";
            lastRawMetar = data["rawOb"]?.ToString() ?? "Unavailable";
            lastTempC = data["temp"]?.ToObject<double>() ?? double.NaN;
            lastDewPoint = data["dewp"]?.ToObject<double>() ?? double.NaN;
            lastPressureHpa = data["altim"]?.ToObject<double>() ?? double.NaN;
            lastVisibility = data["visib"]?.ToString() ?? "Unknown";
            lastWindKmh = data["wspd"]?.ToObject<double>() ?? double.NaN;
            lastWindGust = data["wgst"]?.ToString() ?? "None";
            lastWindDirectionDegrees = data["wdir"]?.ToObject<double>() ?? double.NaN;
            lastCloudCover = data["clouds"]?[0]?["cover"]?.ToString() ?? "Clear";
            lastWindDirectionText = ConvertWindDirection(lastWindDirectionDegrees);

            // Extract TAF Data
            lastRawTaf = data["rawTaf"]?.ToString() ?? "Unavailable";

            UpdateWeatherDisplay();
            UpdateCompassElements();
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
            if (double.IsNaN(degrees))
                return "Unknown";

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
            // Update Decoded Weather Section (Column 2)
            AirportNameOutput.Text = lastAirportName;
            ReportTimeOutput.Text = $"Report Time: {lastReportTime}";
            TemperatureOutput.Text = $"Surface Temp: {lastTempC:F1}°C";
            // Now showing wind speed and direction (both text and degrees)
            WindOutput.Text = $"Wind: {lastWindKmh:F1} km/h, {lastWindDirectionText} ({lastWindDirectionDegrees:F0}°)";
            WindGustOutput.Text = $"Gusts: {lastWindGust}";
            VisibilityOutput.Text = $"Visibility: {lastVisibility}";
            CloudCoverOutput.Text = $"Clouds: {lastCloudCover}";

            // Update Raw METAR & TAF Sections (Columns 3 & 4)
            RawMetarOutput.Text = $"METAR: {lastRawMetar}";
            RawTafOutput.Text = $"TAF: {lastRawTaf}";
        }

        private void UpdateCompassElements()
        {
            // Example: updating runway based on a provided heading.
            // Here we assume a runway heading (e.g., 310° for runway 31, reciprocal 130° for runway 13).
            double runwayHeading = 310; // This value may be replaced with real data if available.
            if (RunwayVisual.RenderTransform is RotateTransform rt)
            {
                rt.Angle = runwayHeading;
            }
            else
            {
                RunwayVisual.RenderTransform = new RotateTransform(runwayHeading);
            }

            // Set runway labels based on heading (dividing by 10 and formatting as two digits)
            RunwayStartLabel.Text = (runwayHeading / 10).ToString("00");
            double reciprocalHeading = (runwayHeading + 180) % 360;
            RunwayEndLabel.Text = (reciprocalHeading / 10).ToString("00");

            // Update the wind arrow based on wind direction data.
            double windAngle = lastWindDirectionDegrees;
            RotateTransform arrowTransform = new RotateTransform(windAngle, WindArrow.X1, WindArrow.Y1);
            WindArrow.RenderTransform = arrowTransform;
            RotateTransform arrowHeadTransform = new RotateTransform(windAngle, 150, 50);
            WindArrowHead.RenderTransform = arrowHeadTransform;
        }
    }
}