<Query Kind="Program">
  <Namespace>System.Diagnostics.CodeAnalysis</Namespace>
  <Namespace>System.Diagnostics.Contracts</Namespace>
  <Namespace>System.Runtime.CompilerServices</Namespace>
</Query>

#define VERBOSE_DEBUGGING
#define DEBUG_REPRESENTATION_INVARIANTS
#define DEBUG_TOPOLOGY

namespace Eliah {
    public sealed class AltTreeMap<TKey, TValue>
            : IEnumerable<KeyValuePair<TKey, TValue>> {
        public AltTreeMap() : this(Comparer<TKey>.Default) { }
        
        public AltTreeMap(IComparer<TKey> comparer)
        {
            Comparer = comparer;
            MaybeCheckRI("created empty tree");
        }
        
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
            MaybeCheckRI("cleared all nodes");
        }
        
        public bool ContainsKey(TKey key)
            => Search(key, out _) != null;
        
        public void ForEach(Action<TKey, TValue> action)
        {
            foreach (var node in GetNodesInOrder())
                action(node.Key, node.Value);
        }
        
        public void ForEachReverse(Action<TKey, TValue> action)
        {
            foreach (var node in GetNodesInReverseOrder())
                action(node.Key, node.Value);
        }
        
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            => GetNodesInOrder()
                .Select(node => node.Mapping)
                .GetEnumerator();
        
        System.Collections.IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
        
        public TValue GetOrDefault(TKey key, TValue value = default)
        {
            var child = Search(key, out _);
            return child == null ? value : child.Value;
        }
        
        public bool Remove(TKey key)
        {
            ref var child = ref Search(key, out var parent);
            if (child == null) return false;
            
            child = Drop(child);
            --Count;
            InvalidateEnumerators();
            MaybeCheckRI($"removed node with key: {key}");
            return true;
        }
        
        public IEnumerator<KeyValuePair<TKey, TValue>> Reverse()
            => GetNodesInReverseOrder()
                .Select(node => node.Mapping)
                .GetEnumerator();
        
        public bool TryGetValue(TKey key,
                                [MaybeNullWhen(false)] out TValue value)
        {
            var child = Search(key, out _);
            
            if (child == null) {
                value = default!; // FIXME: Given MaybeNullWhen, is "!" needed?
                return false;
            }
            
            value = child.Value;
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
        
        /// <summary>Helper funtion for Note() and Warn().</summary>
        /// <remarks>So we don't always have to use the Console.</remarks>
        // TODO: Log(), Note(), and Warn() should really go outside AltTreeMap.
        private static void Log(string message) => Console.WriteLine(message);
        
        [Conditional("VERBOSE_DEBUGGING")]
        private static void Note(string message) => Log(message);
        
        private static void Warn(string message) => Log(message);
        
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
            if (node.Left != null) return MaxNode(node.Left);
            
            while (node.Parent != null && node == node.Parent.Left)
                node = node.Parent;
            
            return node.Parent;
        }
        
        /// <summary>Removes a node from the tree that contains it.</summary>
        /// <returns>The descendant that should replace it, if any.</returns>
        private static Node? Drop(Node node)
        {
            if (node.Left == null) {
                if (node.Right == null) return null;
                
                node.Right.Parent = node.Parent;
                return node.Right;
            }
            
            if (node.Right == null) {
                node.Left.Parent = node.Parent;
                return node.Left;
            }
            
            var next = MinNode(node.Right);
            
            if (next != node.Right) {
                next.Parent.Left = next.Right;
                if (next.Right != null) next.Right.Parent = next.Parent;
            }
            
            next.Left = node.Left;
            next.Left.Parent = next;
            next.Parent = node.Parent;
            
            return next;
        }
        
        private static void InvalidEnumeratorUsed()
            => throw new InvalidOperationException(
                    "tree structure modified during enumeration");
        
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
            child = new Node(key, value, parent);
            ++Count;
            InvalidateEnumerators();
            MaybeCheckRI($"emplaced ({key}, {value})");
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
            var ver = _version;
            
