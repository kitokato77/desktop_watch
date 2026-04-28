# Desktop Watch

Desktop Watch is a lightweight WPF widget for Windows that shows time, date, weather, and national holiday status. It is designed to stay unobtrusive while remaining visible on the desktop.

## Features
- Live clock and Indonesian locale date
- Weather via Open-Meteo (no API key required)
- Auto-refresh weather every 30 minutes
- Offline weather fallback (uses last saved fetch)
- National holidays via libur.deno.dev
- Quick position shortcuts (Shift + 1..6)
- City change (Shift + L)
- Autostart toggle (Shift + S)
- Persists window position and last city

## Requirements
- Windows
- .NET 8 SDK

## Build
```bash
dotnet build
```

## Run
```bash
dotnet run
```

## Usage
Use the shortcuts below to move the widget, change the city, or toggle autostart.

| Shortcut | Action |
| --- | --- |
| Shift + 1 | Move to top-left |
| Shift + 2 | Move to top-center |
| Shift + 3 | Move to top-right |
| Shift + 4 | Move to bottom-left |
| Shift + 5 | Move to bottom-center |
| Shift + 6 | Move to bottom-right |
| Shift + L | Change city |
| Shift + S | Toggle autostart |

## Weather tooltip
Hover the weather text to see whether the data is online or offline and the last update time.

## Data storage
Settings are saved at:
`%LOCALAPPDATA%\DesktopWatch\config.json`

Weather cache is saved at:
`%LOCALAPPDATA%\DesktopWatch\weather_cache.json`

## Autostart
Autostart uses the Windows Registry:
`HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`

## Notes
- Weather and holiday data require an internet connection. When offline, the app uses the last cached weather.
