using SmartX.API.Models;

namespace SmartX.API.Services
{
    public class ActionPatternAnalyser
    {
        private static readonly TimeSpan FollowUpWindow = TimeSpan.FromMinutes(10);

        public record ActionPattern(string AlertDevice, string FollowUpType, string FollowUpDevice, int Count);

        // alert device
        public Dictionary<string, Dictionary<string, int>> BuildTransitions(
            IEnumerable<Alert> alerts,
            IEnumerable<TelemetrySearchRecord> searches,
            IEnumerable<CommandRecord> commands)
        {
            var actions = searches
                .Select(s => (At: s.SearchedAt, Key: SearchKey(s)))
                .Concat(commands.Select(c => (At: c.CreatedAt, Key: $"{c.CommandType}|{c.DeviceIdentifier}")))
                .OrderBy(a => a.At)
                .ToList();

            var transitions = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            int start = 0;   // alerts are processed in time order, so this index only moves forward

            foreach (var alert in alerts.OrderBy(a => a.Timestamp))
            {
                while (start < actions.Count && actions[start].At < alert.Timestamp)
                    start++;

                var seenForThisAlert = new HashSet<string>();

                for (int i = start;
                     i < actions.Count && actions[i].At - alert.Timestamp <= FollowUpWindow;
                     i++)
                {
                    if (!seenForThisAlert.Add(actions[i].Key))
                        continue;

                    if (!transitions.TryGetValue(alert.DeviceIdentifier, out var follow))
                        transitions[alert.DeviceIdentifier] = follow =
                            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                    follow[actions[i].Key] = follow.GetValueOrDefault(actions[i].Key) + 1;
                }
            }

            return transitions;
        }

        public List<ActionPattern> FindLikelyFollowUps(
            Dictionary<string, Dictionary<string, int>> transitions,
            IEnumerable<string> devicesWithActiveAlerts,
            int minCount = 2)
        {
            var result = new List<ActionPattern>();

            foreach (var device in devicesWithActiveAlerts.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!transitions.TryGetValue(device, out var follow))
                    continue;

                foreach (var (key, count) in follow
                    .Where(f => f.Value >= minCount)
                    .OrderByDescending(f => f.Value)
                    .Take(2))
                {
                    var parts = key.Split('|', 2);
                    result.Add(new ActionPattern(device, parts[0], parts[1], count));
                }
            }

            return result;
        }

        // ======================================================================
        // ACTION-SEQUENCE LEARNING (works without any alert)
        // ======================================================================
        //
        // Searches, route traces and commands are merged into one time-ordered action
        // log, split into "sessions" wherever the operator paused for more than
        // SessionGap. Inside each session we count which action followed which:
        //   one action of context   "Searched|ESP32-001"            -> "TogglePump|ESP32-003"
        //   two actions of context  "Searched|ESP32-001 > Traced|X" -> "TogglePump|ESP32-003"
        // Longer context is tried first, so a sequence beats a single habit.

        private static readonly TimeSpan SessionGap = TimeSpan.FromMinutes(15);

        public record SequencePrediction(
            string Context, string NextType, string NextDevice, int Count, int Total)
        {
            public double Probability => Total == 0 ? 0 : (double)Count / Total;
        }

        private static string SearchKey(TelemetrySearchRecord s)
            => string.Equals(s.TelemetryCategory, "Route", StringComparison.OrdinalIgnoreCase)
                ? $"Traced|{s.DeviceIdentifier}"
                : $"Searched|{s.DeviceIdentifier}";

        private static List<(DateTime At, string Key)> BuildActionLog(
            IEnumerable<TelemetrySearchRecord> searches,
            IEnumerable<CommandRecord> commands)
        {
            return searches
                .Select(s => (At: s.SearchedAt, Key: SearchKey(s)))
                .Concat(commands.Select(c => (At: c.CreatedAt, Key: $"{c.CommandType}|{c.DeviceIdentifier}")))
                .OrderBy(a => a.At)
                .ToList();
        }

