using System;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Globalization;
using System.Windows.Media;
using System.IO;
using Microsoft.Win32; // ID: wajib buat akses Registry | EN: needed for Registry access

namespace desktop_watch
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private const int DWMWA_EXCLUDED_FROM_PEEK = 12;
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        private static readonly HttpClient client = new HttpClient();
        private JsonArray? holidaysArray = null;
        private string lastCheckedDate = "";
        private string configPath = "";
        private string weatherCachePath = "";
        private string lastWeatherSource = "";
        private DateTimeOffset? lastWeatherUpdatedUtc = null;

        private double lat = -7.98;
        private double lon = 112.63;
        private string cityName = "Malang";

        public MainWindow()
        {
            
            InitializeComponent();
            
            // ID: simpan lokasi config | EN: save config location
            this.LocationChanged += (s, e) => SaveConfig();
            
            // --- ID: pola standar Windows (AppData) | EN: standard Windows layout (AppData) ---
            // ID: ambil alamat C:\Users\[NamaKamu]\AppData\Local | EN: grab the Local AppData path
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            
            // ID: bikin folder config: AppData\Local\DesktopWatch | EN: create config folder
            string appFolder = Path.Combine(localAppData, "DesktopWatch");
            
            // ID: kalau belum ada, buat dulu | EN: create it if missing
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            // ID: set alamat config final | EN: set final config path
            configPath = Path.Combine(appFolder, "config.json");
            weatherCachePath = Path.Combine(appFolder, "weather_cache.json");
            LoadConfig();

            _ = FetchHolidaysAsync();
            _ = FetchWeatherAsync();

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();

            DispatcherTimer weatherTimer = new DispatcherTimer();
            weatherTimer.Interval = TimeSpan.FromMinutes(30);
            weatherTimer.Tick += async (s, e) => await FetchWeatherAsync();
            weatherTimer.Start();

        }

        private void LoadConfig()
        {
            try {
                if (File.Exists(configPath)) {
                    string json = File.ReadAllText(configPath);
                    var doc = JsonNode.Parse(json);
                    if (doc != null) {
                        this.Left = (double)(doc["posLeft"] ?? this.Left);
                        this.Top = (double)(doc["posTop"] ?? this.Top);
                        lat = (double)(doc["lat"] ?? lat);
                        lon = (double)(doc["lon"] ?? lon);
                        cityName = doc["city"]?.ToString() ?? cityName;
                        this.WindowStartupLocation = WindowStartupLocation.Manual;
                    }
                }
            } catch { }
        }

        private void SaveConfig()
        {
            try {
                var data = new { posLeft = this.Left, posTop = this.Top, lat = lat, lon = lon, city = cityName };
                File.WriteAllText(configPath, JsonSerializer.Serialize(data));
            } catch { }
        }

        // --- ID: ambil cuaca | EN: fetch weather ---
        private async Task FetchWeatherAsync()
        {
            try {
                string url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code&timezone=Asia%2FJakarta";
                string response = await client.GetStringAsync(url);
                JsonNode? node = JsonNode.Parse(response);
                if (node != null) {
                    string? temp = node["current"]?["temperature_2m"]?.ToString();
                    int code = (int)(node["current"]?["weather_code"]?.GetValue<int>() ?? 0);
                    CuacaText.Text = $"{GetWeatherIcon(code)} {cityName}, {temp}°C";

                    lastWeatherSource = "online";
                    lastWeatherUpdatedUtc = DateTimeOffset.UtcNow;
                    UpdateWeatherTooltip();

                    SaveWeatherCache(new WeatherCache {
                        Temp = temp,
                        Code = code,
                        City = cityName,
                        UpdatedAtUtc = lastWeatherUpdatedUtc.Value
                    });
                }
            } catch {
                if (TryLoadWeatherCache(out WeatherCache? cached) && cached != null) {
                    CuacaText.Text = $"{GetWeatherIcon(cached.Code)} {cached.City}, {cached.Temp}°C (offline)";
                    lastWeatherSource = "offline";
                    lastWeatherUpdatedUtc = cached.UpdatedAtUtc;
                    UpdateWeatherTooltip();
                } else {
                    CuacaText.Text = "☁️ Cuaca Offline";
                    lastWeatherSource = "offline";
                    lastWeatherUpdatedUtc = null;
                    UpdateWeatherTooltip();
                }
            }
        }

        private void UpdateWeatherTooltip()
        {
            string timeText = lastWeatherUpdatedUtc.HasValue
                ? lastWeatherUpdatedUtc.Value.ToLocalTime().ToString("HH:mm")
                : "-";
            string sourceText = string.IsNullOrEmpty(lastWeatherSource) ? "unknown" : lastWeatherSource;
            CuacaText.ToolTip = $"Data: {sourceText} | Updated: {timeText}";
        }

        private sealed class WeatherCache
        {
            public string? Temp { get; set; }
            public int Code { get; set; }
            public string? City { get; set; }
            public DateTimeOffset UpdatedAtUtc { get; set; }
        }

        private void SaveWeatherCache(WeatherCache cache)
        {
            try {
                string json = JsonSerializer.Serialize(cache);
                File.WriteAllText(weatherCachePath, json);
            } catch { }
        }

        private bool TryLoadWeatherCache(out WeatherCache? cache)
        {
            cache = null;
            try {
                if (!File.Exists(weatherCachePath)) return false;
                string json = File.ReadAllText(weatherCachePath);
                cache = JsonSerializer.Deserialize<WeatherCache>(json);
                return cache != null;
            } catch { return false; }
        }

        private string GetWeatherIcon(int code) {
            if (code == 0) return "☀️";
            if (code <= 3) return "⛅";
            if (code == 45 || code == 48) return "🌫️";
            if (code <= 67) return "🌧️";
            return "⛈️";
        }

        // --- ID: ambil libur & update jam | EN: fetch holidays & update clock ---
        private async Task FetchHolidaysAsync()
        {
            try {
                string response = await client.GetStringAsync("https://libur.deno.dev/api");
                JsonNode? node = JsonNode.Parse(response);
                if (node != null) holidaysArray = node.AsArray();
            } catch { }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            JamText.Text = now.ToString("HH:mm:ss");
            TanggalText.Text = now.ToString("dddd, dd MMMM yyyy", new CultureInfo("id-ID"));

            string todayString = now.ToString("yyyy-MM-dd");
            if (holidaysArray != null && todayString != lastCheckedDate) {
                lastCheckedDate = todayString;
                bool isHolidayToday = false;
                string currentHolidayName = "";

                foreach (var item in holidaysArray) {
                    if (item == null) continue;
                    string? dateStr = item["holiday_date"]?.ToString();
                    string? isNationalStr = item["is_national_holiday"]?.ToString().ToLower();

                    if (dateStr != null && dateStr.StartsWith(todayString) && isNationalStr == "true") {
                        isHolidayToday = true;
                        currentHolidayName = item["holiday_name"]?.ToString() ?? "Hari Libur";
                        break;
                    }
                }

                if (isHolidayToday) {
                    JamText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4C4C"));
                    TanggalText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9999"));
                    LiburText.Text = currentHolidayName;
                    LiburText.Visibility = Visibility.Visible;
                } else {
                    JamText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0"));
                    TanggalText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
                    LiburText.Visibility = Visibility.Collapsed;
                }
            }
        }

        // --- ID: UX & shortcut | EN: UX & shortcuts ---
        private void Window_MouseDown(object? sender, MouseButtonEventArgs e)
        {
            // ID: cegah drag saat ngetik | EN: avoid drag while typing
            if (e.ChangedButton == MouseButton.Left && !CityTextBox.IsFocused) this.DragMove();
        }

        private void Window_KeyDown(object? sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift) {
                var workArea = SystemParameters.WorkArea;
                if (e.Key == Key.D1) { this.Left = workArea.Left; this.Top = workArea.Top; }
                else if (e.Key == Key.D2) { this.Left = workArea.Left + (workArea.Width/2) - (this.ActualWidth/2); this.Top = workArea.Top; }
                else if (e.Key == Key.D3) { this.Left = workArea.Right - this.ActualWidth; this.Top = workArea.Top; }
                else if (e.Key == Key.D4) { this.Left = workArea.Left; this.Top = workArea.Bottom - this.ActualHeight; }
                else if (e.Key == Key.D5) { this.Left = workArea.Left + (workArea.Width/2) - (this.ActualWidth/2); this.Top = workArea.Bottom - this.ActualHeight; }
                else if (e.Key == Key.D6) { this.Left = workArea.Right - this.ActualWidth; this.Top = workArea.Bottom - this.ActualHeight; }
                
                // ID: shortcut Shift+L (ganti lokasi) | EN: Shift+L (change location)
                else if (e.Key == Key.L) {
                    LocationInputBox.Visibility = Visibility.Visible;
                    CityTextBox.Text = cityName;
                    CityTextBox.Focus();
                    CityTextBox.SelectAll();
                }

                // ID: shortcut Shift+S (toggle autostart) | EN: Shift+S (toggle autostart)
                else if (e.Key == Key.S) {
                    ToggleStartup();
                }
                SaveConfig();
            }
        }

        // --- ID: cari kota lalu update | EN: search city then update ---
        private async void CityTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) {
                LocationInputBox.Visibility = Visibility.Collapsed;
                string searchCity = CityTextBox.Text.Trim();
                if (string.IsNullOrEmpty(searchCity)) return;

                CuacaText.Text = "⏳ Mencari kota...";
                
                try {
                    // ID: panggil API Geocoding Open-Meteo | EN: call Open-Meteo geocoding API
                    string geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(searchCity)}&count=1";
                    string geoResponse = await client.GetStringAsync(geoUrl);
                    JsonNode? geoNode = JsonNode.Parse(geoResponse);
                    var results = geoNode?["results"]?.AsArray();

                    if (results != null && results.Count > 0) {
                        lat = (double)results[0]?["latitude"]!;
                        lon = (double)results[0]?["longitude"]!;
                        cityName = results[0]?["name"]?.ToString() ?? searchCity;
                        
                        SaveConfig(); // ID: simpan config sekarang | EN: save config now
                        await FetchWeatherAsync(); // ID: update UI cuaca | EN: refresh weather UI
                    } else {
                        CuacaText.Text = "❌ Kota tak ditemukan";
                    }
                } catch {
                    CuacaText.Text = "⚠️ Gagal cari kota";
                }
            } 
            else if (e.Key == Key.Escape) {
                // ID: batal ganti kota | EN: cancel location change
                LocationInputBox.Visibility = Visibility.Collapsed;
            }
        }

        private void Window_SourceInitialized(object? sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, (extendedStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            // ID: cegah transparan saat Aero Peek | EN: prevent transparency during Aero Peek
            int excludeFromPeek = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_EXCLUDED_FROM_PEEK, ref excludeFromPeek, sizeof(int));
        }

        // --- ID: autostart via Registry | EN: autostart via Registry ---
        private async void ToggleStartup()
        {
            try
            {
                string appName = "DesktopWatch_App";
                // ID: ProcessPath otomatis baca lokasi .exe | EN: ProcessPath gets exe path
                string? appPath = Environment.ProcessPath; 
                
                // ID: buka akses Registry Windows | EN: open Windows Registry
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null && appPath != null)
                    {
                        // ID: cek sudah terdaftar atau belum | EN: check if already registered
                        if (key.GetValue(appName) == null)
                        {
                            // ID: belum ada, tambahin (pakai kutip biar aman) | EN: not there, add it (quote path)
                            key.SetValue(appName, $"\"{appPath}\"");
                            CuacaText.Text = "✅ Autostart AKTIF";
                        }
                        else
                        {
                            // ID: sudah ada, cabut | EN: already there, remove
                            key.DeleteValue(appName, false);
                            CuacaText.Text = "❌ Autostart DIMATIKAN";
                        }
                        
                        // ID: tampil 2 detik lalu balik ke cuaca | EN: show 2s then restore
                        await Task.Delay(2000);
                        await FetchWeatherAsync();
                    }
                }
            }
            catch
            {
                CuacaText.Text = "⚠️ Gagal atur Autostart";
                await Task.Delay(2000);
                await FetchWeatherAsync();
            }
        }
    }
}