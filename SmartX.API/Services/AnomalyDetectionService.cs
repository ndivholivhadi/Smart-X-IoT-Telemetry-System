using SmartX.API.Data;
using SmartX.API.Models;

namespace SmartX.API.Services
{
    public class AnomalyDetectionService
    {
        private readonly SmartXDataStore _dataStore;
        private readonly Stack<Alert> _recentAlerts = new();

        // Priority = (severity rank, arrival ticks): Critical always dequeues first,
        // and alerts of the same severity stay first-in-first-out.
        private readonly PriorityQueue<Alert, (int Rank, long Ticks)> _priorityAlerts = new();
        private const float CriticalSoilMoisture = 20.0f;
        private const float WarningSoilMoisture = 30.0f;

        private const int WarningPowerConsumption = 4000;
        private const int CriticalPowerConsumption = 5000;

        public AnomalyDetectionService(SmartXDataStore dataStore)
        {
            _dataStore = dataStore;
        }


        public void CheckSoilMoisture(TelemetryPacket<float> packet)
        {
            string? condition = null, message = null, severity = null;

            if (packet.Value < CriticalSoilMoisture)
            {
                condition = "SoilMoisture.Critical";
                message = $"Soil moisture is critically low: {packet.Value:F1}%";
                severity = "Critical";
            }
            else if (packet.Value < WarningSoilMoisture)
            {
                condition = "SoilMoisture.Warning";
                message = $"Soil moisture is low: {packet.Value:F1}%";
                severity = "Warning";
            }
            ResolveOutdatedAlerts(packet.DeviceIdentifier, "SoilMoisture.", condition);

            if (condition != null)
                CreateAlert(packet.DeviceIdentifier, packet.SensorName, condition, message!, severity!);
        }
        public void CheckPowerConsumption(TelemetryPacket<int> packet)
        {
            string? condition = null, message = null, severity = null;

            if (packet.Value > CriticalPowerConsumption)
            {
                condition = "Power.Critical";
                message = $"Critically high power consumption: {packet.Value} W";
                severity = "Critical";
            }
            else if (packet.Value > WarningPowerConsumption)
            {
                condition = "Power.Warning";
                message = $"High power consumption detected: {packet.Value} W";
                severity = "Warning";
            }

            ResolveOutdatedAlerts(packet.DeviceIdentifier, "Power.", condition);

            if (condition != null)
                CreateAlert(packet.DeviceIdentifier, packet.SensorName, condition, message!, severity!);
        }

        private void CreateAlert(string deviceIdentifier, string sensorName,
                                 string condition, string message, string severity)
        {
            lock (_dataStore.AlertLock)
            {
                // HashSet.Add returns false if this device/condition is already active,
                // which gives O(1) duplicate prevention.
                if (!_dataStore.ActiveErrors.Add($"{deviceIdentifier}|{condition}"))
                    return;


                var alert = new Alert
                {
                    DeviceIdentifier = deviceIdentifier,
                    SensorName = sensorName,
                    Condition = condition,
                    Message = message,
                    Severity = severity,
                    Timestamp = DateTime.UtcNow,
                    IsResolved = false
                };


                _dataStore.Alerts.Add(alert);
                _recentAlerts.Push(alert);
                _priorityAlerts.Enqueue(alert, (GetAlertRank(severity), alert.Timestamp.Ticks));
            }
        }


        private void ResolveOutdatedAlerts(string deviceIdentifier, string family, string? currentCondition)
        {
            lock (_dataStore.AlertLock)
            {
                foreach (var alert in _dataStore.Alerts)
                {
                    if (alert.IsResolved ||
                        !string.Equals(alert.DeviceIdentifier, deviceIdentifier, StringComparison.OrdinalIgnoreCase) ||
                        !alert.Condition.StartsWith(family, StringComparison.Ordinal) ||
                        alert.Condition == currentCondition)
                    {
                        continue;
                    }

                    alert.IsResolved = true;
                    _dataStore.ActiveErrors.Remove($"{alert.DeviceIdentifier}|{alert.Condition}");
                }
            }
        }
        public IEnumerable<Alert> GetAlerts()
        {
            lock (_dataStore.AlertLock)
            {
                return _dataStore.Alerts.OrderByDescending(a => a.Timestamp).ToList();
            }
        }

        public IEnumerable<Alert> GetRecentAlerts()
        {
            lock (_dataStore.AlertLock)
            {
                return _recentAlerts.ToList();   // stack order: newest first
            }
        }

        // Non-destructive view of the priority queue, most urgent first.
        public IEnumerable<Alert> GetPriorityAlerts()
        {
            lock (_dataStore.AlertLock)
            {
                return _priorityAlerts.UnorderedItems
                    .Where(i => !i.Element.IsResolved)
                    .OrderBy(i => i.Priority)
                    .Select(i => i.Element)
                    .ToList();
            }
        }

        // Dequeues the most urgent unresolved alert.
        public Alert? GetNextPriorityAlert()
        {
            lock (_dataStore.AlertLock)
            {
                while (_priorityAlerts.TryDequeue(out var alert, out _))
                {
                    if (!alert.IsResolved)
                        return alert;
                }

                return null;
            }
        }

        public IEnumerable<string> GetActiveErrors()
        {
            lock (_dataStore.AlertLock)
            {
                return _dataStore.ActiveErrors.OrderBy(e => e).ToList();
            }
        }

        public IEnumerable<string> GetDisconnectedDevices()
        {
            lock (_dataStore.AlertLock)
            {
                return _dataStore.DisconnectedDevices.OrderBy(d => d).ToList();
            }
        }
        private static int GetAlertRank(string severity) => severity.ToLowerInvariant() switch
        {
            "critical" => 1,
            "warning" => 2,
            "information" => 3,
            _ => 4
        };
    }
}