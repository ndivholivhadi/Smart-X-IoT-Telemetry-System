using SmartX.API.Data;
using SmartX.API.Models;

namespace SmartX.API.Services
{
    public class CommandService
    {
        public const string EnableTelemetry = "EnableTelemetry";
        public const string DisableTelemetry = "DisableTelemetry";
        public const string TogglePump = "TogglePump";
        public const string ToggleRelay = "ToggleRelay";

        private static readonly Dictionary<string, string> Descriptions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                [EnableTelemetry] = "Enable telemetry for the sensor.",
                [DisableTelemetry] = "Disable telemetry for the sensor.",
                [TogglePump] = "Toggle the water pump.",
                [ToggleRelay] = "Toggle the smart relay."
            };

        private readonly SmartXDataStore _dataStore;

        private readonly Queue<CommandRecord> _commandQueue = new();      // FIFO processing
        private readonly List<CommandRecord> _commandHistory = new();
        private readonly Stack<CommandRecord> _undoStack = new();         // LIFO undo

        private readonly Dictionary<string, bool> _pumpOn = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _relayOn = new(StringComparer.OrdinalIgnoreCase);

        private readonly object _lock = new();

        // When true, CommandProcessingWorker executes queued commands automatically.
        private volatile bool _autoProcess = true;

        public bool AutoProcess
        {
            get => _autoProcess;
            set => _autoProcess = value;
        }

        public CommandService(SmartXDataStore dataStore)
        {
            _dataStore = dataStore;
        }

        public IEnumerable<CommandRecord> GetHistory()
        {
            lock (_lock)
            {
                return _commandHistory.OrderByDescending(c => c.CreatedAt).ToList();
            }
        }

        public IEnumerable<CommandRecord> GetPendingCommands()
        {
            lock (_lock)
            {
                return _commandQueue.ToList();
            }
        }

        public CommandRecord SubmitCommand(string deviceIdentifier, string commandType,
                                           string? description = null)
        {
            if (string.IsNullOrWhiteSpace(deviceIdentifier))
                throw new ArgumentException("Device identifier is required.");

            if (string.IsNullOrWhiteSpace(commandType))
                throw new ArgumentException("Command type is required.");

            var canonical = Descriptions.Keys.FirstOrDefault(k =>
                k.Equals(commandType.Trim(), StringComparison.OrdinalIgnoreCase));

            if (canonical == null)
                throw new ArgumentException("Unsupported command type.");

            // O(1) dictionary lookup instead of scanning the sensor list.
            if (!_dataStore.SensorLookup.TryGetValue(deviceIdentifier.Trim(), out var sensor))
                throw new ArgumentException("Sensor not found.");

            var command = new CommandRecord
            {
                DeviceIdentifier = sensor.DeviceIdentifier,
                CommandType = canonical,
                Description = string.IsNullOrWhiteSpace(description)
                    ? Descriptions[canonical]
                    : description.Trim(),
                Status = "Pending",
                CanUndo = false
            };

            lock (_lock)
            {
                _commandQueue.Enqueue(command);
                _commandHistory.Add(command);
            }

            return command;
        }

        public CommandRecord? ExecuteNextCommand()
        {
            lock (_lock)
            {
                if (_commandQueue.Count == 0)
                    return null;

                var command = _commandQueue.Dequeue();

                if (!_dataStore.SensorLookup.TryGetValue(command.DeviceIdentifier, out var sensor))
                {
                    command.Status = "Failed";
                    command.ResultMessage = "Sensor no longer exists.";
                    command.ExecutedAt = DateTime.UtcNow;
                    return command;
                }

                command.PreviousState = Apply(command.CommandType, sensor);
                command.Status = "Executed";
                command.ExecutedAt = DateTime.UtcNow;
                command.CanUndo = true;
                command.ResultMessage = command.CommandType switch
                {
                    TogglePump => $"Pump is now {(_pumpOn[sensor.DeviceIdentifier] ? "ON" : "OFF")}.",
                    ToggleRelay => $"Relay is now {(_relayOn[sensor.DeviceIdentifier] ? "ON" : "OFF")}.",
                    _ => $"Telemetry is now {(sensor.TelemetryEnabled ? "enabled" : "disabled")}."
                };

                _undoStack.Push(command);
                return command;
            }
        }

        public CommandRecord? UndoLastCommand()
        {
            lock (_lock)
            {
                if (_undoStack.Count == 0)
                    return null;

                var command = _undoStack.Pop();
                command.CanUndo = false;

                if (!command.PreviousState.HasValue ||
                    !_dataStore.SensorLookup.TryGetValue(command.DeviceIdentifier, out var sensor))
                {
                    command.ResultMessage = "Undo failed: the device or its previous state is unavailable.";
                    return command;   // IsUndone stays false, so the controller returns 409
                }

                Restore(command.CommandType, sensor, command.PreviousState.Value);

                command.IsUndone = true;
                command.Status = "Undone";
                command.ResultMessage = "Command undone successfully.";
                return command;
            }
        }

        // Applies the command and returns the state before it ran.
        private bool Apply(string commandType, Sensor sensor)
        {
            switch (commandType)
            {
                case EnableTelemetry:
                    {
                        var previous = sensor.TelemetryEnabled;
                        sensor.TelemetryEnabled = true;
                        return previous;
                    }
                case DisableTelemetry:
                    {
                        var previous = sensor.TelemetryEnabled;
                        sensor.TelemetryEnabled = false;
                        return previous;
                    }
                case TogglePump:
                    return Toggle(_pumpOn, sensor.DeviceIdentifier);
                case ToggleRelay:
                    return Toggle(_relayOn, sensor.DeviceIdentifier);
                default:
                    throw new InvalidOperationException($"Unknown command {commandType}.");
            }
        }

        private void Restore(string commandType, Sensor sensor, bool previous)
        {
            switch (commandType)
            {
                case EnableTelemetry:
                case DisableTelemetry:
                    sensor.TelemetryEnabled = previous;
                    break;
                case TogglePump:
                    _pumpOn[sensor.DeviceIdentifier] = previous;
                    break;
                case ToggleRelay:
                    _relayOn[sensor.DeviceIdentifier] = previous;
                    break;
            }
        }

        private static bool Toggle(Dictionary<string, bool> states, string deviceIdentifier)
        {
            states.TryGetValue(deviceIdentifier, out var previous);
            states[deviceIdentifier] = !previous;
            return previous;
        }
    }
}