        /// <summary>Counts, for every one- or two-action context, what the operator did next.</summary>
        public Dictionary<string, Dictionary<string, int>> BuildSequenceTransitions(
            IEnumerable<TelemetrySearchRecord> searches,
            IEnumerable<CommandRecord> commands)
        {
            var transitions = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

            foreach (var session in SplitIntoSessions(BuildActionLog(searches, commands)))
            {
                for (int i = 1; i < session.Count; i++)
                {
                    Learn(transitions, session[i - 1], session[i]);

                    if (i >= 2)
                        Learn(transitions, $"{session[i - 2]} > {session[i - 1]}", session[i]);
                }
            }

            return transitions;
        }

        /// <summary>The last (up to) two actions of the most recent session, oldest first.</summary>
        public List<string> GetRecentActions(
            IEnumerable<TelemetrySearchRecord> searches,
            IEnumerable<CommandRecord> commands,
            int take = 2)
        {
            var sessions = SplitIntoSessions(BuildActionLog(searches, commands));

            if (sessions.Count == 0)
                return new List<string>();

            var latest = sessions[^1];
            return latest.Skip(Math.Max(0, latest.Count - take)).ToList();
        }

        /// <summary>
        /// Predicts the operator's next action from their recent actions. Only patterns seen at
        /// least minCount times AND making up at least minProbability of the follow-ups are returned.
        /// </summary>
        public List<SequencePrediction> PredictNext(
            Dictionary<string, Dictionary<string, int>> transitions,
            IReadOnlyList<string> recentActions,
            int minCount = 2,
            double minProbability = 0.5)
        {
            for (int length = Math.Min(2, recentActions.Count); length >= 1; length--)
            {
                var context = string.Join(" > ", recentActions.Skip(recentActions.Count - length));

                if (!transitions.TryGetValue(context, out var follow))
                    continue;

                int total = follow.Values.Sum();

                var matches = follow
                    .Where(f => f.Value >= minCount && (double)f.Value / total >= minProbability)
                    .OrderByDescending(f => f.Value)
                    .Take(2)
                    .Select(f =>
                    {
                        var parts = f.Key.Split('|', 2);
                        return new SequencePrediction(context, parts[0], parts[1], f.Value, total);
                    })
                    .ToList();

                if (matches.Count > 0)
                    return matches;       // the longest context that has a confident answer wins
            }

            return new List<SequencePrediction>();
        }

        /// <summary>Turns an action type and device into the text shown to the operator.</summary>
        public static string DescribeAction(string actionType, string device) => actionType switch
        {
            "Searched" => $"Review telemetry for {device}",
            "Traced" => $"Trace the mesh route for {device}",
            _ => $"Consider {actionType} on {device}"
        };

        public static string DescribeContext(string context)
            => string.Join(" then ", context.Split(" > ").Select(PrettyKey));

        private static string PrettyKey(string key)
        {
            var parts = key.Split('|', 2);
            return parts.Length == 2 ? $"{parts[0]} {parts[1]}" : key;
        }

        private static List<List<string>> SplitIntoSessions(List<(DateTime At, string Key)> log)
        {
            var sessions = new List<List<string>>();
            List<string>? current = null;
            DateTime? previous = null;

            foreach (var (at, key) in log)
            {
                if (current == null || (previous.HasValue && at - previous.Value > SessionGap))
                {
                    current = new List<string>();
                    sessions.Add(current);
                }

                // Repeating the same action back-to-back adds noise, not a sequence.
                if (current.Count == 0 || !string.Equals(current[^1], key, StringComparison.OrdinalIgnoreCase))
                    current.Add(key);

                previous = at;
            }

            return sessions;
        }

        private static void Learn(
            Dictionary<string, Dictionary<string, int>> transitions, string context, string next)
        {
            if (!transitions.TryGetValue(context, out var follow))
                transitions[context] = follow = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            follow[next] = follow.GetValueOrDefault(next) + 1;
        }
    }
}