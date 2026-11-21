
using SmartX.API.Data;
using SmartX.API.Models;

namespace SmartX.API.Services
{
    public class RecommendationService
    {
        private readonly SmartXDataStore _dataStore;
        private readonly CommandService _commandService;
        private readonly ActionPatternAnalyser _patternAnalyser = new();

        public RecommendationService(SmartXDataStore dataStore, CommandService commandService)
        {
            _dataStore = dataStore;
            _commandService = commandService;
        }


        public object GetRecommendations()
        {

            List<Alert> alerts;
            lock (_dataStore.AlertLock)
            {
                alerts = _dataStore.Alerts.ToList();
            }
            var suggestions = new List<object>();
            var insights = new List<string>();

            // 1. Identify unresolved critical alerts.
            var criticalAlerts = alerts
                .Where(a => !a.IsResolved &&
                    string.Equals(
                        a.Severity,
                        "Critical",
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var alert in criticalAlerts)
            {
                suggestions.Add(new
                {
                    DeviceIdentifier = alert.DeviceIdentifier,
                    SuggestedAction = "Investigate critical alert",
                    Reason = alert.Message,
                    Priority = "Critical"
                });
            }

            // 2. Identify offline devices.
            var offlineDevices = _dataStore.Sensors
                .Where(s => s.Status == SensorStatus.Offline)
                .Select(s => s.DeviceIdentifier)
                .ToList();

            foreach (var device in offlineDevices)
            {
                suggestions.Add(new
                {
                    DeviceIdentifier = device,
                    SuggestedAction = "Check device connectivity",
                    Reason = "The device is currently offline.",
                    Priority = "High"
                });
            }

            // 3. Review recent commands for recurring behaviour.
            var recentCommands = _commandService.GetHistory()
                .OrderByDescending(c => c.CreatedAt)
                .Take(20)
                .ToList();


            foreach (var group in recentCommands
                .GroupBy(c => c.DeviceIdentifier))
            {
                var disableCount = group.Count(c =>
                    string.Equals(
                        c.CommandType,
                        "DisableTelemetry",
                        StringComparison.OrdinalIgnoreCase));

                if (disableCount >= 2)
                {
                    insights.Add(
                        $"{group.Key} has had telemetry disabled " +
                        $"{disableCount} times in recent command history. " +
                        "Review whether monitoring should be restored.");
                }
            }

            // 4. Add general insights ONCE, outside the command loop.
            if (recentCommands.Count == 0)
            {
                insights.Add(
                    "No command history is available yet. Submit and execute " + "device commands to generate command-history insights.");
            }
            else if (!insights.Any())
            {
                insights.Add($"Reviewed {recentCommands.Count} recent command(s). " + "No repeated telemetry-disable pattern was detected.");
            }

            if (offlineDevices.Count > 0)
            {
                insights.Add($"{offlineDevices.Count} device(s) are currently offline. " + "Review their connectivity and investigate related alerts.");
            }

            var unresolvedCriticalCount = criticalAlerts.Count;

            if (unresolvedCriticalCount > 0)
            {
                insights.Add($"{unresolvedCriticalCount} unresolved critical alert(s) " + "require attention. Prioritise investigation of affected devices.");
            }

            if (criticalAlerts.Count == 0 && offlineDevices.Count == 0)
            {
                insights.Add("No unresolved critical alerts or offline devices were " + "detected in the current data.");
            }

            // Identify devices that are repeatedly investigated.
            var repeatedSearches = _dataStore.GetSearchHistorySnapshot()
                .GroupBy(s => s.DeviceIdentifier,
                    StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() >= 3);

            foreach (var group in repeatedSearches)
            {
                var categories = group
                    .Select(s => s.TelemetryCategory)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                insights.Add(
                    $"{group.Key} has been searched {group.Count()} times. " +
                    $"Telemetry categories investigated: {string.Join(", ", categories)}.");

                suggestions.Add(new
                {
                    DeviceIdentifier = group.Key,
                    SuggestedAction = "Review recurring telemetry investigations",
                    Reason = $"This device has been searched {group.Count()} times.",
                    Priority = "Medium"
                });
            }


            // Analyse recent historical soil-moisture readings.
            var recentSoilReadings = _dataStore.GetFloatTelemetrySnapshot()
                .GroupBy(p => p.DeviceIdentifier,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var group in recentSoilReadings)
            {
                var readings = group
                    .OrderByDescending(p => p.Timestamp)
                    .Take(5)
                    .ToList();

                if (readings.Count < 3)
                    continue;

                var criticalReadings = readings
                    .Count(p => p.Value < 20.0f);

                if (criticalReadings >= 3)
                {
                    suggestions.Add(new
                    {
                        DeviceIdentifier = group.Key,
                        SuggestedAction = "Investigate persistent low soil moisture",
                        Reason = $"{criticalReadings} of the latest {readings.Count} readings " +
                                 "are below the critical threshold of 20%.",
                        Priority = "Critical"
                    });

                    insights.Add(
                        $"{group.Key} shows persistent low soil moisture across " +
                        $"{criticalReadings} of its latest {readings.Count} readings.");
                }

                var chronological = readings
                    .OrderBy(p => p.Timestamp)
                    .ToList();

                bool increasing = true;

                for (int i = 1; i < chronological.Count; i++)
                {
                    if (chronological[i].Value <= chronological[i - 1].Value)
                    {
                        increasing = false;
                        break;
                    }
                }

                if (increasing)
                {
                    insights.Add(
                        $"{group.Key} soil moisture has increased across its " +
                        $"{chronological.Count} latest readings. Continue monitoring " +
                        "to confirm recovery.");
                }
            }

            // Analyse recent historical power readings.
            var recentPowerReadings = _dataStore.GetIntegerTelemetrySnapshot()
                .GroupBy(p => p.DeviceIdentifier,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var group in recentPowerReadings)
            {
                var readings = group
                    .OrderByDescending(p => p.Timestamp)
                    .Take(5)
                    .ToList();

                if (readings.Count < 3)
                    continue;

                var criticalReadings = readings
                    .Count(p => p.Value > 5000);

                if (criticalReadings >= 3)
                {
                    suggestions.Add(new
                    {
                        DeviceIdentifier = group.Key,
                        SuggestedAction = "Investigate sustained high power consumption",
                        Reason = $"{criticalReadings} of the latest {readings.Count} readings " +
                                 "exceed the critical threshold of 5000 W.",
                        Priority = "Critical"
                    });

                    insights.Add(
                        $"{group.Key} shows sustained high power consumption across " +
                        $"{criticalReadings} of its latest {readings.Count} readings.");
                }

                var chronologicalPowerReadings = readings
                    .OrderBy(p => p.Timestamp)
                    .ToList();

                if (chronologicalPowerReadings.Count >= 3)
                {
                    var firstReading = chronologicalPowerReadings.First().Value;
                    var latestReading = chronologicalPowerReadings.Last().Value;

                    // Avoid division by zero or invalid percentage calculations.
                    if (firstReading > 0)
                    {
                        var percentageIncrease =
                            ((double)(latestReading - firstReading) / firstReading) * 100;

                        // Require a meaningful net increase of at least 15%.
                        if (percentageIncrease >= 15)
                        {
                            insights.Add(
                                $"{group.Key} power consumption increased by " +
                                $"{percentageIncrease:F1}% across its latest " +
                                $"{chronologicalPowerReadings.Count} readings. " +
                                "Investigate the upward trend before consumption " +
                                "reaches the critical threshold.");

                            suggestions.Add(new
                            {
                                DeviceIdentifier = group.Key,
                                SuggestedAction = "Investigate rising power consumption",
                                Reason = $"Power consumption increased from {firstReading} W " +
                                         $"to {latestReading} W " +
                                         $"({percentageIncrease:F1}% increase).",
                                Priority = "High"
                            });
                        }
                    }
                }
            }

            var transitions = _patternAnalyser.BuildTransitions(
                alerts,
                _dataStore.GetSearchHistorySnapshot(),
                _commandService.GetHistory());

            var devicesWithActiveAlerts = alerts
                .Where(a => !a.IsResolved)
                .Select(a => a.DeviceIdentifier);

            foreach (var p in _patternAnalyser.FindLikelyFollowUps(transitions, devicesWithActiveAlerts))
            {
                var action = ActionPatternAnalyser.DescribeAction(p.FollowUpType, p.FollowUpDevice);

                suggestions.Add(new
                {
                    DeviceIdentifier = p.FollowUpDevice,
                    SuggestedAction = action,
                    Reason = $"After alerts on {p.AlertDevice}, operators did this {p.Count} times within 10 minutes.",
                    Priority = "High"
                });

                insights.Add(
                    $"Pattern: alerts on {p.AlertDevice} are usually followed by " +
                    $"'{p.FollowUpType}' on {p.FollowUpDevice} ({p.Count} times).");
            }

            // Learn from the operator's own habits. This needs no alert: it looks only at
            // the order of searches, route traces and commands.
            var searchHistory = _dataStore.GetSearchHistorySnapshot();
            var commandHistory = _commandService.GetHistory().ToList();

            var sequenceTransitions = _patternAnalyser.BuildSequenceTransitions(searchHistory, commandHistory);
            var recentActions = _patternAnalyser.GetRecentActions(searchHistory, commandHistory);

            foreach (var p in _patternAnalyser.PredictNext(sequenceTransitions, recentActions))
            {
                var context = ActionPatternAnalyser.DescribeContext(p.Context);

                suggestions.Add(new
                {
                    DeviceIdentifier = p.NextDevice,
                    SuggestedAction = ActionPatternAnalyser.DescribeAction(p.NextType, p.NextDevice),
                    Reason = $"After \"{context}\", you did this {p.Count} of {p.Total} times ({p.Probability:P0}).",
                    Priority = "Medium"
                });

                insights.Add(
                    $"Habit: \"{context}\" is usually followed by " +
                    $"'{p.NextType}' on {p.NextDevice} ({p.Count} of {p.Total} times).");
            }

            return new
            {
                GeneratedAt = DateTime.UtcNow,
                SuggestedActions = suggestions,
                AutomatedInsights = insights
            };
        }
    }
}
