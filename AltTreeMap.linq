<Query Kind="Program" />

namespace Eliah {
    public sealed class AltTreeMap<TKey, TValue> {
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
            var tree = new AltTreeMap<string, decimal>();
            Console.WriteLine($"Hello, {tree}! ");
        }
    }
}