using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmartX.API.Models;
using SmartX.API.Structures;

namespace SmartX.API.Data
{
    public class SmartXDataStore
    {
        private readonly string _sensorFilePath;
        private readonly string _attachmentFilePath;

        //Folder for sensors.json, attachments.json and the attachment key
        public string DataDirectory { get; }

        private readonly object _telemetryLock = new();

        public List<Sensor> Sensors { get; } = new();

        public Dictionary<string, Sensor> SensorLookup { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<TelemetryPacket<float>> FloatTelemetry { get; } = new();

        public List<TelemetryPacket<int>> IntegerTelemetry { get; } = new();

        public List<TelemetryPacket<bool>> BooleanTelemetry { get; } = new();

        public List<Alert> Alerts { get; } = new();

        public List<Attachment> Attachments { get; } = new();

        public List<TelemetrySearchRecord> TelemetrySearchHistory { get; } = new();

        // Per-device AVL-tree history, capped so memory use cannot grow forever.
        public const int MaxHistoryPerDevice = 1000;

        // The flat lists below feed statistics and search; they are capped too.
        public const int MaxFlatRecords = 50_000;

        public DeviceHistoryIndex<float> FloatHistory { get; }

        public DeviceHistoryIndex<int> IntegerHistory { get; }

        public DeviceHistoryIndex<bool> BooleanHistory { get; }

        // Older readings, summarised per hour in a jagged array (see HourlyArchive).
        public HourlyArchive FloatArchive { get; } = new();

        public HourlyArchive IntegerArchive { get; } = new();

        public HourlyArchive BooleanArchive { get; } = new();

        // MAC address -> sensor (the device identifier lookup is SensorLookup).
        public Dictionary<string, Sensor> MacLookup { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> ActiveErrors { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> DisconnectedDevices { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<TelemetryPacket<float>> GetFloatTelemetryHistorySnapshot(string? deviceIdentifier = null)
        {
            lock (_telemetryLock)
            {
                return string.IsNullOrWhiteSpace(deviceIdentifier)
                    ? FloatHistory.All()
                    : FloatHistory.ForDevice(deviceIdentifier);
            }
        }

        public List<TelemetryPacket<int>> GetIntegerTelemetryHistorySnapshot(string? deviceIdentifier = null)
        {
            lock (_telemetryLock)
            {
                return string.IsNullOrWhiteSpace(deviceIdentifier)
                    ? IntegerHistory.All()
                    : IntegerHistory.ForDevice(deviceIdentifier);
            }
        }

        public List<TelemetryPacket<bool>> GetBooleanTelemetryHistorySnapshot(string? deviceIdentifier = null)
        {
            lock (_telemetryLock)
            {
                return string.IsNullOrWhiteSpace(deviceIdentifier)
                    ? BooleanHistory.All()
                    : BooleanHistory.ForDevice(deviceIdentifier);
            }
        }

        // Time-range queries: O(log n + k) on the device's AVL tree.
        public List<TelemetryPacket<float>> GetFloatTelemetryRange(string deviceIdentifier, DateTime fromUtc, DateTime toUtc)
        {
            lock (_telemetryLock) { return FloatHistory.Range(deviceIdentifier, fromUtc, toUtc); }
        }

        public List<TelemetryPacket<int>> GetIntegerTelemetryRange(string deviceIdentifier, DateTime fromUtc, DateTime toUtc)
        {
            lock (_telemetryLock) { return IntegerHistory.Range(deviceIdentifier, fromUtc, toUtc); }
        }

        public List<TelemetryPacket<bool>> GetBooleanTelemetryRange(string deviceIdentifier, DateTime fromUtc, DateTime toUtc)
        {
            lock (_telemetryLock) { return BooleanHistory.Range(deviceIdentifier, fromUtc, toUtc); }
        }

        // Stored reading count and tree height for one device (shown in the UI).
        public (int Count, int Height) GetHistoryShape(string metric, string deviceIdentifier)
        {
            lock (_telemetryLock)
            {
                return metric switch
                {
                    "soil" => (FloatHistory.CountFor(deviceIdentifier), FloatHistory.TreeHeightFor(deviceIdentifier)),
                    "power" => (IntegerHistory.CountFor(deviceIdentifier), IntegerHistory.TreeHeightFor(deviceIdentifier)),
                    _ => (BooleanHistory.CountFor(deviceIdentifier), BooleanHistory.TreeHeightFor(deviceIdentifier))
                };
            }
        }

        public SmartXDataStore(string? dataDirectory = null)
        {
            DataDirectory = dataDirectory ?? Path.Combine(AppContext.BaseDirectory, "Data");
            _sensorFilePath = Path.Combine(DataDirectory, "sensors.json");
            _attachmentFilePath = Path.Combine(DataDirectory, "attachments.json");

            // A reading that falls out of the recent-history tree is archived, not lost.
            FloatHistory = new(MaxHistoryPerDevice, p => FloatArchive.Add(p.DeviceIdentifier, p.Timestamp, p.Value));
            IntegerHistory = new(MaxHistoryPerDevice, p => IntegerArchive.Add(p.DeviceIdentifier, p.Timestamp, p.Value));
            BooleanHistory = new(MaxHistoryPerDevice, p => BooleanArchive.Add(p.DeviceIdentifier, p.Timestamp, p.Value ? 1 : 0));

            LoadSensors();
            RebuildSensorLookup();
            LoadAttachments();
        }

        // Attachment details (not the files) are saved so downloads still work after a restart.
        public void SaveAttachments()
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                List<Attachment> copy;

                lock (_telemetryLock)
                {
                    copy = Attachments.ToList();
                }

                File.WriteAllText(_attachmentFilePath,
                    JsonSerializer.Serialize(copy, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save attachments: {ex.Message}");
            }
        }

        public void AddAttachment(Attachment attachment)
        {
            lock (_telemetryLock)
            {
                Attachments.Add(attachment);
            }

            SaveAttachments();
        }

        public Attachment? FindAttachment(Guid sensorId, Guid attachmentId)
        {
            lock (_telemetryLock)
            {
                return Attachments.FirstOrDefault(a => a.Id == attachmentId && a.SensorId == sensorId);
            }
        }

        public List<Attachment> GetSensorAttachments(Guid sensorId)
        {
            lock (_telemetryLock)
            {
                return Attachments.Where(a => a.SensorId == sensorId).ToList();
            }
        }

        private void LoadAttachments()
        {
            try
            {
                if (!File.Exists(_attachmentFilePath))
                    return;

                var attachments = JsonSerializer.Deserialize<List<Attachment>>(File.ReadAllText(_attachmentFilePath));

                if (attachments != null)
                    Attachments.AddRange(attachments);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load attachments: {ex.Message}");
            }
        }

        // Archived (older) readings as hourly summaries, for one device and reading type.
        public List<HourlySummary> GetArchive(string metric, string deviceIdentifier, DateTime fromUtc, DateTime toUtc)
        {
            lock (_telemetryLock)
            {
                var archive = metric switch { "soil" => FloatArchive, "power" => IntegerArchive, _ => BooleanArchive };
                return archive.Query(deviceIdentifier, fromUtc, toUtc);
            }
        }

        // One lock for everything alert-related (Alerts, ActiveErrors, DisconnectedDevices).
        public object AlertLock { get; } = new();

        public List<TelemetrySearchRecord> GetSearchHistorySnapshot()
        {
            lock (_telemetryLock)
            {
                return TelemetrySearchHistory.ToList();
            }
        }

        public void SaveSensors()
        {
            try
            {
                var directory = Path.GetDirectoryName(_sensorFilePath);

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(
                    Sensors,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(_sensorFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Failed to save sensors: {ex.Message}");
            }
        }

        private void LoadSensors()
        {
            try
            {
                if (!File.Exists(_sensorFilePath))
                {
                    return;
                }

                var json = File.ReadAllText(_sensorFilePath);

                var sensors = JsonSerializer.Deserialize<List<Sensor>>(
                    json);

                if (sensors != null)
                {
                    Sensors.AddRange(sensors);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Failed to load sensors: {ex.Message}");
            }
        }

        public void RebuildSensorLookup()
        {
            SensorLookup.Clear();
            MacLookup.Clear();

            foreach (var sensor in Sensors)
            {
                if (!string.IsNullOrWhiteSpace(sensor.DeviceIdentifier))
                    IndexSensor(sensor);
            }
        }

        // Adds a sensor to both lookups (device identifier and MAC address).
        public void IndexSensor(Sensor sensor)
        {
            sensor.DeviceIdentifier = sensor.DeviceIdentifier.Trim();
            sensor.MacAddress = string.IsNullOrWhiteSpace(sensor.MacAddress)
                ? GenerateMacAddress(sensor.DeviceIdentifier)
                : NormalizeMac(sensor.MacAddress);

            SensorLookup.TryAdd(sensor.DeviceIdentifier, sensor);
            MacLookup.TryAdd(sensor.MacAddress, sensor);
        }

        // Finds a sensor by device identifier OR MAC address.
        public Sensor? FindSensor(string? identifierOrMac)
        {
            if (string.IsNullOrWhiteSpace(identifierOrMac))
                return null;

            var key = identifierOrMac.Trim();

            if (SensorLookup.TryGetValue(key, out var sensor))
                return sensor;

            return MacLookup.TryGetValue(NormalizeMac(key), out sensor) ? sensor : null;
        }

        private static readonly Regex MacPattern =
            new("^([0-9A-F]{2}:){5}[0-9A-F]{2}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

        public static string NormalizeMac(string mac)
            => mac.Trim().ToUpperInvariant().Replace('-', ':');

        public static bool IsValidMac(string mac)
            => MacPattern.IsMatch(NormalizeMac(mac));

        // Sensors saved before MAC addresses existed get a stable one derived from
        // their identifier (24:6F:28 is an Espressif prefix, like a real ESP32).
        public static string GenerateMacAddress(string deviceIdentifier)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(deviceIdentifier));
            return $"24:6F:28:{hash[0]:X2}:{hash[1]:X2}:{hash[2]:X2}";
        }

        public void StoreFloatTelemetry(TelemetryPacket<float> packet)
        {
            lock (_telemetryLock)
            {
                FloatTelemetry.Add(packet);
                TrimFlat(FloatTelemetry);

                FloatHistory.Add(packet);     // per-device AVL tree (capped)
            }
        }

        public void StoreIntegerTelemetry(TelemetryPacket<int> packet)
        {
            lock (_telemetryLock)
            {
                IntegerTelemetry.Add(packet);
                TrimFlat(IntegerTelemetry);

                IntegerHistory.Add(packet);     // per-device AVL tree (capped)
            }
        }

        public void StoreBooleanTelemetry(TelemetryPacket<bool> packet)
        {
            lock (_telemetryLock)
            {
                BooleanTelemetry.Add(packet);
                TrimFlat(BooleanTelemetry);

                BooleanHistory.Add(packet);     // per-device AVL tree (capped)
            }
        }


        public List<TelemetryPacket<float>> GetFloatTelemetrySnapshot()
        {
            lock (_telemetryLock)
            {
                return FloatTelemetry.ToList();
            }
        }

        public List<TelemetryPacket<int>> GetIntegerTelemetrySnapshot()
        {
            lock (_telemetryLock)
            {
                return IntegerTelemetry.ToList();
            }
        }

        public List<TelemetryPacket<bool>> GetBooleanTelemetrySnapshot()
        {
            lock (_telemetryLock)
            {
                return BooleanTelemetry.ToList();
            }
        }
        private static void TrimFlat<T>(List<T> list)
        {
            // Trim in batches so the O(n) removal does not run on every insert.
            if (list.Count > MaxFlatRecords + MaxFlatRecords / 10)
                list.RemoveRange(0, list.Count - MaxFlatRecords);
        }

        public void RecordTelemetrySearch(TelemetrySearchRecord record)
        {
            lock (_telemetryLock)
            {
                TelemetrySearchHistory.Add(record);

                // Keep the action log bounded (it feeds the predictive engine).
                if (TelemetrySearchHistory.Count > 5000)
                    TelemetrySearchHistory.RemoveRange(0, 500);
            }
        }

    }
}