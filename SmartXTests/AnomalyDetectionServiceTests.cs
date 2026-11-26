using SmartX.API.Data;
using SmartX.API.Models;
using SmartX.API.Services;

namespace SmartX.Tests;

public class AnomalyDetectionServiceTests
{
    [Fact]
    public void CheckSoilMoisture_ShouldCreateCriticalAlert_WhenMoistureIsTooLow()
    {
        // Arrange
        var dataStore = new SmartXDataStore();
        var service = new AnomalyDetectionService(dataStore);

        var packet = new TelemetryPacket<float>
        {
            DeviceIdentifier = "SENSOR-001",
            SensorName = "Soil Moisture Sensor",
            Unit = "%",
            Value = 15.0f,
            Timestamp = DateTime.UtcNow
        };

        // Act
        service.CheckSoilMoisture(packet);

        // Assert
        var alert = Assert.Single(dataStore.Alerts);

        Assert.Equal("SENSOR-001", alert.DeviceIdentifier);
        Assert.Equal("Soil Moisture Sensor", alert.SensorName);
        Assert.Equal("Critical", alert.Severity);
        Assert.Contains("critically low", alert.Message);
        Assert.False(alert.IsResolved);
    }

    [Fact]
    public void CheckSoilMoisture_ShouldNotCreateAlert_WhenMoistureIsNormal()
    {
        // Arrange
        var dataStore = new SmartXDataStore();
        var service = new AnomalyDetectionService(dataStore);

        var packet = new TelemetryPacket<float>
        {
            DeviceIdentifier = "SENSOR-002",
            SensorName = "Soil Moisture Sensor",
            Unit = "%",
            Value = 50.0f,
            Timestamp = DateTime.UtcNow
        };

        // Act
        service.CheckSoilMoisture(packet);

        // Assert
        Assert.Empty(dataStore.Alerts);
    }

    [Fact]
    public void CheckPowerConsumption_ShouldCreateCriticalAlert_WhenPowerIsTooHigh()
    {
        // Arrange
        var dataStore = new SmartXDataStore();
        var service = new AnomalyDetectionService(dataStore);

        var packet = new TelemetryPacket<int>
        {
            DeviceIdentifier = "SENSOR-003",
            SensorName = "Power Sensor",
            Unit = "W",
            Value = 6000,
            Timestamp = DateTime.UtcNow
        };

        // Act
        service.CheckPowerConsumption(packet);

        // Assert
        var alert = Assert.Single(dataStore.Alerts);

        Assert.Equal("SENSOR-003", alert.DeviceIdentifier);
        Assert.Equal("Power Sensor", alert.SensorName);
        Assert.Equal("Critical", alert.Severity);
        Assert.Contains("6000 W", alert.Message);
        Assert.False(alert.IsResolved);
    }

    [Fact]
    public void CheckPowerConsumption_ShouldNotCreateAlert_WhenPowerIsNormal()
    {
        // Arrange
        var dataStore = new SmartXDataStore();
        var service = new AnomalyDetectionService(dataStore);

        var packet = new TelemetryPacket<int>
        {
            DeviceIdentifier = "SENSOR-004",
            SensorName = "Power Sensor",
            Unit = "W",
            Value = 3000,
            Timestamp = DateTime.UtcNow
        };

        // Act
        service.CheckPowerConsumption(packet);

        // Assert
        Assert.Empty(dataStore.Alerts);
    }

    [Fact]
    public void CheckSoilMoisture_ShouldPreventDuplicateUnresolvedAlerts()
    {
        // Arrange
        var dataStore = new SmartXDataStore();
        var service = new AnomalyDetectionService(dataStore);

        var packet = new TelemetryPacket<float>
        {
            DeviceIdentifier = "SENSOR-005",
            SensorName = "Soil Moisture Sensor",
            Unit = "%",
            Value = 10.0f,
            Timestamp = DateTime.UtcNow
        };

        // Act
        service.CheckSoilMoisture(packet);
        service.CheckSoilMoisture(packet);

        // Assert
        Assert.Single(dataStore.Alerts);
    }

    [Fact]
    public void GetRecentAlerts_ShouldReturnAlertsUsingStackOrder()
    {
        // Arrange
        var dataStore = new SmartXDataStore();
        var service = new AnomalyDetectionService(dataStore);

        var firstPacket = new TelemetryPacket<float>
        {
            DeviceIdentifier = "SENSOR-006",
            SensorName = "Soil Moisture Sensor",
            Unit = "%",
            Value = 10.0f,
            Timestamp = DateTime.UtcNow
        };

        var secondPacket = new TelemetryPacket<int>
        {
            DeviceIdentifier = "SENSOR-007",
            SensorName = "Power Sensor",
            Unit = "W",
            Value = 7000,
            Timestamp = DateTime.UtcNow
        };

        // Act
        service.CheckSoilMoisture(firstPacket);
        service.CheckPowerConsumption(secondPacket);

        var recentAlerts = service.GetRecentAlerts().ToList();

        // Assert
        Assert.Equal(2, recentAlerts.Count);
        Assert.Equal("SENSOR-007", recentAlerts[0].DeviceIdentifier);
        Assert.Equal("SENSOR-006", recentAlerts[1].DeviceIdentifier);
    }
}