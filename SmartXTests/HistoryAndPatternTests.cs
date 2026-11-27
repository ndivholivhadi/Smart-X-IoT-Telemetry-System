using SmartX.API.Data;
using SmartX.API.Models;
using SmartX.API.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartX.Tests
{
    public class HistoryAndPatternTests
    {
        [Fact]
        public void SortedHistory_ShouldReturnReadingsInTimestampOrder()
        {
            var store = new SmartXDataStore();
            var now = DateTime.UtcNow;

            store.StoreFloatTelemetry(new TelemetryPacket<float> { DeviceIdentifier = "D", Timestamp = now.AddMinutes(2), Value = 3 });
            store.StoreFloatTelemetry(new TelemetryPacket<float> { DeviceIdentifier = "D", Timestamp = now, Value = 1 });
            store.StoreFloatTelemetry(new TelemetryPacket<float> { DeviceIdentifier = "D", Timestamp = now.AddMinutes(1), Value = 2 });

            var values = store.GetFloatTelemetryHistorySnapshot("D").Select(p => p.Value).ToList();

            Assert.Equal(new float[] { 1, 2, 3 }, values);
        }

        [Fact]
        public void PatternAnalyser_ShouldFindRepeatedFollowUpAfterAlert()
        {
            var t0 = DateTime.UtcNow;
            var alerts = new[]
            {
            new Alert { DeviceIdentifier = "SENSOR-B", Timestamp = t0 },
            new Alert { DeviceIdentifier = "SENSOR-B", Timestamp = t0.AddMinutes(30) }
        };
            var searches = new[]
            {
            new TelemetrySearchRecord { DeviceIdentifier = "PUMP-A", SearchedAt = t0.AddMinutes(1) },
            new TelemetrySearchRecord { DeviceIdentifier = "PUMP-A", SearchedAt = t0.AddMinutes(31) }
        };

            var analyser = new ActionPatternAnalyser();
            var transitions = analyser.BuildTransitions(alerts, searches, Array.Empty<CommandRecord>());
            var patterns = analyser.FindLikelyFollowUps(transitions, new[] { "SENSOR-B" });

            var pattern = Assert.Single(patterns);
            Assert.Equal("PUMP-A", pattern.FollowUpDevice);
            Assert.Equal("Searched", pattern.FollowUpType);
            Assert.Equal(2, pattern.Count);
        }


        [Fact]
        public void SortedHistory_ShouldFilterReadingsByDeviceIdentifier()
        {
            var store = new SmartXDataStore();
            var timestamp = DateTime.UtcNow;

            store.StoreFloatTelemetry(new TelemetryPacket<float>
            {
                DeviceIdentifier = "DEVICE-A",
                Timestamp = timestamp,
                Value = 10
            });

            store.StoreFloatTelemetry(new TelemetryPacket<float>
            {
                DeviceIdentifier = "DEVICE-B",
                Timestamp = timestamp.AddMinutes(1),
                Value = 20
            });

            store.StoreFloatTelemetry(new TelemetryPacket<float>
            {
                DeviceIdentifier = "DEVICE-A",
                Timestamp = timestamp.AddMinutes(2),
                Value = 30
            });

            var results = store
                .GetFloatTelemetryHistorySnapshot("DEVICE-A")
                .ToList();

            Assert.Equal(2, results.Count);
            Assert.All(results, reading =>
                Assert.Equal("DEVICE-A", reading.DeviceIdentifier));
            Assert.Equal(new float[] { 10, 30 },
                results.Select(reading => reading.Value).ToArray());
        }

        [Fact]
        public void SortedHistory_ShouldRetainMultipleReadingsWithSameTimestamp()
        {
            var store = new SmartXDataStore();
            var timestamp = DateTime.UtcNow;

            store.StoreFloatTelemetry(new TelemetryPacket<float>
            {
                DeviceIdentifier = "DEVICE-A",
                Timestamp = timestamp,
                Value = 10
            });

            store.StoreFloatTelemetry(new TelemetryPacket<float>
            {
                DeviceIdentifier = "DEVICE-A",
                Timestamp = timestamp,
                Value = 20
            });

            var results = store
                .GetFloatTelemetryHistorySnapshot("DEVICE-A")
                .ToList();

            Assert.Equal(2, results.Count);
            Assert.Equal(new float[] { 10, 20 },
                results.Select(reading => reading.Value).ToArray());
        }

    }
}