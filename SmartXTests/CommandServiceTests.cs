using SmartX.API.Data;
using SmartX.API.Models;
using SmartX.API.Services;

namespace SmartX.Tests;

public class CommandServiceTests
{
    private static (CommandService Service, SmartXDataStore Store, Sensor Sensor) Create()
    {
        var store = new SmartXDataStore();
        store.Sensors.Clear();

        var sensor = new Sensor { DeviceIdentifier = "PUMP-001", TelemetryEnabled = true };
        store.Sensors.Add(sensor);
        store.RebuildSensorLookup();

        return (new CommandService(store), store, sensor);
    }

    [Fact]
    public void Submit_ShouldRejectUnknownDevice()
    {
        var (service, _, _) = Create();
        Assert.Throws<ArgumentException>(() => service.SubmitCommand("NOPE", "TogglePump"));
    }

    [Fact]
    public void Submit_ShouldRejectUnsupportedCommand()
    {
        var (service, _, _) = Create();
        Assert.Throws<ArgumentException>(() => service.SubmitCommand("PUMP-001", "SelfDestruct"));
    }

    [Fact]
    public void Execute_ShouldProcessCommandsInFifoOrder()
    {
        var (service, _, _) = Create();
        service.SubmitCommand("PUMP-001", "DisableTelemetry");
        service.SubmitCommand("PUMP-001", "EnableTelemetry");

        Assert.Equal("DisableTelemetry", service.ExecuteNextCommand()!.CommandType);
        Assert.Equal("EnableTelemetry", service.ExecuteNextCommand()!.CommandType);
        Assert.Null(service.ExecuteNextCommand());
    }

    [Fact]
    public void Execute_DisableTelemetry_ShouldChangeSensor()
    {
        var (service, _, sensor) = Create();
        service.SubmitCommand("PUMP-001", "DisableTelemetry");
        service.ExecuteNextCommand();

        Assert.False(sensor.TelemetryEnabled);
    }

    [Fact]
    public void Undo_ShouldRestorePreviousStateAndMarkUndone()
    {
        var (service, _, sensor) = Create();
        service.SubmitCommand("PUMP-001", "DisableTelemetry");
        service.ExecuteNextCommand();

        var undone = service.UndoLastCommand();

        Assert.NotNull(undone);
        Assert.True(undone!.IsUndone);
        Assert.Equal("Undone", undone.Status);
        Assert.True(sensor.TelemetryEnabled);
    }

    [Fact]
    public void Undo_ShouldReturnNull_WhenNothingToUndo()
    {
        var (service, _, _) = Create();
        Assert.Null(service.UndoLastCommand());
    }

    [Fact]
    public void Undo_ShouldUndoMostRecentCommandFirst()
    {
        var (service, _, _) = Create();
        service.SubmitCommand("PUMP-001", "DisableTelemetry");
        service.SubmitCommand("PUMP-001", "TogglePump");
        service.ExecuteNextCommand();
        service.ExecuteNextCommand();

        Assert.Equal("TogglePump", service.UndoLastCommand()!.CommandType);
        Assert.Equal("DisableTelemetry", service.UndoLastCommand()!.CommandType);
    }
}