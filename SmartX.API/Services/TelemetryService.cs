using SmartX.API.Data;
using SmartX.API.Models;

namespace SmartX.API.Services
{
    public class TelemetryService
    {
        private readonly SmartXDataStore _dataStore;
        private readonly AnomalyDetectionService _anomalyDetectionService;

        // When true, every Add... call drains its queue straight away. The unit tests use this
        // (the two shorter constructors). The running API uses false (see Program.cs), so
        // telemetry waits in the queue until TelemetryIngestionWorker consumes it.
        private readonly bool _processInline;

        public TelemetryService(SmartXDataStore dataStore)
            : this(dataStore, new AnomalyDetectionService(dataStore), true)
        {
        }

        public TelemetryService(SmartXDataStore dataStore, AnomalyDetectionService anomalyDetectionService)
            : this(dataStore, anomalyDetectionService, true)
        {
        }

        // queues are used to represent telemetry
        // waiting to be processed by the ingestion layer.
        private readonly Queue<TelemetryPacket<float>> _floatQueue = new();
        private readonly Queue<TelemetryPacket<int>> _integerQueue = new();
        private readonly Queue<TelemetryPacket<bool>> _booleanQueue = new();
        private readonly Dictionary<string, int> _deviceTelemetryCounts = new();


        private void UpdateDeviceTelemetryCount(string deviceIdentifier)
        {
            if (_deviceTelemetryCounts.ContainsKey(deviceIdentifier))
            {
                _deviceTelemetryCounts[deviceIdentifier]++;
            }
            else
            {
                _deviceTelemetryCounts[deviceIdentifier] = 1;
            }
        }

        private readonly object _queueLock = new();

        private long _telemetryGenerated;
        private long _telemetryTransmitted;
        private long _telemetryReceived;

        private DateTime? _lastTelemetryReceived;

        public TelemetryService(SmartXDataStore dataStore, AnomalyDetectionService anomalyDetectionService, bool processInline)
        {
            _dataStore = dataStore;
            _anomalyDetectionService = anomalyDetectionService;
            _processInline = processInline;
        }

        /// <summary>
        /// Takes up to maxPerQueue packets from each queue (oldest first), stores them,
        /// updates LastSeen and runs anomaly checks. Returns how many were processed.
        /// </summary>
        public int ProcessPendingTelemetry(int maxPerQueue = 500)
        {
            int processed = 0;

            lock (_queueLock)
            {
                processed += ProcessFloatQueue(maxPerQueue);
                processed += ProcessIntegerQueue(maxPerQueue);
                processed += ProcessBooleanQueue(maxPerQueue);
            }

            return processed;
        }

        // =================
        // FLOAT TELEMETRY
        // ================

        public void AddFloatTelemetry(TelemetryPacket<float> packet)
        {
            lock (_queueLock)
            {
                _telemetryGenerated++;
                _floatQueue.Enqueue(packet);
                _telemetryTransmitted++;

                UpdateDeviceTelemetryCount(packet.DeviceIdentifier);
            }

            if (_processInline)
                ProcessFloatQueue();
        }

        private int ProcessFloatQueue(int max = int.MaxValue)
        {
            int count = 0;

            lock (_queueLock)
            {
                while (_floatQueue.Count > 0 && count < max)
                {
                    var packet = _floatQueue.Dequeue();

                    _telemetryReceived++;

                    _dataStore.StoreFloatTelemetry(packet);

                    UpdateSensorLastSeen(packet.DeviceIdentifier);

                    _anomalyDetectionService.CheckSoilMoisture(packet);

                    _lastTelemetryReceived = packet.Timestamp;
                    count++;
                }
            }

            return count;
        }

        // =====================
        // INTEGER TELEMETRY
        // =====================

        public void AddIntegerTelemetry(TelemetryPacket<int> packet)
        {
            lock (_queueLock)
            {
                _telemetryGenerated++;
                _integerQueue.Enqueue(packet);
                _telemetryTransmitted++;

                UpdateDeviceTelemetryCount(packet.DeviceIdentifier);
            }

            if (_processInline)
                ProcessIntegerQueue();
        }

