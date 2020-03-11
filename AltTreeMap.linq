<Query Kind="Program">
  <Namespace>System.Diagnostics.Contracts</Namespace>
  <Namespace>System.Runtime.CompilerServices</Namespace>
</Query>

#define DEBUG_REPRESENTATION_INVARIANTS
#define DEBUG_TOPOLOGY

namespace Eliah {
    public sealed class AltTreeMap<TKey, TValue>
            : IEnumerable<KeyValuePair<TKey, TValue>> {
        public AltTreeMap() : this(Comparer<TKey>.Default) { }
        
        public AltTreeMap(IComparer<TKey> comparer) => Comparer = comparer;
        
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
        
        public bool AddIfAbsent(TKey key, TValue value)
        {
            ref var child = ref Search(key, out var parent);
            if (child != null) return false;
            
            Emplace(ref child, parent, key, value);
            return true;
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
            => GetNodesInOrder().Select(node => node.Mapping).GetEnumerator();
        
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
        
        public KeyValuePair<TKey, TValue> First() => FirstNode().Mapping;
        
        public TKey FirstKey() => FirstNode().Key;
        
        public TValue FirstValue() => FirstNode().Value;
        
        public KeyValuePair<TKey, TValue> Last() => LastNode().Mapping;
        
        public TKey LastKey() => LastNode().Key;
        
        public TValue LastValue() => LastNode().Value;
        
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
            
            internal KeyValuePair<TKey, TValue> Mapping
                => KeyValuePair.Create(Key, Value);
            
            internal Node? Parent { get; set; }
            
            internal Node? Left;
            
            internal Node? Right;
            
            private object ToDump()
                => new { Key, Value, Parent, Left, Right };
        }
        
        private static void Log(string message) => Console.WriteLine(message);
        
        private static Node MinNode(Node node)
        {
            while (node.Left != null) node = node.Left;
            return node;
        }
        
        private static Node MaxNode(Node node)
        {
            while (node.Right != null) node = node.Right;
            return node;
        }
        
        private static Node? NextNode(Node node)
        {
            if (node.Right != null) return MinNode(node.Right);
            
            while (node.Parent != null && node == node.Parent.Right)
                node = node.Parent;
            
            return node.Parent;
        }
        
        private static Node? PrevNode(Node node)
        {
            // FIXME: Implement this!
            throw new NotImplementedException();
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
            
            Contract.Assert(child == null || child.Parent == parent);
            resultParent = parent;
            return ref child;
        }
        
        private void Emplace(ref Node? child, Node? parent,
                             TKey key, TValue value)
        {
            MaybeCheckRI($"emplacing ({key}, {value})");
            
            child = new Node(key, value, parent);
            ++Count;
            InvalidateEnumerators();
        }
        
        private Node FirstNode()
        {
            if (_root == null) {
                throw new InvalidOperationException(
                        "an empty tree has no first item");
            }
            
            return MinNode(_root);
        }
        
        private Node? FirstNodeOrNull()
            => _root == null ? null : MinNode(_root);
        
        private Node LastNode()
        {
            if (_root == null) {
                throw new InvalidOperationException(
                        "an empty tree has no last item");
            }
            
            return MaxNode(_root);
        }
        
        private Node? LastNodeOrNull()
            => _root == null ? null : MaxNode(_root);
        
        private IEnumerable<Node> GetNodesInOrder()
        {
            for (var cur = FirstNodeOrNull(); cur != null; cur = NextNode(cur))
                yield return cur;
        }
        
        private IEnumerable<Node> GetNodesInReverseOrder()
        {
            for (var cur = LastNodeOrNull(); cur != null; cur = PrevNode(cur))
                yield return cur;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InvalidateEnumerators()
        {
            unchecked {
                ++_version;
            }
        }
        
        [Conditional("DEBUG_REPRESENTATION_INVARIANTS")]
        private void MaybeCheckRI(string reason)
        {
            bool Check(Node node) => CheckLeft(node) && CheckRight(node);
            
            bool CheckLeft(Node node)
            {
                if (node.Left == null) return true;
                
                if (Comparer.Compare(node.Left.Key, node.Key) >= 0)
                    Log("Left child key not less than parent.");
                else if (node.Left.Parent != node)
                    Log("Left child has incorrect parent reference.");
                else if (!Check(node.Left))
                    Log("LEFT subtree contains invariant violation.");
                else return true;
                
                return false;
            }
            
            bool CheckRight(Node node)
            {
                if (node.Right == null) return true;
                
                if (Comparer.Compare(node.Key, node.Right.Key) >= 0)
                    Log("Right child key not greater than parent.");
                else if (node.Right.Parent != node)
                    Log("Right child has incorrect parent reference.");
                else if (!Check(node.Right))
                    Log("RIGHT subtree contains invariant violation.");
                else return true;
                
                return false;
            }
            
            Log($"Checking RI because: {reason}");
            
            if (_root == null)
                Log("Representation invariants OK. Tree is empty.");
            else if (_root.Parent != null)
                Log("The root of the tree thinks it has a parent node!");
            else if (Check(_root))
                Log("Representation invariants seem OK.");
            else 
                Log("Representation invariant(s) VIOLATED!");
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
            
            tree.Dump($"after building, size {tree.Count}");
            tree.Clear();
            tree.Dump($"after clearing, size {tree.Count}");
        }
    }
}