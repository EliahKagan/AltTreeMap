<Query Kind="Program">
  <Namespace>System.Runtime.CompilerServices</Namespace>
</Query>

//#define DEBUG_TOPOLOGY

namespace Eliah {
    public sealed class AltTreeMap<TKey, TValue>
            : IEnumerable<KeyValuePair<TKey, TValue>> {
        public AltTreeMap() : this(Comparer<TKey>.Default) { }
        
        public AltTreeMap(IComparer<TKey> comparer)
            => Comparer = comparer;
        
        public IComparer<TKey> Comparer { get; }
        
        public int Count { get; private set; } = 0;
        
        public TValue this[TKey key]
        {
            get {
                var node = Search(key, out _);
                
                if (node == null) {
                    throw new ArgumentException(paramName: nameof(key),
                                                message: "key not found");
                }
                
                return node.Value;
            }
            
            set {
                ref var child = ref Search(key, out var parent);
                
                if (child == null)
                    Emplace(ref child, parent, key, value);
                else
                    child.Value = value;
            }
        }
        
        public void Add(TKey key, TValue value)
        {
            ref var child = ref Search(key, out var parent);
            
            if (child != null) {
                throw new ArgumentException(paramName: nameof(key),
                                            message: "key already exists");
            }
            
            Emplace(ref child, parent, key, value);
        }
        
        public void Clear()
        {
            _root = null;
            Count = 0;
            InvalidateEnumerators();
        }
        
        public bool ContainsKey(TKey key)
            => Search(key, out _) != null;
        
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            => GetNodesInOrder()
                .Select(node => KeyValuePair.Create(node.Key, node.Value))
                .GetEnumerator();
        
        System.Collections.IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
        
        public bool Remove(TKey key)
        {
            ref var child = ref Search(key, out var parent);
            if (child == null) return false;
            
            child = Drop(child);
            --Count;
            InvalidateEnumerators();
            return true;
        }
        
        private sealed class Node {
            internal Node(TKey key, TValue value, Node? parent)
                : this(key, value, parent, null, null) { }
        
            internal Node(TKey key, TValue value,
                          Node? parent, Node? left, Node? right)
            {
                Key = key;
                Value = value;
                Parent = parent;
                Left = left;
                Right = right;
            }
            
            internal TKey Key { get; }
            
            internal TValue Value { get; set; }
            
            internal Node? Parent { get; set; }
            
            internal Node? Left;
            
            internal Node? Right;
            
            private object ToDump()
                => new { Key, Value, Parent, Left, Right };
        }
        
        /// <summary>Removes a node from the tree that contains it.</summary>
        /// <returns>The descendant that should replace it, if any.</returns>
        private static Node? Drop(Node node)
        {
            // FIXME: Implement this!
            throw new NotImplementedException();
        }
        
        private ref Node? Search(TKey key, out Node? resultParent)
        {
            var parent = default(Node?);
            ref var child = ref _root;
            
            while (child != null) {
                var comp = Comparer.Compare(key, child.Key);
                
                if (comp < 0) {
                    parent = child;
                    child = ref parent.Left;
                }
                else if (comp != 0) {
                    parent = child;
                    child = ref parent.Right;
                }
                else break;
            }
            
            resultParent = parent;
            return ref child;
        }
        
        private void Emplace(ref Node? child, Node? parent,
                             TKey key, TValue value)
        {
            child = new Node(key, value, parent);
            ++Count;
            InvalidateEnumerators();
        }
        
        private IEnumerable<Node> GetNodesInOrder()
        {
            var last = default(Node?);
        
            for (var node = _root; node != null; ) {
                // Go left as far as possible.
                while (node.Left != null) node = node.Left;
                
                if (node.Right == null || node.Right != last) {
                    // We've emitted all nodes left of here but nodes right of
                    // here. So emit the current node.
                    yield return node;
                }
                
                if (node.Right != null && node.Right != last) {
                    // We haven't gone right of here yet but we can. Do so now.
                    node = node.Right;
                } else {
                    // We've emitted all nodes right of here. Retreat.
                    last = node;
                    node = node.Parent;
                }
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InvalidateEnumerators()
        {
            unchecked {
                ++_version;
            }
        }
        
        [Conditional("DEBUG_TOPOLOGY")]
        private void MaybeDumpNodes() => _root.Dump();
        
        private Node? _root = null;
        
        private ulong _version = 0uL;
    }
    
    internal static class UnitTest {
        private static void Main()
        {
            var tree = new AltTreeMap<string, int> {
                { "foo", 10 },
                { "bar", 20 },
                { "baz", 30 },
                { "quux", 40 },
                { "foobar", 50 },
                { "ham", 60 },
                { "spam", 70 },
                { "eggs", 80 },
                { "speegs", 90 },
            };
            
            //tree.Dump($"after building, size {tree.Count}");
            //tree.Clear();
            //tree.Dump($"after clearing, size {tree.Count}");
        }
    }
}