        private int ProcessIntegerQueue(int max = int.MaxValue)
        {
            int count = 0;

            lock (_queueLock)
            {
                while (_integerQueue.Count > 0 && count < max)
                {
                    var packet = _integerQueue.Dequeue();

                    _telemetryReceived++;

                    _dataStore.StoreIntegerTelemetry(packet);

                    UpdateSensorLastSeen(packet.DeviceIdentifier);

                    _anomalyDetectionService.CheckPowerConsumption(packet);

                    _lastTelemetryReceived = packet.Timestamp;
                    count++;
                }
            }

            return count;
        }

        // ======================
        // BOOLEAN TELEMETRY
        // ======================

        public void AddBooleanTelemetry(TelemetryPacket<bool> packet)
        {
            lock (_queueLock)
            {
                _telemetryGenerated++;
                _booleanQueue.Enqueue(packet);
                _telemetryTransmitted++;

                UpdateDeviceTelemetryCount(packet.DeviceIdentifier);
            }

            if (_processInline)
                ProcessBooleanQueue();
        }

        private int ProcessBooleanQueue(int max = int.MaxValue)
        {
            int count = 0;

            lock (_queueLock)
            {
                while (_booleanQueue.Count > 0 && count < max)
                {
                    var packet = _booleanQueue.Dequeue();

                    _telemetryReceived++;

                    _dataStore.StoreBooleanTelemetry(packet);

                    UpdateSensorLastSeen(packet.DeviceIdentifier);

                    _lastTelemetryReceived = packet.Timestamp;
                    count++;
                }
            }

            return count;
        }


        public List<TelemetryPacket<float>> GetFloatTelemetryHistory(string? deviceIdentifier = null)
        {
            return _dataStore.GetFloatTelemetryHistorySnapshot(deviceIdentifier);
        }

        public List<TelemetryPacket<int>> GetIntegerTelemetryHistory(string? deviceIdentifier = null)
        {
            return _dataStore.GetIntegerTelemetryHistorySnapshot(deviceIdentifier);
        }

        public List<TelemetryPacket<bool>> GetBooleanTelemetryHistory(string? deviceIdentifier = null)
        {
            return _dataStore.GetBooleanTelemetryHistorySnapshot(deviceIdentifier);
        }


        private void UpdateSensorLastSeen(string deviceIdentifier)
        {
            if (string.IsNullOrWhiteSpace(deviceIdentifier))
            {
                return;
            }

            if (_dataStore.SensorLookup.TryGetValue(
                deviceIdentifier.Trim(), out var sensor))
            {
                sensor.LastSeen = DateTime.UtcNow;
                sensor.Status = SensorStatus.Online;
            }


        }


        // ==============================
        // RETRIEVE TELEMETRY
        // ==============================


        public IEnumerable<TelemetryPacket<float>> GetFloatTelemetry()
        {
            return _dataStore.GetFloatTelemetrySnapshot();
        }

        public IEnumerable<TelemetryPacket<int>> GetIntegerTelemetry()
        {
            return _dataStore.GetIntegerTelemetrySnapshot();
        }

        public IEnumerable<TelemetryPacket<bool>> GetBooleanTelemetry()
        {
            return _dataStore.GetBooleanTelemetrySnapshot();
        }

        // ==============================
        // STATISTICS
        // ==============================


        public TelemetryStatistics GetStatistics()
        {
            lock (_queueLock)
            {
                var floatTelemetry = _dataStore.GetFloatTelemetrySnapshot();
                var integerTelemetry = _dataStore.GetIntegerTelemetrySnapshot();
                var booleanTelemetry = _dataStore.GetBooleanTelemetrySnapshot();

                return new TelemetryStatistics
                {
                    TelemetryGenerated = _telemetryGenerated,
                    TelemetryTransmitted = _telemetryTransmitted,
                    TelemetryReceived = _telemetryReceived,

                    TotalRecordsStored =
                        floatTelemetry.Count +
                        integerTelemetry.Count +
                        booleanTelemetry.Count,

                    LastTelemetryReceived = _lastTelemetryReceived,

                    PendingTelemetry =
                        _floatQueue.Count +
                        _integerQueue.Count +
                        _booleanQueue.Count
                };
            }
        }

        public Dictionary<string, int> GetDeviceTelemetryCounts()
        {
            lock (_queueLock)
            {
                return new Dictionary<string, int>(_deviceTelemetryCounts);
            }
        }
    }
}