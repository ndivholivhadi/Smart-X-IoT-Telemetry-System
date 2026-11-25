using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Win32;
using SmartX.Client.Models;
using SmartX.Client.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SmartX.Client.Views
{
    public partial class TelemetryDashboard : UserControl
    {
        private readonly ApiService _apiService;
        private readonly DispatcherTimer _refreshTimer;
        private int _currentOnlineSensors;
        private int _currentOfflineSensors;
        private int _currentTotalSensors;
        private int _currentActiveAlerts;
        private List<Alerts> _allAlerts = new();

        private bool _isUpdatingLiveDeviceFilter = false;

        public TelemetryDashboard()
        {
            InitializeComponent();

            _apiService = new ApiService();

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };

            _refreshTimer.Tick += RefreshTimer_Tick;

            Loaded += TelemetryDashboard_Loaded;
            Unloaded += TelemetryDashboard_Unloaded;
        }

        private async void TelemetryDashboard_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadSensorsAsync();
            await LoadTelemetryAsync();
            await LoadAlertsAsync();
            await LoadTelemetryStatisticsAsync();
            await LoadSensorSelectionAsync();

            if (SensorSelectionComboBox.SelectedItem is string deviceIdentifier)
            {
                await LoadSoilMoistureHistoryAsync(deviceIdentifier);
            }

            _refreshTimer.Start();
        }

        private async void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            await LoadSensorsAsync();
            await LoadTelemetryAsync();
            await LoadAlertsAsync();
            await LoadTelemetryStatisticsAsync();

            if (SensorSelectionComboBox.SelectedItem is string deviceIdentifier)
            {
                await LoadSoilMoistureHistoryAsync(deviceIdentifier);
            }
        }

        private void TelemetryDashboard_Unloaded(object sender, RoutedEventArgs e)
        {
            _refreshTimer.Stop();
        }

        private async Task LoadSensorsAsync()
        {
            try
            {
                var sensors = await _apiService.GetSensorsAsync();

                SensorsDataGrid.ItemsSource = sensors;

                var selectedDevice = LiveTelemetryDeviceComboBox.SelectedItem as string;

                var deviceOptions = new List<string> { "All Devices" };

                deviceOptions.AddRange(
                    sensors
                        .Select(s => s.DeviceIdentifier)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct(StringComparer.OrdinalIgnoreCase));

                // Only rebuild the ComboBox when its available devices change.
                var currentOptions = LiveTelemetryDeviceComboBox.Items
                    .Cast<string>()
                    .ToList();

                bool optionsChanged =
                    !currentOptions.SequenceEqual(
                        deviceOptions,
                        StringComparer.OrdinalIgnoreCase);

                if (optionsChanged)
                {
                    _isUpdatingLiveDeviceFilter = true;

                    try
                    {
                        LiveTelemetryDeviceComboBox.ItemsSource = deviceOptions;

                        LiveTelemetryDeviceComboBox.SelectedItem =
                            deviceOptions.FirstOrDefault(d =>
                                string.Equals(
                                    d,
                                    selectedDevice,
                                    StringComparison.OrdinalIgnoreCase))
                            ?? "All Devices";
                    }
                    finally
                    {
                        _isUpdatingLiveDeviceFilter = false;
                    }
                }


                AttachmentSensorComboBox.ItemsSource = sensors;
                SearchSensorComboBox.ItemsSource = sensors;

                AttachmentSensorComboBox.DisplayMemberPath = "DeviceIdentifier";
                SearchSensorComboBox.DisplayMemberPath = "DeviceIdentifier";
                TotalSensorsText.Text = sensors.Count.ToString();

                OnlineSensorsText.Text =
                    sensors.Count(s =>
                        s.Status.Equals(
                            "Online",
                            StringComparison.OrdinalIgnoreCase))
                    .ToString();

                OfflineSensorsText.Text =
                    sensors.Count(s =>
                        s.Status.Equals(
                            "Offline",
                            StringComparison.OrdinalIgnoreCase))
                    .ToString();

                _currentTotalSensors = sensors.Count;

                _currentOnlineSensors = sensors.Count(s =>
                    s.Status.Equals(
                        "Online",
                        StringComparison.OrdinalIgnoreCase));

                _currentOfflineSensors = sensors.Count(s =>
                    s.Status.Equals(
                        "Offline",
                        StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not connect to SmartX.API.\n\n{ex.Message}",
                    "API Connection Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task LoadTelemetryAsync()
        {
            try
            {
                var soilMoisture = await _apiService.GetSoilMoistureAsync();

                var power = await _apiService.GetPowerAsync();

                var valve = await _apiService.GetValveAsync();


                var selectedDevice = LiveTelemetryDeviceComboBox.SelectedItem as string;

                bool showAllDevices = string.IsNullOrWhiteSpace(selectedDevice) || selectedDevice == "All Devices";

                var filteredSoilMoisture = showAllDevices
                    ? soilMoisture
                    : soilMoisture
                        .Where(t => string.Equals(
                            t.DeviceIdentifier,
                            selectedDevice,
                            StringComparison.OrdinalIgnoreCase))
                        .ToList();

                var filteredPower = showAllDevices
                    ? power
                    : power
                        .Where(t => string.Equals(
                            t.DeviceIdentifier,
                            selectedDevice,
                            StringComparison.OrdinalIgnoreCase))
                        .ToList();

                var filteredValve = showAllDevices
                    ? valve
                    : valve
                        .Where(t => string.Equals(
                            t.DeviceIdentifier,
                            selectedDevice,
                            StringComparison.OrdinalIgnoreCase))
                        .ToList();



                if (filteredSoilMoisture.Count > 0)
                {
                    var latestSoilMoisture = filteredSoilMoisture
                        .OrderByDescending(t => t.Timestamp)
                        .First();

                    SoilMoistureText.Text = $"{latestSoilMoisture.Value:F1} %";
                    SoilMoistureDeviceText.Text = latestSoilMoisture.DeviceIdentifier;

                }
                else
                {
                    SoilMoistureText.Text = "_%";
                    SoilMoistureDeviceText.Text = "No data for selected device.";
                }

                if (filteredPower.Count > 0)
                {
                    var latestPower = filteredPower
                        .OrderByDescending(t => t.Timestamp)
                        .First();

                    PowerText.Text = $"{latestPower.Value} W";

                    PowerDeviceText.Text = latestPower.DeviceIdentifier;

                }
                else
                {
                    PowerText.Text = "_W";
                    PowerDeviceText.Text = "No data for selected device.";
                }

                if (filteredValve.Count > 0)
                {
                    var latestValve = filteredValve
                        .OrderByDescending(t => t.Timestamp)
                        .First();

                    ValveText.Text = latestValve.Value ? "OPEN" : "CLOSED";

                    ValveDeviceText.Text = latestValve.DeviceIdentifier;
                }
                else
                {
                    ValveText.Text = "__";
                    ValveDeviceText.Text = "No data for selected device.";
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not load telemetry.\n\n{ex.Message}",
                   "Telemetry Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private async Task LoadAlertsAsync()
        {
            try
            {
                var alerts = await _apiService.GetAlertsAsync();

                _allAlerts = alerts.ToList();

                _currentActiveAlerts = _allAlerts.Count(a => !a.IsResolved);

                ActiveAlertsText.Text = _currentActiveAlerts.ToString();

                ApplyAlertFilters();

                UpdateEnvironmentHealth(
                    _currentTotalSensors,
                    _currentOnlineSensors,
                    _currentOfflineSensors,
                    _currentActiveAlerts);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not load alerts.\n\n{ex.Message}",
                    "Alert Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }



        private void ApplyAlertFilters()
        {
            string deviceSearch = AlertDeviceFilterTextBox.Text?.Trim() ?? "";

            string severity = (AlertSeverityFilterComboBox.SelectedItem as ComboBoxItem)
                ?.Content?.ToString() ?? "All";

            string status = (AlertStatusFilterComboBox.SelectedItem as ComboBoxItem)
                ?.Content?.ToString() ?? "Active";

            var filteredAlerts = _allAlerts.Where(alert =>
                (string.IsNullOrWhiteSpace(deviceSearch) ||
                 alert.DeviceIdentifier.Contains(
                     deviceSearch,
                     StringComparison.OrdinalIgnoreCase)) &&

                (severity == "All" ||
                 string.Equals(
                     alert.Severity,
                     severity,
                     StringComparison.OrdinalIgnoreCase)) &&

                (status == "All" ||
                 (status == "Active" && !alert.IsResolved) ||
                 (status == "Resolved" && alert.IsResolved))
            )
            .OrderByDescending(alert => alert.Timestamp)
            .ToList();

            AlertsListBox.ItemsSource = filteredAlerts;
        }


        private async Task LoadSensorSelectionAsync()
        {
            var sensors = await _apiService.GetSensorsAsync();

            var soilMoistureSensors = sensors
                .Where(s => s.Category == "Environmental")
                .Select(s => s.DeviceIdentifier)
                .ToList();

            SensorSelectionComboBox.ItemsSource = soilMoistureSensors;

            if (soilMoistureSensors.Count > 0)
            {
                SensorSelectionComboBox.SelectedIndex = 0;
            }
        }

        private async Task LoadSoilMoistureHistoryAsync(string deviceIdentifier)
        {
            try
            {
                var history = await _apiService
                    .GetSoilMoistureHistoryAsync(deviceIdentifier);

                if (history == null || history.Count == 0)
                {
                    TelemetryChart.Series = Array.Empty<ISeries>();
                    return;
                }

                var orderedHistory = history
                    .OrderBy(x => x.Timestamp)
                    .ToList();

                var values = orderedHistory
                    .Select(x => (double)x.Value)
                    .ToArray();

                TelemetryChart.Series = new ISeries[]
                {
                    new LineSeries<double>
                   {
                      Name = "Soil Moisture",
                      Values = values
                   }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load soil moisture history:\n\n{ex.Message}",
                    "Graph Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void SensorSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SensorSelectionComboBox.SelectedItem is string deviceIdentifier)
            {
                await LoadSoilMoistureHistoryAsync(deviceIdentifier);
            }
        }

        private async Task LoadTelemetryStatisticsAsync()
        {
            try
            {
                var statistics =
                    await _apiService.GetTelemetryStatisticsAsync();

                if (statistics == null)
                {
                    ApiStatusText.Text = "Offline";
                    return;
                }

                ApiStatusText.Text = "Online";

                TelemetryGeneratedText.Text = statistics.TelemetryGenerated.ToString("N0");

                TelemetryTransmittedText.Text = statistics.TelemetryTransmitted.ToString("N0");

                TelemetryReceivedText.Text = statistics.TelemetryReceived.ToString("N0");

                RecordsStoredText.Text = statistics.TotalRecordsStored.ToString("N0");
            }
            catch
            {
                ApiStatusText.Text = "Offline";

                TelemetryGeneratedText.Text = "0";
                TelemetryTransmittedText.Text = "0";
                TelemetryReceivedText.Text = "0";
                RecordsStoredText.Text = "0";
            }
        }

        private async void RegisterSensorButton_Click(object sender, RoutedEventArgs e)
        {
            string deviceIdentifier = DeviceIdentifierTextBox.Text.Trim();

            string deploymentLocation = DeploymentLocationTextBox.Text.Trim();

            string zone = ZoneTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(deviceIdentifier) ||
                string.IsNullOrWhiteSpace(deploymentLocation) ||
                string.IsNullOrWhiteSpace(zone) ||
                SensorCategoryComboBox.SelectedItem == null)
            {
                RegistrationStatusTextBlock.Text = "Please complete all sensor details.";

                return;
            }

            string category = ((ComboBoxItem)SensorCategoryComboBox.SelectedItem)
                .Content
                .ToString()!;

            try
            {
                var sensor = new Sensor
                {
                    DeviceIdentifier = deviceIdentifier,
                    DeploymentLocation = deploymentLocation,
                    Zone = zone,
                    Category = category,
                    Status = "Online",
                    LastSeen = DateTime.UtcNow
                };

                var result = await _apiService.RegisterSensorAsync(sensor);

                if (result.Success)
                {
                    RegistrationStatusTextBlock.Text = result.Message;

                    DeviceIdentifierTextBox.Clear();
                    DeploymentLocationTextBox.Clear();
                    ZoneTextBox.Clear();
                    SensorCategoryComboBox.SelectedIndex = -1;

                    await LoadSensorsAsync();
                    await LoadTelemetryStatisticsAsync();
                    await LoadSensorSelectionAsync();
                }
                else
                {
                    RegistrationStatusTextBlock.Text = result.Message;
                }
            }
            catch (Exception ex)
            {
                RegistrationStatusTextBlock.Text = $"Error: {ex.Message}";
            }
        }

        private async void ChooseAttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            if (AttachmentSensorComboBox.SelectedItem is not Sensor sensor)
            {
                AttachmentStatusTextBlock.Text = "Select a sensor first.";

                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Select Sensor File",
                Filter = "Supported files|*.jpg;*.jpeg;*.png;*.gif;*.txt;*.log;*.json;*.xml;*.csv|" + "All files|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                AttachmentStatusTextBlock.Text = "Uploading...";

                bool success =
                    await _apiService.UploadAttachmentAsync(
                        sensor.Id,
                        dialog.FileName);

                if (success)
                {
                    AttachmentStatusTextBlock.Text = "File uploaded successfully.";
                    await LoadAttachmentsAsync(sensor.Id);
                }
                else
                {
                    AttachmentStatusTextBlock.Text = "File upload failed.";
                }
            }
            catch (Exception ex)
            {
                AttachmentStatusTextBlock.Text = $"Upload error: {ex.Message}";
            }
        }

        private async void AttachmentSensorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AttachmentSensorComboBox.SelectedItem is not Sensor sensor)
            {
                AttachmentsListBox.ItemsSource = null;
                return;
            }

            await LoadAttachmentsAsync(sensor.Id);
        }

        private async void DownloadAttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            if (AttachmentsListBox.SelectedItem is not Attachment attachment)
            {
                AttachmentStatusTextBlock.Text = "Select an uploaded file first.";

                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Save Sensor File",
                FileName = attachment.FileName
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                AttachmentStatusTextBlock.Text = "Downloading...";

                byte[] content = await _apiService.DownloadAttachmentAsync(attachment.SensorId, attachment.Id);

                await File.WriteAllBytesAsync(dialog.FileName, content);

                AttachmentStatusTextBlock.Text = "File downloaded.";
            }
            catch (Exception ex)
            {
                AttachmentStatusTextBlock.Text = $"Download error: {ex.Message}";
            }
        }

        private async Task LoadAttachmentsAsync(Guid sensorId)
        {
            try
            {
                var attachments = await _apiService
                    .GetAttachmentsAsync(sensorId);

                AttachmentsListBox.ItemsSource = attachments;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load attachments:\n\n{ex.Message}",
                    "Attachment Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void SearchTelemetryButton_Click(object sender, RoutedEventArgs e)
        {
            if (SearchSensorComboBox.SelectedItem is not Sensor sensor)
            {
                MessageBox.Show(
                    "Please select a sensor first.",
                    "Search",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (SearchTypeComboBox.SelectedItem is not ComboBoxItem typeItem) return;

            string searchType = typeItem.Content?.ToString() ?? "Soil Moisture";

            try
            {
                if (searchType == "Soil Moisture")
                {
                    var result = await _apiService.SearchSoilMoistureAsync(sensor.DeviceIdentifier);

                    if (result == null) return;

                    SearchAlgorithmText.Text = result.SearchAlgorithm;

                    SearchResultsText.Text = result.ResultsFound.ToString();

                    SearchTimeText.Text = $"{result.SearchTimeMicroseconds} μs";

                    SearchResultsListBox.ItemsSource = result.Results;
                }
                else if (searchType == "Power")
                {
                    var result = await _apiService.SearchPowerAsync(sensor.DeviceIdentifier);

                    if (result == null) return;

                    SearchAlgorithmText.Text = result.SearchAlgorithm;

                    SearchResultsText.Text = result.ResultsFound.ToString();

                    SearchTimeText.Text = $"{result.SearchTimeMicroseconds} μs";

                    SearchResultsListBox.ItemsSource = result.Results;
                }
                else if (searchType == "Valve")
                {
                    var result = await _apiService.SearchValveAsync(sensor.DeviceIdentifier);

                    if (result == null) return;

                    SearchAlgorithmText.Text = result.SearchAlgorithm;

                    SearchResultsText.Text = result.ResultsFound.ToString();

                    SearchTimeText.Text = $"{result.SearchTimeMicroseconds} μs";

                    SearchResultsListBox.ItemsSource = result.Results;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Search failed:\n\n{ex.Message}",
                    "Search Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateEnvironmentHealth(int totalSensors, int onlineSensors, int offlineSensors, int activeAlerts)
        {
            if (totalSensors == 0)
            {
                HealthScoreText.Text = "--";
                HealthStatusText.Text = "No sensors registered";
                HealthRecommendationText.Text =
                    "Register sensors to begin monitoring the Smart-X environment.";

                HealthOnlineText.Text = "0";
                HealthOfflineText.Text = "0";
                HealthAlertsText.Text = "0";

                return;
            }

            // 70% of the score comes from sensor availability.
            double sensorHealth = (double)onlineSensors / totalSensors * 70;

            // 30% of the score comes from active alerts.
            double alertHealth = Math.Max(0, 30 - (activeAlerts * 3));

            // Combine the two components.
            double score = sensorHealth + alertHealth;

            // Ensure score stays between 0 and 100.
            score = Math.Max(0, Math.Min(100, score));

            int healthScore = (int)Math.Round(score);

            HealthScoreText.Text = healthScore.ToString();
            HealthOnlineText.Text = onlineSensors.ToString();
            HealthOfflineText.Text = offlineSensors.ToString();
            HealthAlertsText.Text = activeAlerts.ToString();

            if (healthScore >= 90)
            {
                HealthStatusText.Text = "Environment operating normally";

                HealthRecommendationText.Text = "All monitored conditions are currently stable. " + "Continue normal monitoring.";
            }
            else if (healthScore >= 70)
            {
                HealthStatusText.Text = "Minor attention required";

                HealthRecommendationText.Text = "Some sensors or telemetry conditions require attention. " + "Review the active alerts and sensor statuses.";
            }
            else
            {
                HealthStatusText.Text = "Immediate attention required";

                HealthRecommendationText.Text = "The Smart-X environment has significant issues. " + "Check offline sensors and investigate active alerts.";
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.ShowMainMenu();
            }
        }

        private void AlertFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded ||
                AlertDeviceFilterTextBox == null ||
                AlertSeverityFilterComboBox == null ||
                AlertStatusFilterComboBox == null)
            {
                return;
            }

            ApplyAlertFilters();
        }

        private async void LiveTelemetryDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _isUpdatingLiveDeviceFilter)
                return;

            await LoadTelemetryAsync();
        }
    }

}