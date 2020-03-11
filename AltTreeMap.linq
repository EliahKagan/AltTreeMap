<Query Kind="Program" />

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
        
        public bool ContainsKey(TKey key)
            => Search(key, out _) != null;
        
        // FIXME: Replace with an O(1)-space iterative implementation.
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            static IEnumerable<Node> InOrder(Node? root)
            {
                if (root == null) yield break;
                
                foreach (var node in InOrder(root.Left)) yield return node;
                yield return root;
                foreach (var node in InOrder(root.Right)) yield return node;
            }
            
            foreach (var node in InOrder(_root))
                yield return KeyValuePair.Create(node.Key, node.Value);
        }
        
        System.Collections.IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
        
        public bool Remove(TKey key)
        {
            ref var child = ref Search(key, out var parent);
            if (child == null) return false;
            
            child = Drop(child);
            --Count;
            ++_version;
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
        }
        
        /// <summary>Removes a node from the tree that contains it.</summary>
        /// <returns>The descendant that should replace it, if any.</returns>
        private Node? Drop(Node node)
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
            ++_version;
        }
        
        private Node? _root = null;
        
        private ulong _version = 0uL;
    }
    
    internal static class UnitTest {
        private static void Main()
        {
            var tree = new AltTreeMap<string, int>();
            
            tree.Add("foo", 10);
            tree.Add("bar", 20);
            tree.Add("baz", 30);
            tree.Add("quux", 40);
            tree.Add("foobar", 50);
            tree.Add("ham", 60);
            tree.Add("spam", 70);
            tree.Add("eggs", 80);
            tree.Add("speegs", 90);
            
            tree.Dump();
        }
    }
}