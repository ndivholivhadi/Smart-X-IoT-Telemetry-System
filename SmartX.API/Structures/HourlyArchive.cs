namespace SmartX.API.Structures
{
    public struct HourlyBucket
    {
        public long Hour;
        public int Count;
        public double Sum;
        public double Min;
        public double Max;
    }

    public sealed record HourlySummary(DateTime HourStartUtc, int Count, double Min, double Max, double Average);

    public sealed class HourlyArchive
    {
        public const int SlotsPerDevice = 24 * 7;

        private readonly Dictionary<string, int> _rowByDevice = new(StringComparer.OrdinalIgnoreCase);
        private HourlyBucket[][] _rows = new HourlyBucket[4][];
        private int _rowCount;

        public int DeviceCount => _rowCount;

        public void Add(string deviceIdentifier, DateTime timestamp, double value)
        {
            var row = GetOrCreateRow(deviceIdentifier.Trim());
            long hour = timestamp.Ticks / TimeSpan.TicksPerHour;
            ref var bucket = ref row[(int)(hour % SlotsPerDevice)];

            if (bucket.Count == 0 || hour > bucket.Hour)
            {
                // Empty slot, or a newer hour reusing the slot (the week-old hour is dropped).
                bucket = new HourlyBucket { Hour = hour, Count = 1, Sum = value, Min = value, Max = value };
            }
            else if (hour == bucket.Hour)
            {
                bucket.Count++;
                bucket.Sum += value;
                bucket.Min = Math.Min(bucket.Min, value);
                bucket.Max = Math.Max(bucket.Max, value);
            }
            // else: older than the week we keep, so it is ignored.
        }

        public List<HourlySummary> Query(string deviceIdentifier, DateTime from, DateTime to)
        {
            var results = new List<HourlySummary>();

            if (!_rowByDevice.TryGetValue(deviceIdentifier.Trim(), out var index))
                return results;

            long fromHour = from.Ticks / TimeSpan.TicksPerHour;
            long toHour = to.Ticks / TimeSpan.TicksPerHour;

            foreach (var bucket in _rows[index])
            {
                if (bucket.Count > 0 && bucket.Hour >= fromHour && bucket.Hour <= toHour)
                {
                    results.Add(new HourlySummary(
                        new DateTime(bucket.Hour * TimeSpan.TicksPerHour, DateTimeKind.Utc),
                        bucket.Count, bucket.Min, bucket.Max, bucket.Sum / bucket.Count));
                }
            }

            results.Sort((a, b) => a.HourStartUtc.CompareTo(b.HourStartUtc));
            return results;
        }

        /// <summary>Total readings folded into this device's archive.</summary>
        public int TotalReadings(string deviceIdentifier)
            => _rowByDevice.TryGetValue(deviceIdentifier.Trim(), out var index)
                ? _rows[index].Sum(b => b.Count)
                : 0;

        private HourlyBucket[] GetOrCreateRow(string device)
        {
            if (_rowByDevice.TryGetValue(device, out var index))
                return _rows[index];

            if (_rowCount == _rows.Length)
                Array.Resize(ref _rows, _rows.Length * 2);      // grow the outer array

            _rows[_rowCount] = new HourlyBucket[SlotsPerDevice]; // a new row for this device
            _rowByDevice[device] = _rowCount;
            return _rows[_rowCount++];
        }
    }
}
