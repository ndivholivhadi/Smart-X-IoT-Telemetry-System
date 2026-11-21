namespace SmartX.API.Structures
{
    public class AvlTree<TKey, TValue> where TKey : IComparable<TKey>
    {
        private sealed class Node
        {
            public TKey Key;
            public TValue Value;
            public int Height = 1;
            public Node? Left;
            public Node? Right;

            public Node(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }
        }

        private Node? _root;

        public int Count { get; private set; }

        public int Height => _root?.Height ?? 0;


        // ---------- Insert ----------
        public void Insert(TKey key, TValue value)
        {
            _root = Insert(_root, key, value);
        }

        private Node Insert(Node? node, TKey key, TValue value)
        {
            if (node == null)
            {
                Count++;
                return new Node(key, value);
            }

            int cmp = key.CompareTo(node.Key);

            if (cmp < 0)
                node.Left = Insert(node.Left, key, value);
            else if (cmp > 0)
                node.Right = Insert(node.Right, key, value);
            else
                node.Value = value;  // same key: replace

            return Rebalance(node);
        }


        // ---------- Remove ----------
        public bool Remove(TKey key)
        {
            int before = Count;
            _root = Remove(_root, key);
            return Count < before;
        }

        private Node? Remove(Node? node, TKey key)
        {
            if (node == null)
                return null;

            int cmp = key.CompareTo(node.Key);

            if (cmp < 0)
            {
                node.Left = Remove(node.Left, key);
            }
            else if (cmp > 0)
            {
                node.Right = Remove(node.Right, key);
            }
            else
            {
                if (node.Left == null || node.Right == null)
                {
                    Count--;
                    return node.Left ?? node.Right;
                }

                // Two children: copy the in-order successor up, then delete it.
                var successor = node.Right;
                while (successor.Left != null)
                    successor = successor.Left;

                node.Key = successor.Key;
                node.Value = successor.Value;
                node.Right = Remove(node.Right, successor.Key);
            }

            return Rebalance(node);
        }

        // Removes and returns the oldest (smallest-key) entry
        public bool TryRemoveMin(out TValue value)
        {
            if (_root == null)
            {
                value = default!;
                return false;
            }

            var node = _root;
            while (node.Left != null)
                node = node.Left;

            value = node.Value;
            Remove(node.Key);
            return true;
        }


        // ---------- Queries ----------
        public bool TryGet(TKey key, out TValue value)
        {
            var node = _root;

            while (node != null)
            {
                int cmp = key.CompareTo(node.Key);

                if (cmp == 0)
                {
                    value = node.Value;
                    return true;
                }

                node = cmp < 0 ? node.Left : node.Right;
            }

            value = default!;
            return false;
        }

        // All values whose key is between from and to (inclusive), in key order.
        // Subtrees that cannot contain a match are skipped
        public List<TValue> Range(TKey from, TKey to)
        {
            var results = new List<TValue>();
            CollectRange(_root, from, to, results);
            return results;
        }

        private static void CollectRange(Node? node, TKey from, TKey to, List<TValue> results)
        {
            if (node == null)
                return;

            if (from.CompareTo(node.Key) < 0)
                CollectRange(node.Left, from, to, results);

            if (from.CompareTo(node.Key) <= 0 && node.Key.CompareTo(to) <= 0)
                results.Add(node.Value);

            if (node.Key.CompareTo(to) < 0)
                CollectRange(node.Right, from, to, results);
        }

        // All values in ascending key order (iterative, so it never overflows the stack)
        public List<TValue> InOrder()
        {
            var results = new List<TValue>(Count);
            var stack = new Stack<Node>();
            var current = _root;

            while (current != null || stack.Count > 0)
            {
                while (current != null)
                {
                    stack.Push(current);
                    current = current.Left;
                }

                current = stack.Pop();
                results.Add(current.Value);
                current = current.Right;
            }

            return results;
        }


        // ---------- Balancing ----------
        private static int HeightOf(Node? node) => node?.Height ?? 0;

        private static void UpdateHeight(Node node)
            => node.Height = 1 + Math.Max(HeightOf(node.Left), HeightOf(node.Right));

        private static int BalanceFactor(Node node)
            => HeightOf(node.Left) - HeightOf(node.Right);

        private static Node RotateRight(Node y)
        {
            var x = y.Left!;
            y.Left = x.Right;
            x.Right = y;
            UpdateHeight(y);
            UpdateHeight(x);
            return x;
        }

        private static Node RotateLeft(Node x)
        {
            var y = x.Right!;
            x.Right = y.Left;
            y.Left = x;
            UpdateHeight(x);
            UpdateHeight(y);
            return y;
        }

        private static Node Rebalance(Node node)
        {
            UpdateHeight(node);
            int balance = BalanceFactor(node);

            if (balance > 1)                                   // left heavy
            {
                if (BalanceFactor(node.Left!) < 0)
                    node.Left = RotateLeft(node.Left!);        // left-right case
                return RotateRight(node);
            }

            if (balance < -1)                                  // right heavy
            {
                if (BalanceFactor(node.Right!) > 0)
                    node.Right = RotateRight(node.Right!);     // right-left case
                return RotateLeft(node);
            }

            return node;
        }
    }
}
