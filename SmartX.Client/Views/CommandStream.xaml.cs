
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SmartX.Client.Views
{
    public partial class CommandStream : UserControl
    {
        private static readonly HttpClient Client = new()
        {
            BaseAddress = new Uri("http://localhost:5090/")
        };

        private readonly List<CommandItem> _allCommands = new();

        private readonly List<LiveTelemetryItem> _allLiveTelemetry = new();
        private readonly HashSet<string> _activeAlertDevices = new(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan ResolvedAlertLifetime = TimeSpan.FromSeconds(60);
        private readonly Dictionary<Guid, PriorityAlertRow> _seenAlerts = new();
        private readonly Dictionary<Guid, DateTime> _resolvedAt = new();

        private readonly System.Windows.Threading.DispatcherTimer _refreshTimer =
            new()
            {
                Interval = TimeSpan.FromSeconds(5)
            };

        private readonly Dictionary<string, string> _lastSnapshots = new();

        // True only when the lines differ from the last time this key was shown.
        private bool HasChanged(string key, IEnumerable<string> lines)
        {
            var snapshot = string.Join("\n", lines);

            if (_lastSnapshots.TryGetValue(key, out var previous) && previous == snapshot)
                return false;

            _lastSnapshots[key] = snapshot;
            return true;
        }

        private bool IsAnyDropDownOpen() =>
            DeviceComboBox.IsDropDownOpen ||
            CommandComboBox.IsDropDownOpen ||
            LiveDeviceFilterComboBox.IsDropDownOpen ||
            LiveCategoryFilterComboBox.IsDropDownOpen ||
            LiveAlertFilterComboBox.IsDropDownOpen ||
            StatusFilterComboBox.IsDropDownOpen;

        private bool _isUpdatingLiveFilters;
        private bool _isRefreshing;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };


        public CommandStream()
        {
            InitializeComponent();
            Loaded += CommandStreamView_Loaded;
            Unloaded += CommandStreamView_Unloaded;

            _refreshTimer.Tick += RefreshTimer_Tick;
        }

        private async void CommandStreamView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDevicesAsync();
            await LoadHistoryAsync();
            await LoadRecommendationsAsync();
            await LoadLiveTelemetryAsync();
            await LoadAlertsPanelAsync();

            if (!_refreshTimer.IsEnabled)
                _refreshTimer.Start();
        }

        private void CommandStreamView_Unloaded(object sender, RoutedEventArgs e)
        {
            _refreshTimer.Stop();
        }

        private async void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            if (_isRefreshing || AutoRefreshCheckBox.IsChecked != true || IsAnyDropDownOpen())
                return;

            _isRefreshing = true;

            try
            {
                await LoadLiveTelemetryAsync();
                await LoadAlertsPanelAsync();
                await LoadRecommendationsAsync();
            }
            finally
            {
                _isRefreshing = false;
            }
        }


        private async Task LoadDevicesAsync()
        {
            try
            {
                var devices = await Client.GetFromJsonAsync<List<SensorItem>>("api/sensors", JsonOptions);

                var currentDevice = DeviceComboBox.Text;

                DeviceComboBox.ItemsSource = devices?
                    .Select(s => s.DeviceIdentifier)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                DeviceComboBox.Text = currentDevice;
            }
            catch (Exception ex)
            {
                MessageTextBlock.Text = $"Could not load devices: {ex.Message}";
            }
        }

        private async Task LoadHistoryAsync()
        {
            await LoadCommandsAsync("api/commands/history");
        }

        private async Task LoadCommandsAsync(string endpoint)
        {
            try
            {
                var commands = await Client.GetFromJsonAsync<List<CommandItem>>(endpoint, JsonOptions);

                _allCommands.Clear();
                _allCommands.AddRange(commands ?? new List<CommandItem>());

                ApplyFilters();
                MessageTextBlock.Text = $"Loaded {_allCommands.Count} command records.";
            }
            catch (Exception ex)
            {
                MessageTextBlock.Text = $"Could not load commands: {ex.Message}";
            }
        }

        // from here

        private async Task LoadLiveTelemetryAsync()
        {
            try
            {
                var soilTask = Client.GetFromJsonAsync<List<TelemetryItem>>("api/Telemetry/soil-moisture", JsonOptions);

                var powerTask = Client.GetFromJsonAsync<List<TelemetryItem>>("api/Telemetry/power", JsonOptions);

                var valveTask = Client.GetFromJsonAsync<List<TelemetryItem>>("api/Telemetry/valve", JsonOptions);

                var alertsTask = Client.GetFromJsonAsync<List<AlertItem>>("api/alerts", JsonOptions);

                await Task.WhenAll(soilTask, powerTask, valveTask, alertsTask);

                var readings = new List<LiveTelemetryItem>();

                AddLatestReadings(readings, await soilTask, "Soil Moisture");
                AddLatestReadings(readings, await powerTask, "Power");
                AddLatestReadings(readings, await valveTask, "Valve");

                _activeAlertDevices.Clear();

                foreach (var alert in await alertsTask ?? new List<AlertItem>())
                {
                    if (!alert.IsResolved &&
                        !string.IsNullOrWhiteSpace(alert.DeviceIdentifier))
                    {
                        _activeAlertDevices.Add(alert.DeviceIdentifier);
                    }
                }

                _allLiveTelemetry.Clear();
                _allLiveTelemetry.AddRange(readings);

                UpdateLiveDeviceFilter();
                ApplyLiveTelemetryFilters();

                LiveTelemetryStatusTextBlock.Text = $"Updated {DateTime.Now:HH:mm:ss}";
                LiveTelemetryMessageTextBlock.Text = $"Loaded {_allLiveTelemetry.Count} latest device readings.";
            }
            catch (Exception ex)
            {
                LiveTelemetryStatusTextBlock.Text = "Refresh failed";
                LiveTelemetryMessageTextBlock.Text = $"Could not load live telemetry: {ex.Message}";
            }
        }

        private async Task LoadAlertsPanelAsync()
        {
            try
            {
                var priorityTask = Client.GetFromJsonAsync<List<AlertItem>>("api/alerts/priority", JsonOptions);
                var trackingTask = Client.GetFromJsonAsync<TrackingResponse>("api/alerts/tracking", JsonOptions);

                await Task.WhenAll(priorityTask, trackingTask);

                var priority = await priorityTask ?? new List<AlertItem>();
                var tracking = await trackingTask;

                var rows = BuildAlertRows(priority);

                var activeErrors = (tracking?.ActiveErrors ?? new List<string>())
                    .Select(e => e.Replace("|", "  —  "))
                    .ToList();

                var disconnected = tracking?.DisconnectedDevices ?? new List<string>();

                // Only touch a control when its content actually changed.
                if (HasChanged("priority", rows.Select(r =>
                        $"{r.Order}|{r.Severity}|{r.DeviceIdentifier}|{r.Message}|{r.Raised}")))
                    PriorityAlertsGrid.ItemsSource = rows;

                if (HasChanged("errors", activeErrors))
                    ActiveErrorsListBox.ItemsSource = activeErrors;

                if (HasChanged("disconnected", disconnected))
                    DisconnectedDevicesListBox.ItemsSource = disconnected;

                AlertsStatusTextBlock.Text =
                    $"{priority.Count} queued alert(s) · {activeErrors.Count} active error(s) · " +
                    $"{disconnected.Count} disconnected device(s) · updated {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                AlertsStatusTextBlock.Text = $"Could not load alerts: {ex.Message}";
            }
        }

        // Live alerts first (in priority order), then recently resolved ones.
        private List<PriorityAlertRow> BuildAlertRows(List<AlertItem> live)
        {
            var now = DateTime.UtcNow;
            var liveIds = live.Select(a => a.Id).ToHashSet();

            // Remember every live alert, and clear any earlier "resolved" mark.
            foreach (var alert in live)
            {
                _seenAlerts[alert.Id] = new PriorityAlertRow
                {
                    Severity = alert.Severity ?? "",
                    DeviceIdentifier = alert.DeviceIdentifier ?? "",
                    Message = alert.Message ?? "",
                    Raised = alert.Timestamp.ToLocalTime().ToString("HH:mm:ss")
                };
                _resolvedAt.Remove(alert.Id);
            }

            // Alerts that left the queue are resolved: stamp the time once, drop after 60 seconds.
            foreach (var id in _seenAlerts.Keys.Where(id => !liveIds.Contains(id)).ToList())
            {
                if (!_resolvedAt.TryAdd(id, now) && now - _resolvedAt[id] > ResolvedAlertLifetime)
                {
                    _seenAlerts.Remove(id);
                    _resolvedAt.Remove(id);
                }
            }

            var rows = new List<PriorityAlertRow>();
            var order = 1;

            foreach (var alert in live)
            {
                var row = _seenAlerts[alert.Id];
                rows.Add(new PriorityAlertRow
                {
                    Order = (order++).ToString(),
                    Severity = row.Severity,
                    DeviceIdentifier = row.DeviceIdentifier,
                    Message = row.Message,
                    Raised = row.Raised
                });
            }

            foreach (var id in _resolvedAt.OrderByDescending(r => r.Value).Select(r => r.Key))
            {
                var row = _seenAlerts[id];
                rows.Add(new PriorityAlertRow
                {
                    Order = "—",
                    Severity = row.Severity,
                    DeviceIdentifier = row.DeviceIdentifier,
                    Message = row.Message + "  (resolved)",
                    Raised = row.Raised
                });
            }

            return rows;
        }

        private void AddLatestReadings(List<LiveTelemetryItem> destination, List<TelemetryItem>? readings, string category)
        {
            if (readings == null)
                return;

            foreach (var group in readings
                .GroupBy(x => x.DeviceIdentifier ?? "",
                    StringComparer.OrdinalIgnoreCase))
            {
                var latest = group
                    .OrderByDescending(x => x.Timestamp)
                    .First();

                var deviceId = latest.DeviceIdentifier ?? "";

                var hasActiveAlert = _activeAlertDevices.Contains(deviceId);

                destination.Add(new LiveTelemetryItem
                {
                    DeviceIdentifier = deviceId,
                    SensorName = latest.SensorName ?? "",
                    Category = category,
                    DisplayValue = $"{latest.Value} {latest.Unit}".Trim(),
                    Timestamp = latest.Timestamp.ToLocalTime()
                        .ToString("yyyy-MM-dd HH:mm:ss"),
                    AlertState = hasActiveAlert ? "Active" : "None"
                });
            }
        }

        private void UpdateLiveDeviceFilter()
        {
            if (_isUpdatingLiveFilters)
                return;

            _isUpdatingLiveFilters = true;

            try
            {
                var selected = (LiveDeviceFilterComboBox.SelectedItem
                    as ComboBoxItem)?.Content?.ToString();

                var current = selected ?? "All devices";

                var devices = _allLiveTelemetry
                    .Select(x => x.DeviceIdentifier)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList();

                var existingDevices = LiveDeviceFilterComboBox.Items
                    .OfType<ComboBoxItem>()
                    .Skip(1)
                    .Select(item => item.Content?.ToString() ?? "")
                    .ToList();

                if (existingDevices.SequenceEqual(devices))
                    return;

                LiveDeviceFilterComboBox.Items.Clear();
                LiveDeviceFilterComboBox.Items.Add(new ComboBoxItem { Content = "All devices" });

                foreach (var device in devices)
                {
                    LiveDeviceFilterComboBox.Items.Add(new ComboBoxItem { Content = device });
                }

                var matchingItem = LiveDeviceFilterComboBox.Items
                    .OfType<ComboBoxItem>()
                    .FirstOrDefault(x =>
                        string.Equals(x.Content?.ToString(), current,
                            StringComparison.OrdinalIgnoreCase));

                LiveDeviceFilterComboBox.SelectedItem = matchingItem ?? LiveDeviceFilterComboBox.Items[0];
            }
            finally
            {
                _isUpdatingLiveFilters = false;
            }
        }

        private void LiveTelemetryFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isUpdatingLiveFilters && IsLoaded)
                ApplyLiveTelemetryFilters();
        }

        private void ApplyLiveTelemetryFilters()
        {
            if (LiveTelemetryGrid == null)
                return;

            var device = (LiveDeviceFilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All devices";

            var category = (LiveCategoryFilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All categories";

            var alertState = (LiveAlertFilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All readings";

            var filtered = _allLiveTelemetry.Where(item =>
            {
                var matchesDevice =
                    device == "All devices" ||
                    string.Equals(item.DeviceIdentifier, device,
                        StringComparison.OrdinalIgnoreCase);

                var matchesCategory =
                    category == "All categories" ||
                    item.Category == category;

                var hasAlert = _activeAlertDevices.Contains(item.DeviceIdentifier);

                var matchesAlert =
                    alertState == "All readings" ||
                    (alertState == "Active alert" && hasAlert) ||
                    (alertState == "No active alert" && !hasAlert);

                return matchesDevice && matchesCategory && matchesAlert;
            }).ToList();

            LiveTelemetryGrid.ItemsSource = filtered;
        }


        private async void SubmitCommand_Click(object sender, RoutedEventArgs e)
        {
            var device = DeviceComboBox.Text?.Trim();
            var selectedCommand = (CommandComboBox.SelectedItem as ComboBoxItem)
                ?.Content?.ToString();

            if (string.IsNullOrWhiteSpace(device) || string.IsNullOrWhiteSpace(selectedCommand))
            {
                MessageTextBlock.Text = "Select or enter a device and choose a command.";
                return;
            }

            var request = new
            {
                DeviceIdentifier = device,
                CommandType = selectedCommand,
                Description = DescriptionTextBox.Text.Trim()
            };

            try
            {
                using var response = await Client.PostAsJsonAsync("api/commands", request);

                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageTextBlock.Text = $"Submission failed ({(int)response.StatusCode}): {body}";
                    return;
                }

                MessageTextBlock.Text = "Command submitted successfully.";

                await LoadCommandsAsync("api/commands/pending");
            }
            catch (Exception ex)
            {
                MessageTextBlock.Text = $"Command submission failed: {ex.Message}";
            }
        }

        private async void ExecuteNext_Click(object sender, RoutedEventArgs e)
        {
            await PostCommandActionAsync("api/commands/execute-next");
        }

        private async void UndoCommand_Click(object sender, RoutedEventArgs e)
        {
            await PostCommandActionAsync("api/commands/undo");
        }

        private async Task PostCommandActionAsync(string endpoint)
        {
            try
            {
                using var response = await Client.PostAsync(endpoint, null);
                var body = await response.Content.ReadAsStringAsync();

                MessageTextBlock.Text = response.IsSuccessStatusCode
                    ? $"Action completed: {body}"
                    : $"Action failed ({(int)response.StatusCode}): {body}";

                await LoadHistoryAsync();
            }
            catch (Exception ex)
            {
                MessageTextBlock.Text = $"Request failed: {ex.Message}";
            }
        }

        private async void LoadPending_Click(object sender, RoutedEventArgs e)
        {
            await LoadCommandsAsync("api/commands/pending");
        }

        private async void LoadHistory_Click(object sender, RoutedEventArgs e)
        {
            await LoadHistoryAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadDevicesAsync();
            await LoadHistoryAsync();
            await LoadRecommendationsAsync();
            await LoadLiveTelemetryAsync();
            await LoadAlertsPanelAsync();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                ApplyFilters();
        }
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.ShowMainMenu();
            }
        }

        private void ApplyFilters()
        {
            if (CommandsGrid == null)
                return;

            var search = SearchTextBox.Text?.Trim() ?? string.Empty;
            var status = (StatusFilterComboBox.SelectedItem as ComboBoxItem)
                ?.Content?.ToString() ?? "All";

            var filtered = _allCommands.Where(c =>
                (status == "All" ||
                 string.Equals(c.Status, status,
                     StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(search) ||
                 (c.DeviceIdentifier ?? "").Contains(
                     search, StringComparison.OrdinalIgnoreCase) ||
                 (c.CommandType ?? "").Contains(
                     search, StringComparison.OrdinalIgnoreCase) ||
                 (c.Description ?? "").Contains(
                     search, StringComparison.OrdinalIgnoreCase) ||
                 (c.ResultMessage ?? "").Contains(
                     search, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            CommandsGrid.ItemsSource = filtered;
        }

        public sealed class SensorItem
        {
            public string DeviceIdentifier { get; set; } = "";
        }

        public sealed class CommandItem
        {
            public string DeviceIdentifier { get; set; } = "";
            public string CommandType { get; set; } = "";
            public string Description { get; set; } = "";
            public string Status { get; set; } = "";
            public string? CreatedAt { get; set; }
            public string? ResultMessage { get; set; }
        }

        private async Task LoadRecommendationsAsync()
        {
            try
            {
                var recommendations =
                    await Client.GetFromJsonAsync<RecommendationsResponse>(
                        "api/recommendations",
                        JsonOptions);

                var suggested = recommendations?.SuggestedActions?
                    .Select(action =>
                        $"[{action.Priority}] {action.DeviceIdentifier}: " +
                        $"{action.SuggestedAction} — {action.Reason}")
                    .ToList()
                    ?? new List<string>();

                var insights = recommendations?.AutomatedInsights ?? new List<string>();

                if (HasChanged("suggested", suggested))
                    SuggestedActionsListBox.ItemsSource = suggested;

                if (HasChanged("insights", insights))
                    AutomatedInsightsListBox.ItemsSource = insights;

                RecommendationStatusTextBlock.Text =
                    $"Recommendations updated: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                RecommendationStatusTextBlock.Text =
                    $"Could not load recommendations: {ex.Message}";
            }
        }

        private async void RefreshRecommendations_Click(object sender, RoutedEventArgs e)
        {
            await LoadRecommendationsAsync();
        }

        public sealed class RecommendationsResponse
        {
            public DateTime GeneratedAt { get; set; }

            public List<SuggestedActionItem>? SuggestedActions { get; set; }

            public List<string>? AutomatedInsights { get; set; }
        }

        public sealed class SuggestedActionItem
        {
            public string DeviceIdentifier { get; set; } = "";

            public string SuggestedAction { get; set; } = "";

            public string Reason { get; set; } = "";

            public string Priority { get; set; } = "";
        }


        public sealed class TelemetryItem
        {
            public string? DeviceIdentifier { get; set; }
            public string? SensorName { get; set; }
            public string? Unit { get; set; }
            public DateTime Timestamp { get; set; }
            public JsonElement Value { get; set; }
        }

        public sealed class AlertItem
        {
            public Guid Id { get; set; }
            public string? DeviceIdentifier { get; set; }
            public string? Severity { get; set; }
            public string? Message { get; set; }
            public DateTime Timestamp { get; set; }
            public bool IsResolved { get; set; }
        }

        public sealed class PriorityAlertRow
        {
            public string Order { get; set; } = "";
            public string Severity { get; set; } = "";
            public string DeviceIdentifier { get; set; } = "";
            public string Message { get; set; } = "";
            public string Raised { get; set; } = "";
        }

        public sealed class TrackingResponse
        {
            public List<string> ActiveErrors { get; set; } = new();
            public List<string> DisconnectedDevices { get; set; } = new();
        }

        public sealed class LiveTelemetryItem
        {
            public string DeviceIdentifier { get; set; } = "";
            public string SensorName { get; set; } = "";
            public string Category { get; set; } = "";
            public string DisplayValue { get; set; } = "";
            public string Timestamp { get; set; } = "";
            public string AlertState { get; set; } = "None";
        }

    }
}
