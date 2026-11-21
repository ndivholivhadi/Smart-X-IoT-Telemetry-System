using System.Diagnostics;
using SmartX.API.Data;
using SmartX.API.Models;
using SmartX.API.Structures;

namespace SmartX.API.Services
{
    /// <summary>
    /// Builds the mesh graph from the registered sensors and answers topology questions:
    /// who is connected (BFS), what is the most power-efficient route (Prim's MST),
    /// and how do routes change when a node goes down.
    /// </summary>
    public class MeshTopologyService
    {
        public const string GatewayId = "GATEWAY-01";
        public const string GatewayMac = "24:6F:28:00:00:01";

        private readonly SmartXDataStore _dataStore;

        public MeshTopologyService(SmartXDataStore dataStore)
        {
            _dataStore = dataStore;
        }

        // ------------------------------------------------------------------
        // Graph construction
        // ------------------------------------------------------------------
        public MeshGraph BuildGraph()
        {
            var graph = new MeshGraph();

            graph.AddNode(new MeshNode
            {
                Id = GatewayId,
                Mac = GatewayMac,
                Zone = "Gateway",
                Status = SensorStatus.Online,
                IsGateway = true
            });

            var sensors = _dataStore.Sensors
                .ToList()
                .Where(s => !string.IsNullOrWhiteSpace(s.DeviceIdentifier))
                .OrderBy(s => s.DeviceIdentifier, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var sensor in sensors)
            {
                graph.AddNode(new MeshNode
                {
                    Id = sensor.DeviceIdentifier,
                    Mac = string.IsNullOrWhiteSpace(sensor.MacAddress)
                        ? SmartXDataStore.GenerateMacAddress(sensor.DeviceIdentifier)
                        : sensor.MacAddress,
                    Zone = string.IsNullOrWhiteSpace(sensor.Zone) ? "Unassigned" : sensor.Zone.Trim(),
                    Status = sensor.Status,
                    SensorId = sensor.Id
                });
            }

            // Sensors in the same zone sit close together, so they form a chain (a ring from 3 nodes up).
            var zones = sensors
                .Where(s => graph.GetNode(s.DeviceIdentifier) != null)
                .GroupBy(s => graph.GetNode(s.DeviceIdentifier)!.Zone, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Select(s => s.DeviceIdentifier).ToList())
                .ToList();

            foreach (var zone in zones)
            {
                for (int i = 0; i < zone.Count - 1; i++)
                    Link(graph, zone[i], zone[i + 1]);

                if (zone.Count >= 3)
                    Link(graph, zone[^1], zone[0]);

                // The first node in each zone is within radio range of the gateway.
                Link(graph, GatewayId, zone[0]);
            }

            // Neighbouring zones overlap at their edges, which gives the mesh its alternative paths.
            if (zones.Count > 1)
            {
                for (int k = 0; k < zones.Count; k++)
                    Link(graph, zones[k][^1], zones[(k + 1) % zones.Count][0]);
            }

            return graph;
        }

        private static void Link(MeshGraph graph, string a, string b)
            => graph.AddEdge(a, b, LinkCost(a, b));

        // Same two devices always give the same cost (string.GetHashCode is random per run, so use FNV-1a).
        // Range 5-50 mW, standing in for the signal strength a real node would report.
        public static double LinkCost(string a, string b)
        {
            var pair = string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0
                ? $"{a}|{b}"
                : $"{b}|{a}";

            uint hash = 2166136261;

            foreach (char c in pair.ToUpperInvariant())
            {
                hash ^= c;
                hash *= 16777619;
            }

            return 5 + hash % 46;
        }

