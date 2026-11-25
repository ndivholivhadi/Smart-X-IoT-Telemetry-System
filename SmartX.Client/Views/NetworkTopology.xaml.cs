using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SmartX.Client.Views
{
    public partial class NetworkTopology : UserControl
    {
        private static readonly HttpClient Client = new()
        {
            BaseAddress = new Uri("http://localhost:5090/")
        };

        private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private const string DateFormat = "yyyy-MM-dd HH:mm";

        private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(5) };

        private TopologySnapshotDto? _snapshot;
        private RouteTraceDto? _trace;
        private string? _selectedId;
        private bool _refreshing;

        public NetworkTopology()
        {
            InitializeComponent();

            _refreshTimer.Tick += RefreshTimer_Tick;
            Loaded += NetworkTopology_Loaded;
            Unloaded += NetworkTopology_Unloaded;
        }

        // ------------------------------------------------------------------
        // Lifecycle: the timer only runs while this page is on screen
        // ------------------------------------------------------------------

        private async void NetworkTopology_Loaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(HistoryFromBox.Text))
                SetQuickRange(TimeSpan.FromHours(1));

            _refreshTimer.Start();
            await RefreshAsync();
        }

        private void NetworkTopology_Unloaded(object sender, RoutedEventArgs e)
        {
            _refreshTimer.Stop();
        }

        private async void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            if (AutoRefreshCheckBox.IsChecked == true)
                await RefreshAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.ShowMainMenu();
        }

        // ------------------------------------------------------------------
        // Loading the network
        // ------------------------------------------------------------------

        private async Task RefreshAsync()
        {
            if (_refreshing)
                return;

            _refreshing = true;

            try
            {
                var snapshot = await Client.GetFromJsonAsync<TopologySnapshotDto>("api/topology", JsonOptions);
                var connected = await Client.GetFromJsonAsync<ConnectivityReportDto>("api/topology/connected", JsonOptions);

                if (snapshot == null)
                    return;

                _snapshot = snapshot;
                ShowSummary(snapshot);
                ConnectedGrid.ItemsSource = connected?.Connected ?? new List<ConnectedNodeDto>();
                ShowHealth(snapshot);
                DrawGraph();

                StatusText.Text = $"Updated {DateTime.Now:HH:mm:ss}. " +
                                  $"{snapshot.Nodes.Count - 1} sensors, {snapshot.Edges.Count} links.";
            }
            catch (HttpRequestException)
            {
                StatusText.Text = "Could not reach the API at http://localhost:5090. Is SmartX.API running?";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Could not load the network: {ex.Message}";
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void ShowSummary(TopologySnapshotDto snapshot)
        {
            var sensors = snapshot.Nodes.Where(n => !n.IsGateway).ToList();

            TotalDevicesText.Text = sensors.Count.ToString();
            OnlineDevicesText.Text = sensors.Count(n => n.Status != "Offline").ToString();
            OfflineDevicesText.Text = sensors.Count(n => n.Status == "Offline").ToString();
            ReachableText.Text = snapshot.ReachableCount.ToString();
            MstCostText.Text = $"{snapshot.MstTotalCost:0} mW";

            EmptyNetworkText.Visibility = sensors.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowHealth(TopologySnapshotDto snapshot)
        {
            if (snapshot.DownNodes.Count == 0 && snapshot.StrandedNodes.Count == 0)
            {
                HealthSummaryText.Text = "All sensors are connected. Every route is working.";
            }
            else
            {
                var parts = new List<string>();

                if (snapshot.DownNodes.Count > 0)
                    parts.Add($"Down: {string.Join(", ", snapshot.DownNodes)}.");

                if (snapshot.StrandedNodes.Count > 0)
                    parts.Add($"Cut off from the gateway: {string.Join(", ", snapshot.StrandedNodes)}.");

                HealthSummaryText.Text = string.Join(" ", parts);
            }

            ReroutesList.ItemsSource = snapshot.Reroutes
                .Select(r => new RerouteRow
                {
                    Title = $"{r.Node} - {r.State}",
                    Message = r.Message,
                    Color = r.State == "Rerouted" ? "#E67E22" : "#C0392B"
                })
                .ToList();
        }

        // ------------------------------------------------------------------
        // Drawing the graph
        // ------------------------------------------------------------------

        private static SolidColorBrush Paint(string hex)
            => new((Color)ColorConverter.ConvertFromString(hex));

        private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawGraph();

        private void DrawGraph()
        {
            GraphCanvas.Children.Clear();

            if (_snapshot == null || GraphCanvas.ActualWidth < 100 || GraphCanvas.ActualHeight < 100)
                return;

            double width = GraphCanvas.ActualWidth;
            double height = GraphCanvas.ActualHeight;
            var centre = new Point(width / 2, height / 2);
            double radius = Math.Max(40, Math.Min(width, height) / 2 - 52);

            // Gateway in the middle, sensors round a circle (grouped by zone, as the API sorts them).
            var positions = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
            var sensors = _snapshot.Nodes.Where(n => !n.IsGateway).ToList();

            for (int i = 0; i < sensors.Count; i++)
            {
                double angle = 2 * Math.PI * i / Math.Max(1, sensors.Count) - Math.PI / 2;
                positions[sensors[i].Id] = new Point(
                    centre.X + radius * Math.Cos(angle),
                    centre.Y + radius * Math.Sin(angle));
            }

            positions[_snapshot.GatewayId] = centre;

            var tracedLinks = TracedLinkKeys();

            // Links first, so the nodes are drawn on top of them.
            foreach (var edge in _snapshot.Edges.OrderBy(e => e.InMst))
            {
                if (!positions.TryGetValue(edge.From, out var a) || !positions.TryGetValue(edge.To, out var b))
                    continue;

                bool traced = tracedLinks.Contains(LinkKey(edge.From, edge.To));

                var line = new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y };

                if (traced)
                {
                    line.Stroke = Paint("#F39C12");
                    line.StrokeThickness = 4.5;
                    Panel.SetZIndex(line, 2);
                }
                else if (!edge.Active)
                {
                    line.Stroke = Paint("#E6B0AA");
                    line.StrokeThickness = 1;
                    line.StrokeDashArray = new DoubleCollection { 4, 3 };
                }
                else if (edge.InMst)
                {
                    line.Stroke = Paint("#2457C5");
                    line.StrokeThickness = 2.5;
                    Panel.SetZIndex(line, 1);
                }
                else
                {
                    line.Stroke = Paint("#C5CEDA");
                    line.StrokeThickness = 1;
                }

                GraphCanvas.Children.Add(line);

                if (traced || edge.InMst)
                {
                    var cost = new TextBlock
                    {
                        Text = edge.Cost.ToString("0"),
                        FontSize = 9,
                        Foreground = Paint(traced ? "#B9770E" : "#2457C5"),
                        Background = Paint("#E0FFFFFF")
                    };

                    Canvas.SetLeft(cost, (a.X + b.X) / 2 - 5);
                    Canvas.SetTop(cost, (a.Y + b.Y) / 2 - 7);
                    Panel.SetZIndex(cost, 3);
                    GraphCanvas.Children.Add(cost);
                }
            }

            foreach (var node in _snapshot.Nodes)
            {
                if (!positions.TryGetValue(node.Id, out var p))
                    continue;

                double size = node.IsGateway ? 44 : 30;
                bool selected = string.Equals(node.Id, _selectedId, StringComparison.OrdinalIgnoreCase);

                var circle = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = Paint(NodeColour(node)),
                    Stroke = Paint(selected ? "#F39C12" : "#FFFFFF"),
                    StrokeThickness = selected ? 4 : 2,
                    Cursor = node.IsGateway ? Cursors.Arrow : Cursors.Hand,
                    ToolTip = $"{node.Id}\nMAC {node.Mac}\nZone {node.Zone}\nStatus {node.Status}" +
                              (node.HopsToGateway.HasValue ? $"\n{node.HopsToGateway} hop(s) to the gateway" : "")
                };

                if (!node.IsGateway)
                {
                    string id = node.Id;
                    circle.MouseLeftButtonUp += async (_, _) => await TraceAsync(id);
                }

                Canvas.SetLeft(circle, p.X - size / 2);
                Canvas.SetTop(circle, p.Y - size / 2);
                Panel.SetZIndex(circle, 4);
                GraphCanvas.Children.Add(circle);

                var label = new TextBlock
                {
                    Text = node.IsGateway ? "GATEWAY" : node.Id,
                    Width = 90,
                    FontSize = 10,
                    TextAlignment = TextAlignment.Center,
                    Foreground = Paint("#172B4D")
                };

                Canvas.SetLeft(label, p.X - 45);
                Canvas.SetTop(label, p.Y + size / 2 + 2);
                Panel.SetZIndex(label, 4);
                GraphCanvas.Children.Add(label);
            }
        }

        private static string NodeColour(TopologyNodeDto node)
        {
            if (node.IsGateway) return "#2457C5";

            return node.Status switch
            {
                "Offline" => "#C0392B",
                "Unstable" => "#E67E22",
                _ => "#16803C"
            };
        }

        private HashSet<string> TracedLinkKeys()
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (_trace is { Found: true } && _trace.PowerEfficientPath.Count > 1)
            {
                for (int i = 0; i < _trace.PowerEfficientPath.Count - 1; i++)
                    keys.Add(LinkKey(_trace.PowerEfficientPath[i], _trace.PowerEfficientPath[i + 1]));
            }

            return keys;
        }

        private static string LinkKey(string a, string b)
            => string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0 ? $"{a}|{b}" : $"{b}|{a}";

        // ------------------------------------------------------------------
        // Tracking a sensor
        // ------------------------------------------------------------------

        private async void Trace_Click(object sender, RoutedEventArgs e) => await TraceAsync(TraceInput.Text);

        private async void TraceInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await TraceAsync(TraceInput.Text);
        }

        private async Task TraceAsync(string identifier)
        {
            identifier = identifier?.Trim() ?? string.Empty;

            if (identifier.Length == 0)
            {
                TraceResultText.Text = "Enter a device identifier or MAC address first.";
                return;
            }

            try
            {
                var trace = await Client.GetFromJsonAsync<RouteTraceDto>(
                    $"api/topology/route?identifier={Uri.EscapeDataString(identifier)}", JsonOptions);

                if (trace == null)
                    return;

                _trace = trace;
                _selectedId = trace.Found ? trace.NodeId : null;
                TraceInput.Text = trace.Found ? trace.NodeId ?? identifier : identifier;

                if (trace.Found && !string.IsNullOrEmpty(trace.NodeId))
                    HistoryDeviceBox.Text = trace.NodeId;

                TraceResultText.Text = FormatTrace(trace);
                DrawGraph();
            }
            catch (Exception ex)
            {
                TraceResultText.Text = $"Could not trace the route: {ex.Message}";
            }
        }

        private static string FormatTrace(RouteTraceDto trace)
        {
            if (!trace.Found)
                return trace.Message;

            var lines = new List<string>
            {
                $"{trace.NodeId}  ({trace.Mac})",
                $"Status: {trace.Status}"
            };

            if (trace.FewestHopsPath.Count > 0)
                lines.Add($"Fewest hops (BFS): {trace.Hops} - {string.Join(" → ", trace.FewestHopsPath)}");

            if (trace.PowerEfficientPath.Count > 0)
                lines.Add($"Lowest power (MST): {trace.PowerCost:0} mW - {string.Join(" → ", trace.PowerEfficientPath)}");

            lines.Add(trace.Message);
            return string.Join(Environment.NewLine + Environment.NewLine, lines);
        }

        // ------------------------------------------------------------------
        // Failure simulation (uses the endpoints from Part 1)
        // ------------------------------------------------------------------

        private async void SimulateFailure_Click(object sender, RoutedEventArgs e)
            => await SendConnectionCommandAsync("disconnect",
                "The sensor will be marked offline within about 5 seconds. Watch the map re-route.");

        private async void Restore_Click(object sender, RoutedEventArgs e)
            => await SendConnectionCommandAsync("reconnect", "The sensor is back online.");

        private async Task SendConnectionCommandAsync(string action, string successMessage)
        {
            var node = _snapshot?.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, _selectedId, StringComparison.OrdinalIgnoreCase));

            if (node?.SensorId == null)
            {
                StatusText.Text = "Track or click a sensor first.";
                return;
            }

            try
            {
                var response = await Client.PostAsync($"api/SensorConnectionSimulation/{node.SensorId}/{action}", null);
                StatusText.Text = response.IsSuccessStatusCode
                    ? $"{node.Id}: {successMessage}"
                    : $"The request failed (HTTP {(int)response.StatusCode}).";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Could not send the request: {ex.Message}";
            }
        }

        private async void SeedDemo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var response = await Client.PostAsync("api/topology/seed-demo", null);
                StatusText.Text = response.IsSuccessStatusCode
                    ? "Demo sensors registered."
                    : $"The request failed (HTTP {(int)response.StatusCode}).";

                await RefreshAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Could not load the demo network: {ex.Message}";
            }
        }

        // ------------------------------------------------------------------
        // Telemetry history (AVL tree range query on the API)
        // ------------------------------------------------------------------

        private void SetQuickRange(TimeSpan span)
        {
            HistoryToBox.Text = DateTime.Now.ToString(DateFormat);
            HistoryFromBox.Text = DateTime.Now.Subtract(span).ToString(DateFormat);
        }

        private async void Quick15_Click(object sender, RoutedEventArgs e)
        {
            SetQuickRange(TimeSpan.FromMinutes(15));
            await RunHistoryAsync();
        }

        private async void QuickHour_Click(object sender, RoutedEventArgs e)
        {
            SetQuickRange(TimeSpan.FromHours(1));
            await RunHistoryAsync();
        }

        private async void QuickDay_Click(object sender, RoutedEventArgs e)
        {
            SetQuickRange(TimeSpan.FromHours(24));
            await RunHistoryAsync();
        }

        private async void RunHistory_Click(object sender, RoutedEventArgs e) => await RunHistoryAsync();

        private async Task RunHistoryAsync()
        {
            var device = HistoryDeviceBox.Text.Trim();

            if (device.Length == 0)
            {
                HistoryStatusText.Text = "Enter a sensor identifier or MAC address, or click a node on the map.";
                return;
            }

            if (!TryParseLocal(HistoryFromBox.Text, out var from) || !TryParseLocal(HistoryToBox.Text, out var to))
            {
                HistoryStatusText.Text = $"Enter dates like {DateTime.Now.ToString(DateFormat)}.";
                return;
            }

            string metric = HistoryMetricBox.SelectedIndex switch { 1 => "power", 2 => "valve", _ => "soil" };

            var url = "api/topology/history" +
                      $"?identifier={Uri.EscapeDataString(device)}" +
                      $"&metric={metric}" +
                      $"&from={Uri.EscapeDataString(from.ToString("o"))}" +
                      $"&to={Uri.EscapeDataString(to.ToString("o"))}";

            try
            {
                var response = await Client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    HistoryStatusText.Text = $"The query failed (HTTP {(int)response.StatusCode}).";
                    return;
                }

                var result = await response.Content.ReadFromJsonAsync<HistoryQueryResultDto>(JsonOptions);

                if (result == null)
                    return;

                HistoryGrid.ItemsSource = result.Readings
                    .Select(r => new HistoryRow
                    {
                        Time = r.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                        Value = r.Value,
                        Unit = r.Unit
                    })
                    .ToList();

                HistoryStatusText.Text =
                    $"{result.ResultsFound} of {result.TotalStored} stored readings for {result.DeviceIdentifier} " +
                    $"found in {result.SearchTimeMicroseconds} µs using an {result.Algorithm} (tree height {result.TreeHeight}).";
            }
            catch (Exception ex)
            {
                HistoryStatusText.Text = $"Could not run the query: {ex.Message}";
            }
        }

        private static bool TryParseLocal(string text, out DateTimeOffset value)
        {
            if (DateTime.TryParseExact(text.Trim(), DateFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
            {
                value = new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Local));
                return true;
            }

            value = default;
            return false;
        }

        // ------------------------------------------------------------------
        // Shapes of the JSON the API returns
        // ------------------------------------------------------------------

        public sealed class TopologyNodeDto
        {
            public string Id { get; set; } = "";
            public string Mac { get; set; } = "";
            public string Zone { get; set; } = "";
            public string Status { get; set; } = "";
            public bool IsGateway { get; set; }
            public Guid? SensorId { get; set; }
            public bool Reachable { get; set; }
            public int? HopsToGateway { get; set; }
        }

        public sealed class TopologyEdgeDto
        {
            public string From { get; set; } = "";
            public string To { get; set; } = "";
            public double Cost { get; set; }
            public bool InMst { get; set; }
            public bool Active { get; set; }
        }

        public sealed class RerouteDto
        {
            public string Node { get; set; } = "";
            public string State { get; set; } = "";
            public string Message { get; set; } = "";
        }

        public sealed class TopologySnapshotDto
        {
            public string GatewayId { get; set; } = "";
            public List<TopologyNodeDto> Nodes { get; set; } = new();
            public List<TopologyEdgeDto> Edges { get; set; } = new();
            public double MstTotalCost { get; set; }
            public int ReachableCount { get; set; }
            public List<string> DownNodes { get; set; } = new();
            public List<string> StrandedNodes { get; set; } = new();
            public List<RerouteDto> Reroutes { get; set; } = new();
        }

        public sealed class RouteTraceDto
        {
            public bool Found { get; set; }
            public string? NodeId { get; set; }
            public string? Mac { get; set; }
            public string? Status { get; set; }
            public List<string> FewestHopsPath { get; set; } = new();
            public int Hops { get; set; }
            public List<string> PowerEfficientPath { get; set; } = new();
            public double PowerCost { get; set; }
            public string Message { get; set; } = "";
        }

        public sealed class ConnectedNodeDto
        {
            public string Id { get; set; } = "";
            public string Mac { get; set; } = "";
            public string Zone { get; set; } = "";
            public int Hops { get; set; }
            public string? Via { get; set; }
        }

        public sealed class ConnectivityReportDto
        {
            public List<ConnectedNodeDto> Connected { get; set; } = new();
        }

        public sealed class HistoryReadingDto
        {
            public DateTime Timestamp { get; set; }
            public string Value { get; set; } = "";
            public string Unit { get; set; } = "";
        }

        public sealed class HistoryQueryResultDto
        {
            public string DeviceIdentifier { get; set; } = "";
            public string Algorithm { get; set; } = "";
            public int ResultsFound { get; set; }
            public int TotalStored { get; set; }
            public int TreeHeight { get; set; }
            public long SearchTimeMicroseconds { get; set; }
            public List<HistoryReadingDto> Readings { get; set; } = new();
        }

        public sealed class HistoryRow
        {
            public string Time { get; set; } = "";
            public string Value { get; set; } = "";
            public string Unit { get; set; } = "";
        }

        public sealed class RerouteRow
        {
            public string Title { get; set; } = "";
            public string Message { get; set; } = "";
            public string Color { get; set; } = "#C0392B";
        }
    }
}
