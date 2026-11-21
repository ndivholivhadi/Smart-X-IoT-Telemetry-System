using SmartX.API.Models;

namespace SmartX.API.Structures
{
    //summary>Unique, sortable key for a reading: timestamp first, then arrival order
    public readonly record struct TelemetryKey(long Ticks, long Sequence) : IComparable<TelemetryKey>
    {
        public int CompareTo(TelemetryKey other)
        {
            int byTime = Ticks.CompareTo(other.Ticks);
            return byTime != 0 ? byTime : Sequence.CompareTo(other.Sequence);
        }
    }

    /// <summary>
    /// Historical telemetry for one data type, kept as one AVL tree PER DEVICE
    /// (found through a dictionary), and capped at maxPerDevice readings.
    /// When a device goes over its cap the oldest reading is removed, so memory
    /// use is bounded however long the API runs.
    /// Not thread-safe by itself: SmartXDataStore guards it with its telemetry lock.
    /// </summary>
    public sealed class DeviceHistoryIndex<T>
    {
        private readonly Dictionary<string, AvlTree<TelemetryKey, TelemetryPacket<T>>> _byDevice
            = new(StringComparer.OrdinalIgnoreCase);

        private readonly int _maxPerDevice;
        private readonly Action<TelemetryPacket<T>>? _onEvict;
        private long _sequence;

        public DeviceHistoryIndex(int maxPerDevice, Action<TelemetryPacket<T>>? onEvict = null)
        {
            if (maxPerDevice <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPerDevice));

            _maxPerDevice = maxPerDevice;
            _onEvict = onEvict;
        }

        public int DeviceCount => _byDevice.Count;

        public int CountFor(string deviceIdentifier)
            => _byDevice.TryGetValue(deviceIdentifier.Trim(), out var tree) ? tree.Count : 0;

        public int TreeHeightFor(string deviceIdentifier)
            => _byDevice.TryGetValue(deviceIdentifier.Trim(), out var tree) ? tree.Height : 0;

        public void Add(TelemetryPacket<T> packet)
        {
            var device = packet.DeviceIdentifier.Trim();

            if (!_byDevice.TryGetValue(device, out var tree))
            {
                tree = new AvlTree<TelemetryKey, TelemetryPacket<T>>();
                _byDevice[device] = tree;
            }

            tree.Insert(new TelemetryKey(packet.Timestamp.Ticks, ++_sequence), packet);

            while (tree.Count > _maxPerDevice)
            {
                if (tree.TryRemoveMin(out var oldest))
                    _onEvict?.Invoke(oldest);
            }
        }

        //summary>Readings for one device between from and to (inclusive), oldest first
        public List<TelemetryPacket<T>> Range(string deviceIdentifier, DateTime from, DateTime to)
        {
            if (!_byDevice.TryGetValue(deviceIdentifier.Trim(), out var tree))
                return new List<TelemetryPacket<T>>();

            return tree.Range(
                new TelemetryKey(from.Ticks, long.MinValue),
                new TelemetryKey(to.Ticks, long.MaxValue));
        }

        //Every reading for one device, oldest first
        public List<TelemetryPacket<T>> ForDevice(string deviceIdentifier)
            => _byDevice.TryGetValue(deviceIdentifier.Trim(), out var tree)
                ? tree.InOrder()
                : new List<TelemetryPacket<T>>();

        //Every reading for every device, oldest first
        public List<TelemetryPacket<T>> All()
        {
            var all = new List<TelemetryPacket<T>>();

            foreach (var tree in _byDevice.Values)
                all.AddRange(tree.InOrder());

            // Stable sort keeps arrival order for identical timestamps.
            return all.OrderBy(p => p.Timestamp).ToList();
        }
    }
}
