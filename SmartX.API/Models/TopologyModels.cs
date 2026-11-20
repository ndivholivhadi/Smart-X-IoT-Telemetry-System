namespace SmartX.API.Models
{
    public class TopologyNodeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Mac { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsGateway { get; set; }
        public Guid? SensorId { get; set; }
        public bool Reachable { get; set; }
        public int? HopsToGateway { get; set; }
    }

    public class TopologyEdgeDto
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public double Cost { get; set; }
        public bool InMst { get; set; }
        public bool Active { get; set; }
    }

    public class RerouteDto
    {
        public string Node { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;          // Down, Rerouted or Isolated
        public List<string> PlannedPath { get; set; } = new();
        public List<string>? NewPath { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class TopologySnapshot
    {
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public string GatewayId { get; set; } = string.Empty;
        public List<TopologyNodeDto> Nodes { get; set; } = new();
        public List<TopologyEdgeDto> Edges { get; set; } = new();
        public double MstTotalCost { get; set; }
        public int ReachableCount { get; set; }
        public List<string> DownNodes { get; set; } = new();
        public List<string> StrandedNodes { get; set; } = new();
        public List<RerouteDto> Reroutes { get; set; } = new();
    }

    public class RouteTrace
    {
        public string Query { get; set; } = string.Empty;
        public bool Found { get; set; }
        public string? NodeId { get; set; }
        public string? Mac { get; set; }
        public string? Status { get; set; }
        public List<string> FewestHopsPath { get; set; } = new();    // BFS
        public int Hops { get; set; }
        public List<string> PowerEfficientPath { get; set; } = new(); // Prim MST
        public double PowerCost { get; set; }
        public bool WasRerouted { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class ConnectedNodeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Mac { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public int Hops { get; set; }
        public string? Via { get; set; }
    }

    public class ConnectivityReport
    {
        public string Root { get; set; } = string.Empty;
        public bool RootIsUp { get; set; }
        public List<ConnectedNodeDto> Connected { get; set; } = new();
        public List<string> Down { get; set; } = new();
        public List<string> Unreachable { get; set; } = new();
    }

    public class HistoryReading
    {
        public DateTime Timestamp { get; set; }
        public string Value { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
    }

    public class HistoryQueryResult
    {
        public string DeviceIdentifier { get; set; } = string.Empty;
        public string? Mac { get; set; }
        public string Metric { get; set; } = string.Empty;
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string Algorithm { get; set; } = "AVL tree range query";
        public int ResultsFound { get; set; }
        public int TotalStored { get; set; }
        public int TreeHeight { get; set; }
        public long SearchTimeMicroseconds { get; set; }
        public List<HistoryReading> Readings { get; set; } = new();
    }
}
