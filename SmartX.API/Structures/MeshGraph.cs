using SmartX.API.Models;

namespace SmartX.API.Structures
{
    public sealed class MeshNode
    {
        public string Id { get; set; } = string.Empty;
        public string Mac { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public SensorStatus Status { get; set; } = SensorStatus.Online;
        public bool IsGateway { get; set; }
        public Guid? SensorId { get; set; }
    }

    // wireless link. Cost is the transmit power (mW) needed to send one packet over it
    public sealed record MeshEdge(string From, string To, double Cost);

    public sealed record BfsEntry(int Hops, string? Parent);

    // A spanning tree rooted at the gateway. Following Parent from any node leads to the gateway
    public sealed class SpanningTree
    {
        public SpanningTree(string root) => Root = root;

        public string Root { get; }
        public Dictionary<string, string> Parent { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<MeshEdge> Edges { get; } = new();
        public double TotalCost { get; set; }

        public bool Contains(string node)
            => string.Equals(node, Root, StringComparison.OrdinalIgnoreCase) || Parent.ContainsKey(node);

        // The route from a node up to the root (node first, root last), or null if the node is not in the tree
        public List<string>? PathToRoot(string node)
        {
            if (!Contains(node))
                return null;

            var path = new List<string> { node };

            while (Parent.TryGetValue(path[^1], out var parent))
                path.Add(parent);

            return path;
        }
    }

    public class MeshGraph
    {
        private readonly Dictionary<string, MeshNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<MeshEdge>> _adjacency = new(StringComparer.OrdinalIgnoreCase);

        public int NodeCount => _nodes.Count;

        public IEnumerable<MeshNode> Nodes => _nodes.Values;

        public MeshNode? GetNode(string id) => _nodes.GetValueOrDefault(id);

        public bool IsUp(string id)
            => _nodes.TryGetValue(id, out var node) && node.Status != SensorStatus.Offline;

        public bool AddNode(MeshNode node)
        {
            if (string.IsNullOrWhiteSpace(node.Id) || !_nodes.TryAdd(node.Id, node))
                return false;

            _adjacency[node.Id] = new List<MeshEdge>();
            return true;
        }

        // Adds a two-way link. Returns false for self-links, unknown nodes or duplicates
        public bool AddEdge(string a, string b, double cost)
        {
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase) ||
                !_adjacency.TryGetValue(a, out var fromA) ||
                !_adjacency.TryGetValue(b, out var fromB) ||
                fromA.Any(e => string.Equals(e.To, b, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            fromA.Add(new MeshEdge(a, b, cost));
            fromB.Add(new MeshEdge(b, a, cost));
            return true;
        }

        public IReadOnlyList<MeshEdge> Neighbours(string id)
            => _adjacency.TryGetValue(id, out var list) ? list : Array.Empty<MeshEdge>();

        public MeshEdge? GetEdge(string a, string b)
            => Neighbours(a).FirstOrDefault(e => string.Equals(e.To, b, StringComparison.OrdinalIgnoreCase));

        // Every link exactly once
        public IEnumerable<MeshEdge> Edges
            => _adjacency.Values
                .SelectMany(list => list)
                .Where(e => string.Compare(e.From, e.To, StringComparison.OrdinalIgnoreCase) < 0);

        // ------------------------------------------------------------------
        // Breadth-first search: O(V + E)
        // ------------------------------------------------------------------

        /// <summary>
        /// Visits every node reachable from start, level by level, so Hops is the FEWEST hops to start.
        /// With healthyOnly, offline nodes are neither visited nor passed through.
        /// </summary>
        public Dictionary<string, BfsEntry> BreadthFirstSearch(string start, bool healthyOnly)
        {
            var visited = new Dictionary<string, BfsEntry>(StringComparer.OrdinalIgnoreCase);

            if (!_nodes.ContainsKey(start) || (healthyOnly && !IsUp(start)))
                return visited;

            var queue = new Queue<string>();
            visited[start] = new BfsEntry(0, null);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var edge in Neighbours(current))
                {
                    if (visited.ContainsKey(edge.To) || (healthyOnly && !IsUp(edge.To)))
                        continue;

                    visited[edge.To] = new BfsEntry(visited[current].Hops + 1, current);
                    queue.Enqueue(edge.To);
                }
            }

            return visited;
        }

        // The fewest-hop route from a node back to the BFS start (node first), or null if unreachable
        public static List<string>? PathFromBfs(Dictionary<string, BfsEntry> bfs, string node)
        {
            if (!bfs.ContainsKey(node))
                return null;

            var path = new List<string> { node };

            while (bfs[path[^1]].Parent is { } parent)
                path.Add(parent);

            return path;
        }

        // ------------------------------------------------------------------
        // Prim's minimum spanning tree: O(E log V) with a priority queue
        // ------------------------------------------------------------------

        /// <summary>
        /// Grows a tree from root, always adding the cheapest link that reaches a new node.
        /// The result connects every reachable node with the lowest total transmit power.
        /// With healthyOnly, offline nodes are left out, so the tree "heals" around failures.
        /// </summary>
        public SpanningTree Prim(string root, bool healthyOnly)
        {
            var tree = new SpanningTree(root);

            if (!_nodes.ContainsKey(root) || (healthyOnly && !IsUp(root)))
                return tree;

            var inTree = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root };
            var candidates = new PriorityQueue<MeshEdge, double>();

            void AddCandidates(string node)
            {
                foreach (var edge in Neighbours(node))
                {
                    if (!inTree.Contains(edge.To) && (!healthyOnly || IsUp(edge.To)))
                        candidates.Enqueue(edge, edge.Cost);
                }
            }

            AddCandidates(root);

            while (candidates.TryDequeue(out var edge, out _))
            {
                if (!inTree.Add(edge.To))
                    continue;                       // already reached by a cheaper link

                tree.Parent[edge.To] = edge.From;
                tree.Edges.Add(edge);
                tree.TotalCost += edge.Cost;

                AddCandidates(edge.To);
            }

            return tree;
        }
    }
}