            for (var node = FirstNodeOrNull(); node != null;
                                               node = NextNode(node)) {
                if (_version != ver) InvalidEnumeratorUsed();
                yield return node;
            }
        }
        
        private IEnumerable<Node> GetNodesInReverseOrder()
        {
            var ver = _version;
        
            for (var node = LastNodeOrNull(); node != null;
                                              node = PrevNode(node)) {
                if (_version != ver) InvalidEnumeratorUsed();
                yield return node;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InvalidateEnumerators()
        {
            unchecked {
                ++_version;
            }
        }
        
        [Conditional("DEBUG_REPRESENTATION_INVARIANTS")]
        private void MaybeCheckRI(string reason) // TODO: also validate Count
        {
            bool Check(Node node) => CheckLeft(node) && CheckRight(node);
            
            bool CheckLeft(Node node)
            {
                if (node.Left == null) return true;
                
                if (Comparer.Compare(node.Left.Key, node.Key) >= 0)
                    Warn("Left child key not less than parent.");
                else if (node.Left.Parent != node)
                    Warn("Left child has incorrect parent reference.");
                else if (!Check(node.Left))
                    Warn("LEFT subtree contains invariant violation.");
                else return true;
                
                return false;
            }
            
            bool CheckRight(Node node)
            {
                if (node.Right == null) return true;
                
                if (Comparer.Compare(node.Key, node.Right.Key) >= 0)
                    Warn("Right child key not greater than parent.");
                else if (node.Right.Parent != node)
                    Warn("Right child has incorrect parent reference.");
                else if (!Check(node.Right))
                    Warn("RIGHT subtree contains invariant violation.");
                else return true;
                
                return false;
            }
            
            Note($"Checking RI because: {reason}");
            
            if (_root == null)
                Note("Representation invariants OK. Tree is empty.");
            else if (_root.Parent != null)
                Warn("The root of the tree thinks it has a parent node!");
            else if (Check(_root))
                Note("Representation invariants seem OK.");
            else 
                Warn("Representation invariant(s) VIOLATED!");
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
            tree.Reverse().Dump($"after building, size {tree.Count} (reversed)");
            
            tree.TestEnumeratorInvalidation("ham", "waffles", -40);
            
            tree.TestConditionalGetMethods("foo", "Foo", "bar", "Bar",
                                           "waffles", "toast");
            
            tree.Clear();
            tree.Dump($"after clearing, size {tree.Count}");
            tree.Reverse().Dump($"after clearing, size {tree.Count} (reversed)");
        }
        
        private static void TestEnumeratorInvalidation<TKey, TValue>(
                this AltTreeMap<TKey, TValue> tree,
                TKey triggerKey, TKey newKey, TValue newValue)
        {
            var tokens = new List<string>(tree.Count);
            
            try {
                tree.ForEach((key, value) => {
                    tokens.Add($"{key},{value}");
                    
                    if (tree.Comparer.Compare(key, triggerKey) == 0)
                        tree.Add(newKey, newValue);
                });
            }
            catch (InvalidOperationException e) {
                e.Dump(string.Join("; ", tokens));
            }
        }
        
        private static void TestConditionalGetMethods<TKey, TValue>(
                this AltTreeMap<TKey, TValue> tree, params TKey[] keys)
        {
            tree.TestTryGetValue(keys);
            tree.TestGetOrDefault(keys);
        }
        
        private static void TestTryGetValue<TKey, TValue>(
                this AltTreeMap<TKey, TValue> tree, params TKey[] keys)
        {
            keys.Select(key => {
                var result = tree.TryGetValue(key, out var value);
                return new { key, result, value };
            }).Dump(nameof(TestTryGetValue));
        }
        
        private static void TestGetOrDefault<TKey, TValue>(
                this AltTreeMap<TKey, TValue> tree, params TKey[] keys)
        {
            keys.Select(key => new { key, value = tree.GetOrDefault(key) })
                .Dump(nameof(TestGetOrDefault));
        }
    }
}