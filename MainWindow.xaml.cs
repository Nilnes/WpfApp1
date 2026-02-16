using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

// DB : SalainenSana

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        // ---- RWS endpoints ----
        private const string UrlCtrlState = "http://127.0.0.1:8081/rw/panel/ctrlstate/";
        private const string UrlRapidExec = "http://127.0.0.1:8081/rw/rapid/execution";
        private const string UrlRobTarget =
            "http://127.0.0.1:8081/rw/motionsystem/mechunits/ROB_1/robtarget?tool=tool0&wobj=wobj0&coordinate=Base";

        // IO signals
        private const string UrlTcpSpeed = "http://127.0.0.1:8081/rw/iosystem/signals/AO_TCP_SPEED?json=1";
        private const string UrlGripper = "http://127.0.0.1:8081/rw/iosystem/signals/DI_Gripper1_Closed?json=1";

        private const string Username = "Default User";
        private const string Password = "robotics";

        // ---- HTTP ----
        private readonly HttpClient _client;

        // ---- Timer ----
        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private bool _isUpdating = false;

        // ---- Histories  ----
        private const int MaxPoints = 40;

        public ObservableCollection<HistoryItem> GripperHistory { get; } = new();
        public ObservableCollection<HistoryItem> SpeedHistory { get; } = new();
        public ObservableCollection<PosHistoryItem> PosHistory { get; } = new();

        public MainWindow()
        {
            InitializeComponent();

            // Bind lists
            GripperHistoryList.ItemsSource = GripperHistory;
            SpeedHistoryList.ItemsSource = SpeedHistory;
            PosHistoryList.ItemsSource = PosHistory;

            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential(Username, Password),
                PreAuthenticate = true,
                UseDefaultCredentials = false,
                AllowAutoRedirect = false
            };

            _client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            MotorsText.Text = "Haetaan…";
            ProgramText.Text = "Haetaan…";
            GripperTextUi.Text = "Haetaan…";
            SpeedTextUi.Text = "Haetaan…";
            PosTextUix.Text = "Haetaan…";

            // Timer: 1s
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += async (s, e) => await UpdateAllAsync();
            _timer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            _client.Dispose();
            base.OnClosed(e);
        }

        private async void RefreshNowBtn_Click(object sender, RoutedEventArgs e)
        {
            await UpdateAllAsync();
        }

        private async Task UpdateAllAsync()
        {
            if (_isUpdating) return;
            _isUpdating = true;
            RefreshNowBtn.IsEnabled = false;

            try
            {
                // 1) Motors
                var (ctrlOk, ctrlBody, _) = await SafeGetAsync(UrlCtrlState);
                MotorsText.Text = ctrlOk ? ToFinnishMotorState(ExtractSpan(ctrlBody, "ctrlstate")) : "Ei saatavilla";

                // 2) Program
                var (execOk, execBody, _) = await SafeGetAsync(UrlRapidExec);
                if (execOk)
                {
                    var raw = ExtractSpan(execBody, "ctrlexecstate") ??
                              ExtractSpan(execBody, "execstate") ??
                              ExtractSpan(execBody, "state");
                    ProgramText.Text = ToFinnishProgramState(raw);
                }
                else ProgramText.Text = "Ei saatavilla";

                // 3) Gripper (0/1)
                var (gOk, gJson, _) = await SafeGetAsync(UrlGripper);
                string gripRaw = gOk ? (ExtractJsonString(gJson, "lvalue") ?? ExtractJsonString(gJson, "value") ?? "") : "";
                var gripText = ToFinnishGripper(gripRaw);
                GripperTextUi.Text = gripText;

                // 4) Speed
                var (sOk, sJson, _) = await SafeGetAsync(UrlTcpSpeed);
                string speedRaw = sOk ? (ExtractJsonString(sJson, "lvalue") ?? ExtractJsonString(sJson, "value") ?? "") : "";
                SpeedTextUi.Text = FormatNum(speedRaw);

                // 5) Position X,Y,Z
                var (pOk, pHtml, _) = await SafeGetAsync(UrlRobTarget);
                double? px = null, py = null, pz = null;
                if (pOk)
                {
                    var x = ExtractSpan(pHtml, "x");
                    var y = ExtractSpan(pHtml, "y");
                    var z = ExtractSpan(pHtml, "z");

                    px = TryParseInvariant(x);
                    py = TryParseInvariant(y);
                    pz = TryParseInvariant(z);

                    PosTextUix.Text = (x != null)
                        ? $"X: {FormatNum(x)}"
                        : "Ei saatavilla";

                    PosTextUiy.Text = (y != null)
                        ? $"Y: {FormatNum(y)}"
                        : "Ei saatavilla";

                    PosTextUiz.Text = (z != null)
                        ? $"Z: {FormatNum(z)}"
                        : "Ei saatavilla";

                }
                else
                {
                    PosTextUix.Text = "Ei saatavilla";
                    PosTextUiy.Text = "Ei saatavilla";
                    PosTextUiz.Text = "Ei saatavilla";
                }

                // ---- Add history points ----
                var time = DateTime.Now.ToString("HH:mm:ss");

                // Gripper history (0/1)
                if (TryParseInvariant(gripRaw) is double gv)
                {
                    PushHistory(GripperHistory, new HistoryItem(time, gripText, gv));
                    DrawLineChart(GripperLine, GripperHistory, 110, normalize01: true);
                }

                // Speed history
                if (TryParseInvariant(speedRaw) is double sv)
                {
                    PushHistory(SpeedHistory, new HistoryItem(time, FormatNum(speedRaw), sv));
                    DrawLineChart(SpeedLine, SpeedHistory, 110, normalize01: false);
                }

                // Position history
                if (px.HasValue && py.HasValue && pz.HasValue)
                {
                    PushPosHistory(PosHistory, new PosHistoryItem(time,
                        FormatNum(px.Value.ToString(CultureInfo.InvariantCulture)),
                        FormatNum(py.Value.ToString(CultureInfo.InvariantCulture)),
                        FormatNum(pz.Value.ToString(CultureInfo.InvariantCulture)),
                        px.Value, py.Value, pz.Value));

                    DrawTripleChart(PosXLine, PosYLine, PosZLine, PosHistory, 110);
                }

                LastUpdateText.Text = $"Päivitetty: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                // Älä kaada UI:ta, näytä yleinen virhe
                SubTitleText.Text = ex.Message;
            }
            finally
            {
                RefreshNowBtn.IsEnabled = true;
                _isUpdating = false;
            }
        }

        // ----------------- Helpers -----------------

        private async Task<(bool ok, string body, string err)> SafeGetAsync(string url)
        {
            try
            {
                var resp = await _client.GetAsync(url);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    return (false, body, $"HTTP {(int)resp.StatusCode} {resp.StatusCode} @ {url}");
                return (true, body, "");
            }
            catch (Exception ex)
            {
                return (false, "", $"{ex.GetType().Name}: {ex.Message} @ {url}");
            }
        }

        private static string? ExtractSpan(string html, string className)
        {
            var m = Regex.Match(html, $@"<span class=""{Regex.Escape(className)}"">(.*?)</span>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static string? ExtractJsonString(string json, string key)
        {
            var m = Regex.Match(json, $@"""{Regex.Escape(key)}""\s*:\s*""([^""]*)""",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static double? TryParseInvariant(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            return null;
        }

        private static string FormatNum(string raw)
        {
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return d.ToString("0.###", new CultureInfo("fi-FI"));
            return string.IsNullOrWhiteSpace(raw) ? "—" : raw;
        }

        private static string ToFinnishMotorState(string? raw) =>
            raw?.ToLowerInvariant() switch
            {
                "motoron" => "Päällä (käyttövalmis)",
                "motoroff" => "Pois päältä",
                null or "" => "Ei saatavilla",
                _ => raw
            };

        private static string ToFinnishProgramState(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Ei saatavilla";
            var v = raw.Trim().ToLowerInvariant();
            return v switch
            {
                "running" => "Käynnissä",
                "stopped" => "Pysäytetty",
                "stop" => "Pysäytetty",
                "reset" => "Nollattu",
                _ => raw
            };
        }

        private static string ToFinnishGripper(string? raw) =>
            raw?.Trim() switch
            {
                "1" => "Kiinni",
                "0" => "Auki",
                null or "" => "Ei saatavilla",
                _ => raw
            };

        // ----------------- History + Charts -----------------

        private static void PushHistory(ObservableCollection<HistoryItem> list, HistoryItem item)
        {
            list.Insert(0, item);                // newest on top
            while (list.Count > MaxPoints) list.RemoveAt(list.Count - 1);
        }

        private static void PushPosHistory(ObservableCollection<PosHistoryItem> list, PosHistoryItem item)
        {
            list.Insert(0, item);
            while (list.Count > MaxPoints) list.RemoveAt(list.Count - 1);
        }

        private static void DrawLineChart(Polyline poly, ObservableCollection<HistoryItem> data, double height, bool normalize01)
        {
            poly.Points.Clear();
            if (data.Count < 2) return;

            // newest at index 0 -> draw left-to-right oldest->newest
            int n = data.Count;
            double width = 320; // arbitrary; WPF scales fine visually here

            // find min/max
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            for (int i = n - 1; i >= 0; i--)
            {
                var v = data[i].Numeric;
                if (normalize01)
                {
                    min = 0; max = 1;
                    break;
                }
                if (v < min) min = v;
                if (v > max) max = v;
            }
            if (double.IsInfinity(min) || double.IsInfinity(max) || Math.Abs(max - min) < 1e-9)
            {
                min -= 1;
                max += 1;
            }

            for (int i = n - 1; i >= 0; i--)
            {
                double x = (n - 1 - i) * (width / (n - 1));
                double v = data[i].Numeric;
                double yNorm = (v - min) / (max - min);
                double y = (1 - yNorm) * (height - 4) + 2;
                poly.Points.Add(new Point(x, y));
            }
        }

        private static void DrawTripleChart(Polyline xLine, Polyline yLine, Polyline zLine,
            ObservableCollection<PosHistoryItem> data, double height)
        {
            xLine.Points.Clear();
            yLine.Points.Clear();
            zLine.Points.Clear();
            if (data.Count < 2) return;

            int n = data.Count;
            double width = 320;

            // min/max across all three for nice scale
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            for (int i = n - 1; i >= 0; i--)
            {
                min = Math.Min(min, Math.Min(data[i].XN, Math.Min(data[i].YN, data[i].ZN)));
                max = Math.Max(max, Math.Max(data[i].XN, Math.Max(data[i].YN, data[i].ZN)));
            }
            if (Math.Abs(max - min) < 1e-9) { min -= 1; max += 1; }

            for (int i = n - 1; i >= 0; i--)
            {
                double x = (n - 1 - i) * (width / (n - 1));

                xLine.Points.Add(new Point(x, YOf(data[i].XN)));
                yLine.Points.Add(new Point(x, YOf(data[i].YN)));
                zLine.Points.Add(new Point(x, YOf(data[i].ZN)));
            }

            double YOf(double v)
            {
                double yNorm = (v - min) / (max - min);
                return (1 - yNorm) * (height - 4) + 2;
            }
        }
    }

    public record HistoryItem(string Time, string ValueText, double Numeric);

    public class PosHistoryItem
    {
        public string Time { get; }
        public string X { get; }
        public string Y { get; }
        public string Z { get; }

        public double XN { get; }
        public double YN { get; }
        public double ZN { get; }

        public PosHistoryItem(string time, string x, string y, string z, double xn, double yn, double zn)
        {
            Time = time;
            X = x; Y = y; Z = z;
            XN = xn; YN = yn; ZN = zn;
        }
    }
}