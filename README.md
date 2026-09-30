# Weather Viewer (METAR / TAF)

A Windows desktop app that pulls live aviation weather for any airport. Enter an ICAO code (like `KEWR`) to see the current METAR and TAF.

## Features
- Live METAR and TAF from the [aviationweather.gov](https://aviationweather.gov) public API
- Airport lookup by ICAO code
- Wind direction display with a compass rose
- Toggle between metric and imperial units, with wind speed in km/h, mph, or knots

## Build and run
Requires Windows and the .NET 9 SDK.
```bash
dotnet run --project "Weather Viewer"
```
Or open `Weather Viewer.sln` in Visual Studio and press F5.

## Tech
C# · WPF · .NET 9 · Newtonsoft.Json · aviationweather.gov Data API

> Not for real-world flight planning. Always use official sources.
