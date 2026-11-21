using System.Diagnostics;
using SmartX.API.Data;
using SmartX.API.Models;

namespace SmartX.API.Services
{
    public class TelemetrySearchService
    {
        private readonly SmartXDataStore _dataStore;

        public TelemetrySearchService(SmartXDataStore dataStore)
        {
            _dataStore = dataStore;
        }

        public TelemetrySearchResult<float> SearchSoilMoisture(string deviceIdentifier)
        {
            var stopwatch = Stopwatch.StartNew();

            var results = _dataStore.GetFloatTelemetrySnapshot()
                .Where(x =>
                    x.DeviceIdentifier.Equals(
                        deviceIdentifier,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            stopwatch.Stop();

            var result = new TelemetrySearchResult<float>
            {
                DeviceIdentifier = deviceIdentifier,
                Results = results,
                SearchAlgorithm = "LINQ filtering",
                SearchTimeMicroseconds = stopwatch.ElapsedTicks * 1_000_000 / Stopwatch.Frequency,
                ResultsFound = results.Count
            };

            RecordSearch(deviceIdentifier, "SoilMoisture", result);

            return result;
        }

        public TelemetrySearchResult<int> SearchPower(string deviceIdentifier)
        {
            var stopwatch = Stopwatch.StartNew();

            var results = _dataStore.GetIntegerTelemetrySnapshot()
                .Where(x =>
                    x.DeviceIdentifier.Equals(
                        deviceIdentifier,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            stopwatch.Stop();

            var result = new TelemetrySearchResult<int>
            {
                DeviceIdentifier = deviceIdentifier,
                Results = results,
                SearchAlgorithm = "LINQ filtering",
                SearchTimeMicroseconds =
                    stopwatch.ElapsedTicks * 1_000_000 /
                    Stopwatch.Frequency,
                ResultsFound = results.Count
            };

            RecordSearch(deviceIdentifier, "Power", result);
            return result;
        }

        public TelemetrySearchResult<bool> SearchValve(string deviceIdentifier)
        {
            var stopwatch = Stopwatch.StartNew();

            var results = _dataStore.GetBooleanTelemetrySnapshot()
                .Where(x =>
                    x.DeviceIdentifier.Equals(
                        deviceIdentifier,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            stopwatch.Stop();

            var result = new TelemetrySearchResult<bool>
            {
                DeviceIdentifier = deviceIdentifier,
                Results = results,
                SearchAlgorithm = "LINQ filtering",
                SearchTimeMicroseconds =
                    stopwatch.ElapsedTicks * 1_000_000 /
                    Stopwatch.Frequency,
                ResultsFound = results.Count
            };

            RecordSearch(deviceIdentifier, "Valve", result);
            return result;
        }

        private void RecordSearch<T>(string deviceIdentifier, string category, TelemetrySearchResult<T> result)
        {

            _dataStore.RecordTelemetrySearch(
                new TelemetrySearchRecord
                {
                    DeviceIdentifier = deviceIdentifier.Trim(),
                    TelemetryCategory = category,
                    SearchedAt = DateTime.UtcNow,
                    ResultsFound = result.ResultsFound,
                    SearchTimeMicroseconds = result.SearchTimeMicroseconds
                });
        }
    }
}