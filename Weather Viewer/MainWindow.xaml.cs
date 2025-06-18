using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WeatherApp
{
    public class Runway
    {
        public required string Designator { get; set; }
        public double TrueHeading { get; set; }
        public double Reciprocal => (TrueHeading + 180) % 360;
        public override string ToString() => $"{Designator} ({TrueHeading:F0}°)";
    }

    public partial class MainWindow : Window
    {
        private bool isMetric = true;
        private bool isKnots = false;

        private double lastTempC, lastWindKmh, lastWindDirectionDegrees;
        private string? lastAirportName,
                lastReportTime,
                lastWindDirectionText,
                lastVisibility,
                lastCloudCover,
                lastRawMetar,
                lastRawTaf,
                lastWindGust;

        private readonly List<Runway> runwayList = [];
        private Runway selectedRunway = new() { Designator = string.Empty, TrueHeading = 0 };
        public MainWindow()
        {
            InitializeComponent();
        }

        private void IcaoInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FetchWeather(sender, e);
                e.Handled = true;
            }
        }

        private async void FetchWeather(object sender, RoutedEventArgs e)
        {
            IcaoInput.IsEnabled = false;
            CompassCanvas.Visibility = Visibility.Collapsed;

            try
            {
                string icao = IcaoInput.Text.Trim().ToUpper();
                if (icao.Length != 4)
                {
                    string errorMsg = "Error: ICAO code must be exactly 4 characters.";
                    AirportNameOutput.Text = "";
                    ReportTimeOutput.Text = "";
                    TemperatureOutput.Text = "";
                    WindOutput.Text = "";
                    WindGustOutput.Text = "";
                    VisibilityOutput.Text = "";
                    CloudCoverOutput.Text = "";
                    RunwayComboBox.ItemsSource = null;
                    CompassCanvas.Visibility = Visibility.Collapsed;
                    RawMetarOutput.Text = errorMsg;
                    RawTafOutput.Text = errorMsg;
                    return;
                }

                JObject metarData = await FetchAviationWeather(icao);

                if (metarData.ContainsKey("error"))
                {
                    string errorMsg = metarData["error"]?.ToString() ?? "Unknown error";
                    AirportNameOutput.Text = "";
                    ReportTimeOutput.Text = "";
                    TemperatureOutput.Text = "";
                    WindOutput.Text = "";
                    WindGustOutput.Text = "";
                    VisibilityOutput.Text = "";
                    CloudCoverOutput.Text = "";
                    RunwayComboBox.ItemsSource = null;
                    CompassCanvas.Visibility = Visibility.Collapsed;
                    RawMetarOutput.Text = errorMsg;
                    RawTafOutput.Text = errorMsg;
                    return;
                }
                lastAirportName = metarData["name"]?.ToString() ?? icao;
                lastReportTime = metarData["reportTime"]?.ToString() ?? "Unknown";
                lastRawMetar = metarData["rawOb"]?.ToString() ?? "Unavailable";
                lastRawTaf = metarData["rawTaf"]?.ToString() ?? "Unavailable";
                lastTempC = metarData["temp"]?.ToObject<double>() ?? double.NaN;
                lastWindKmh = metarData["wspd"]?.ToObject<double>() ?? double.NaN;
                lastWindGust = metarData["wgst"]?.ToString() ?? "None";
                string windDirStr = metarData["wdir"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(windDirStr) &&
                    windDirStr.Equals("VRB", StringComparison.OrdinalIgnoreCase))
                {
                    lastWindDirectionDegrees = double.NaN;
                }
                else
                {
                    lastWindDirectionDegrees = metarData["wdir"]?.ToObject<double>() ?? double.NaN;
                }

                lastVisibility = metarData["visib"]?.ToString() ?? "Unknown";
                lastCloudCover = metarData["clouds"]?[0]?["cover"]?.ToString() ?? "Clear";
                lastWindDirectionText = ConvertWindDirection(lastWindDirectionDegrees);
                UpdateWeatherDisplay();
                await FetchRunwayData(icao);
                UpdateCompassElements();
                CompassCanvas.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                AirportNameOutput.Text = "";
                ReportTimeOutput.Text = "";
                TemperatureOutput.Text = "";
                WindOutput.Text = "";
                WindGustOutput.Text = "";
                VisibilityOutput.Text = "";
                CloudCoverOutput.Text = "";
                RunwayComboBox.ItemsSource = null;
                CompassCanvas.Visibility = Visibility.Collapsed;

                string errorMsg = $"Error loading data: {ex.Message}";
                RawMetarOutput.Text = errorMsg;
                RawTafOutput.Text = errorMsg;
            }
            finally
            {
                IcaoInput.IsEnabled = true;
            }
        }

        private static async Task<JObject> FetchAviationWeather(string icao)
        {
            string url = $"https://aviationweather.gov/api/data/metar?ids={icao}&format=json&taf=true";
            using var client = new HttpClient();
            try
            {
                client.DefaultRequestHeaders.Add("User-Agent", "WeatherApp/1.0");
                var response = await client.GetStringAsync(url);
                var json = JArray.Parse(response);
                return json.Count > 0 ? (JObject)json[0] : new JObject { ["error"] = "No METAR/TAF data." };
            }
            catch (Exception ex)
            {
                return new JObject { ["error"] = $"Error: {ex.Message}" };
            }
        }
        private async Task FetchRunwayData(string icao)
        {
            string url = $"https://aviationweather.gov/api/data/airport?ids={icao}&format=json";
            using var client = new HttpClient();
            try
            {
                client.DefaultRequestHeaders.Add("User-Agent", "WeatherApp/1.0");
                var response = await client.GetStringAsync(url);
                JArray airportArray = JArray.Parse(response);
                runwayList.Clear();
                RunwayComboBox.ItemsSource = null;
                if (airportArray.Count > 0)
                {
                    JObject airportData = (JObject)airportArray[0];
                    JArray runwaysArray = airportData["runways"] as JArray ?? new();
                    if (runwaysArray != null)
                    {
                        foreach (var r in runwaysArray)
                        {
                            string id = r["id"]?.ToString() ?? "";
                            string alignmentStr = r["alignment"]?.ToString() ?? "";
                            if (string.IsNullOrWhiteSpace(alignmentStr) || alignmentStr == "-")
                                continue;
                            if (double.TryParse(alignmentStr, out double heading))
                            {
                                var runway = new Runway
                                {
                                    Designator = id,
                                    TrueHeading = heading
                                };
                                runwayList.Add(runway);
                            }
                        }
                    }
                    RunwayComboBox.ItemsSource = runwayList;
                    if (runwayList.Count > 0)
                    {
                        RunwayComboBox.SelectedIndex = 0;
                        selectedRunway = runwayList[0];
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void UpdateWeatherDisplay()
        {
            AirportNameOutput.Text = lastAirportName;
            ReportTimeOutput.Text = $"Report Time: {lastReportTime}";
            TemperatureOutput.Text = isMetric
                ? $"Surface Temp: {lastTempC:F1}°C"
                : $"Surface Temp: {(lastTempC * 9 / 5 + 32):F1}°F";
            double windVal = isKnots ? lastWindKmh * 0.539957 : lastWindKmh;
            string windUnit = isKnots ? "knots" : isMetric ? "km/h" : "mph";
            WindOutput.Text = $"Wind: {windVal:F1} {windUnit}, {lastWindDirectionText} ({lastWindDirectionDegrees:F0}°)";
            WindGustOutput.Text = $"Gusts: {lastWindGust}";
            VisibilityOutput.Text = $"Visibility: {lastVisibility}";
            CloudCoverOutput.Text = $"Clouds: {lastCloudCover}";
            RawMetarOutput.Text = $"METAR: {lastRawMetar}";
            RawTafOutput.Text = $"TAF: {lastRawTaf}";

            if (isMetric)
            {
                UnitSwitch.Content = "Switch to Imperial";
            }
            else
            {
                UnitSwitch.Content = "Switch to Metric";
            }
            if (isKnots)
            {
                if (isMetric)
                {
                    WindUnitSwitch.Content = "Switch to km/h";
                }
                else
                {
                    WindUnitSwitch.Content = "Switch to mph";
                }
            }
            else
            {
                WindUnitSwitch.Content = "Switch to knots";
            }
        }

        private void UpdateCompassElements()
        {
            double centerX = 150;
            double centerY = 150;
            double runwayHeading = selectedRunway != null ? selectedRunway.TrueHeading : 310;
            var runwayRotation = new RotateTransform(runwayHeading, centerX, centerY);
            RunwayLine.RenderTransform = runwayRotation;
            double offset = 140;
            double rad = runwayHeading * Math.PI / 180;
            double bottomX = centerX - offset * Math.Sin(rad);
            double bottomY = centerY + offset * Math.Cos(rad);
            double topX = centerX + offset * Math.Sin(rad);
            double topY = centerY - offset * Math.Cos(rad);
            double labelOffsetX = 2, labelOffsetY = 2;
            Canvas.SetLeft(RunwayStartLabel, bottomX - labelOffsetX);
            Canvas.SetTop(RunwayStartLabel, bottomY - labelOffsetY);
            Canvas.SetLeft(RunwayEndLabel, topX - labelOffsetX);
            Canvas.SetTop(RunwayEndLabel, topY - labelOffsetY);
            if (selectedRunway != null && selectedRunway.Designator.Contains('/'))
            {
                var parts = selectedRunway.Designator.Split('/');
                if (parts.Length >= 2)
                {
                    RunwayStartLabel.Text = parts[0];
                    RunwayEndLabel.Text = parts[1];
                }
                else
                {
                    RunwayStartLabel.Text = selectedRunway.Designator;
                    RunwayEndLabel.Text = "";
                }
            }
            else
            {
                RunwayStartLabel.Text = selectedRunway?.Designator ?? "";
                RunwayEndLabel.Text = "";
            }
            double thresholdOffsetFactor = 0.8;
            double thresholdBottomX = centerX + thresholdOffsetFactor * (bottomX - centerX);
            double thresholdBottomY = centerY + thresholdOffsetFactor * (bottomY - centerY);
            double thresholdTopX = centerX + thresholdOffsetFactor * (topX - centerX);
            double thresholdTopY = centerY + thresholdOffsetFactor * (topY - centerY);
            double markerHalfLength = 10;
            double perpAngle = rad + Math.PI / 2;
            double perpX = markerHalfLength * Math.Cos(perpAngle);
            double perpY = markerHalfLength * Math.Sin(perpAngle);
            ThresholdStart.X1 = thresholdBottomX - perpX;
            ThresholdStart.Y1 = thresholdBottomY - perpY;
            ThresholdStart.X2 = thresholdBottomX + perpX;
            ThresholdStart.Y2 = thresholdBottomY + perpY;
            ThresholdEnd.X1 = thresholdTopX - perpX;
            ThresholdEnd.Y1 = thresholdTopY - perpY;
            ThresholdEnd.X2 = thresholdTopX + perpX;
            ThresholdEnd.Y2 = thresholdTopY + perpY;
            if (!double.IsNaN(lastWindDirectionDegrees))
            {
                double windAngle = (lastWindDirectionDegrees + 360) % 360;
                var windRotation = new RotateTransform(windAngle, centerX, centerY);
                WindArrow.RenderTransform = windRotation;
                WindArrowHead.RenderTransform = windRotation;
            }
        }

        private static string ConvertWindDirection(double degrees)
        {
            if (degrees == -1)
                return "Variable";
            string[] directions = ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
                                    "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" ];
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

        private void RunwayComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RunwayComboBox.SelectedItem is Runway selected)
            {
                selectedRunway = selected;
                UpdateCompassElements();
            }
        }
    }
}