using SmartX.API.Data;
using SmartX.API.Models;

namespace SmartX.API.Services
{
    public class SensorMonitoringService
    {
        private readonly SmartXDataStore _dataStore;

        private readonly TimeSpan _disconnectTimeout = TimeSpan.FromSeconds(15);

        public SensorMonitoringService(SmartXDataStore dataStore)
        {
            _dataStore = dataStore;
        }

        public void CheckSensorConnections()
        {
            var now = DateTime.UtcNow;

            foreach (var sensor in _dataStore.Sensors.ToList())
            {
                if (now - sensor.LastSeen > _disconnectTimeout)
                    MarkSensorOffline(sensor);
                else
                    MarkSensorOnline(sensor);
            }
        }

        private void MarkSensorOffline(Sensor sensor)
        {
            sensor.Status = SensorStatus.Offline;

            lock (_dataStore.AlertLock)
            {
                // Add returns false when the device is already known to be disconnected,
                // so duplicate disconnect alerts are never created.
                if (!_dataStore.DisconnectedDevices.Add(sensor.DeviceIdentifier))
                    return;

                _dataStore.ActiveErrors.Add($"{sensor.DeviceIdentifier}|Disconnected");

                _dataStore.Alerts.Add(new Alert
                {
                    DeviceIdentifier = sensor.DeviceIdentifier,
                    SensorName = sensor.DeviceIdentifier,
                    Condition = "Disconnected",
                    Message = $"Sensor {sensor.DeviceIdentifier} has disconnected. " +
                              $"No telemetry received for more than {_disconnectTimeout.TotalSeconds} seconds.",
                    Severity = "Critical",
                    Timestamp = DateTime.UtcNow,
                    IsResolved = false
                });
            }
        }

        private void MarkSensorOnline(Sensor sensor)
        {
            sensor.Status = SensorStatus.Online;

            lock (_dataStore.AlertLock)
            {
                if (!_dataStore.DisconnectedDevices.Remove(sensor.DeviceIdentifier))
                    return;

                foreach (var alert in _dataStore.Alerts)
                {
                    if (!alert.IsResolved &&
                        alert.Condition == "Disconnected" &&
                        string.Equals(alert.DeviceIdentifier, sensor.DeviceIdentifier,
                                      StringComparison.OrdinalIgnoreCase))
                    {
                        alert.IsResolved = true;
                    }
                }

                _dataStore.ActiveErrors.Remove($"{sensor.DeviceIdentifier}|Disconnected");
            }
        }
    }
}