        // ------------------------------------------------------------------
        // Snapshot for the dashboard
        // ------------------------------------------------------------------
        public TopologySnapshot GetSnapshot()
        {
            var graph = BuildGraph();

            var planned = graph.Prim(GatewayId, healthyOnly: false);   // the design: every node working
            var live = graph.Prim(GatewayId, healthyOnly: true);       // reality: down nodes left out
            var reachable = graph.BreadthFirstSearch(GatewayId, healthyOnly: true);

            var mstLinks = live.Edges
                .Select(e => LinkKey(e.From, e.To))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var snapshot = new TopologySnapshot
            {
                GatewayId = GatewayId,
                MstTotalCost = live.TotalCost,
                ReachableCount = reachable.Count - 1     // the gateway itself does not count
            };

            foreach (var node in graph.Nodes.OrderBy(n => n.Zone, StringComparer.OrdinalIgnoreCase)
                                            .ThenBy(n => n.Id, StringComparer.OrdinalIgnoreCase))
            {
                snapshot.Nodes.Add(new TopologyNodeDto
                {
                    Id = node.Id,
                    Mac = node.Mac,
                    Zone = node.Zone,
                    Status = node.Status.ToString(),
                    IsGateway = node.IsGateway,
                    SensorId = node.SensorId,
                    Reachable = reachable.ContainsKey(node.Id),
                    HopsToGateway = reachable.TryGetValue(node.Id, out var entry) ? entry.Hops : null
                });
            }

            foreach (var edge in graph.Edges)
            {
                snapshot.Edges.Add(new TopologyEdgeDto
                {
                    From = edge.From,
                    To = edge.To,
                    Cost = edge.Cost,
                    InMst = mstLinks.Contains(LinkKey(edge.From, edge.To)),
                    Active = graph.IsUp(edge.From) && graph.IsUp(edge.To)
                });
            }

            foreach (var node in graph.Nodes.Where(n => !n.IsGateway))
            {
                if (!graph.IsUp(node.Id))
                    snapshot.DownNodes.Add(node.Id);
                else if (!reachable.ContainsKey(node.Id))
                    snapshot.StrandedNodes.Add(node.Id);
            }

            snapshot.DownNodes.Sort(StringComparer.OrdinalIgnoreCase);
            snapshot.StrandedNodes.Sort(StringComparer.OrdinalIgnoreCase);

            snapshot.Reroutes = BuildReroutes(graph, planned, live);

            return snapshot;
        }

        private static List<RerouteDto> BuildReroutes(MeshGraph graph, SpanningTree planned, SpanningTree live)
        {
            var reroutes = new List<RerouteDto>();

            foreach (var node in graph.Nodes.Where(n => !n.IsGateway)
                                            .OrderBy(n => n.Id, StringComparer.OrdinalIgnoreCase))
            {
                var plannedPath = planned.PathToRoot(node.Id) ?? new List<string>();

                if (!graph.IsUp(node.Id))
                {
                    var hopTargets = graph.Neighbours(node.Id)
                        .Where(e => graph.IsUp(e.To))
                        .Select(e => e.To)
                        .ToList();

                    reroutes.Add(new RerouteDto
                    {
                        Node = node.Id,
                        State = "Down",
                        PlannedPath = plannedPath,
                        Message = hopTargets.Count > 0
                            ? $"{node.Id} is down. Traffic hops around it using: {string.Join(", ", hopTargets)}."
                            : $"{node.Id} is down and has no working neighbours."
                    });

                    continue;
                }

                var brokenBy = plannedPath
                    .Skip(1)
                    .Where(id => !graph.IsUp(id))
                    .ToList();

                if (brokenBy.Count == 0)
                    continue;                          // its normal route still works

                var newPath = live.PathToRoot(node.Id);

                reroutes.Add(new RerouteDto
                {
                    Node = node.Id,
                    State = newPath != null ? "Rerouted" : "Isolated",
                    PlannedPath = plannedPath,
                    NewPath = newPath,
                    Message = newPath != null
                        ? $"{node.Id} normally goes through {string.Join(", ", brokenBy)} (down). New route: {string.Join(" → ", newPath)}."
                        : $"{node.Id} normally goes through {string.Join(", ", brokenBy)} (down) and no working neighbour leads to the gateway."
                });
            }

            return reroutes;
        }

        // ------------------------------------------------------------------
        // Track one sensor by identifier or MAC address
        // ------------------------------------------------------------------
        public RouteTrace TraceRoute(string identifierOrMac)
        {
            var stopwatch = Stopwatch.StartNew();
            var trace = new RouteTrace { Query = identifierOrMac?.Trim() ?? string.Empty };

            var graph = BuildGraph();
            var node = ResolveNode(graph, trace.Query);

            if (node == null)
            {
                trace.Message = $"No device with identifier or MAC address '{trace.Query}' is registered.";
                return trace;
            }

            trace.Found = true;
            trace.NodeId = node.Id;
            trace.Mac = node.Mac;
            trace.Status = node.Status.ToString();

            var planned = graph.Prim(GatewayId, healthyOnly: false);
            var live = graph.Prim(GatewayId, healthyOnly: true);
            var bfs = graph.BreadthFirstSearch(GatewayId, healthyOnly: true);

            if (node.IsGateway)
            {
                trace.FewestHopsPath = new List<string> { GatewayId };
                trace.PowerEfficientPath = new List<string> { GatewayId };
                trace.Message = "This is the gateway: the end of every route.";
                return trace;
            }

            if (!graph.IsUp(node.Id))
            {
                var alternatives = graph.Neighbours(node.Id).Where(e => graph.IsUp(e.To)).Select(e => e.To).ToList();
                trace.Message = alternatives.Count > 0
                    ? $"{node.Id} is down. Its neighbours {string.Join(", ", alternatives)} carry traffic around it."
                    : $"{node.Id} is down and has no working neighbours.";
                return Record(trace, stopwatch);
            }

            var hopPath = MeshGraph.PathFromBfs(bfs, node.Id);
            var powerPath = live.PathToRoot(node.Id);

            if (hopPath == null || powerPath == null)
            {
                trace.Message = $"{node.Id} is up but no working route leads to the gateway.";
                return Record(trace, stopwatch);
            }

            trace.FewestHopsPath = hopPath;
            trace.Hops = hopPath.Count - 1;
            trace.PowerEfficientPath = powerPath;
            trace.PowerCost = PathCost(graph, powerPath);

            var plannedPath = planned.PathToRoot(node.Id) ?? new List<string>();
            trace.WasRerouted = plannedPath.Skip(1).Any(id => !graph.IsUp(id));

            trace.Message = trace.WasRerouted
                ? "Normal route is blocked by a down node, so this is the re-routed path."
                : "Route is healthy.";

            return Record(trace, stopwatch);
        }

        // Tracking a device is an operator action, so log it for the predictive engine.
        private RouteTrace Record(RouteTrace trace, Stopwatch stopwatch)
        {
            stopwatch.Stop();

            _dataStore.RecordTelemetrySearch(new TelemetrySearchRecord
            {
                DeviceIdentifier = trace.NodeId ?? trace.Query,
                TelemetryCategory = "Route",
                SearchedAt = DateTime.UtcNow,
                ResultsFound = trace.PowerEfficientPath.Count,
                SearchTimeMicroseconds = stopwatch.ElapsedTicks * 1_000_000 / Stopwatch.Frequency
            });

            return trace;
        }

        // ------------------------------------------------------------------
        // Who is connected to a gateway / subnet root?
        // ------------------------------------------------------------------
        public ConnectivityReport GetConnectivity(string? from = null)
        {
            var graph = BuildGraph();
            var root = string.IsNullOrWhiteSpace(from) ? graph.GetNode(GatewayId) : ResolveNode(graph, from.Trim());

            var report = new ConnectivityReport { Root = root?.Id ?? (from ?? string.Empty) };

            if (root == null)
                return report;

            report.RootIsUp = graph.IsUp(root.Id);

            var bfs = graph.BreadthFirstSearch(root.Id, healthyOnly: true);

            report.Connected = bfs
                .Where(kv => !string.Equals(kv.Key, root.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(kv => kv.Value.Hops)
                .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv =>
                {
                    var n = graph.GetNode(kv.Key)!;
                    return new ConnectedNodeDto { Id = n.Id, Mac = n.Mac, Zone = n.Zone, Hops = kv.Value.Hops, Via = kv.Value.Parent };
                })
                .ToList();

            foreach (var n in graph.Nodes.Where(n => !string.Equals(n.Id, root.Id, StringComparison.OrdinalIgnoreCase)))
            {
                if (!graph.IsUp(n.Id))
                    report.Down.Add(n.Id);
                else if (!bfs.ContainsKey(n.Id))
                    report.Unreachable.Add(n.Id);
            }

            report.Down.Sort(StringComparer.OrdinalIgnoreCase);
            report.Unreachable.Sort(StringComparer.OrdinalIgnoreCase);

            return report;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private static MeshNode? ResolveNode(MeshGraph graph, string identifierOrMac)
        {
            if (string.IsNullOrWhiteSpace(identifierOrMac))
                return null;

            var key = identifierOrMac.Trim();
            var byId = graph.GetNode(key);

            if (byId != null)
                return byId;

            var mac = SmartXDataStore.NormalizeMac(key);
            return graph.Nodes.FirstOrDefault(n => string.Equals(n.Mac, mac, StringComparison.OrdinalIgnoreCase));
        }

        private static double PathCost(MeshGraph graph, List<string> path)
        {
            double total = 0;

            for (int i = 0; i < path.Count - 1; i++)
                total += graph.GetEdge(path[i], path[i + 1])?.Cost ?? 0;

            return total;
        }

        private static string LinkKey(string a, string b)
            => string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
    }
}
