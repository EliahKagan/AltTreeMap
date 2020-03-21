<Query Kind="Program">
  <Reference>D:\configs\.nuget\packages\geocoordinate.netstandard1\1.0.1\lib\netstandard1.1\GeoCoordinate.NetStandard1.dll</Reference>
  <Reference>D:\configs\.nuget\packages\restsharp\106.6.7\lib\netstandard2.0\RestSharp.dll</Reference>
  <Reference>D:\configs\.nuget\packages\wolframalphanet\2.0.0\lib\netstandard2.0\WolframAlphaNet.dll</Reference>
  <Namespace>System.Diagnostics.CodeAnalysis</Namespace>
  <Namespace>System.Diagnostics.Contracts</Namespace>
  <Namespace>System.Runtime.CompilerServices</Namespace>
  <Namespace>System.Security.Cryptography</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>WolframAlphaNet</Namespace>
  <Namespace>WolframAlphaNet.Objects</Namespace>
</Query>

// AltTreeMap - A tree map implementation and some unit tests.

// TERMS OF USE
//
// This version of the code of AltTreeMap and its unit tests may be built to
// use the Wolfram|Alpha API to obtain some information used in the tests. To
// turn this on (or off), make Configuration.EnableWolframAlpha return true (or
// false). Use of Wolfram|Alpha, including via this application, is subject to
// the Wolfram|Alpha Terms of Use: https://www.wolframalpha.com/termsofuse/
//
// Results and information from this site are not a certified or definitive
// source of information that can be relied on for legal, financial, medical,
// life-safety or any other critical purposes.
//
// See also the Wolfram|Alpha API Terms of Use:
// https://products.wolframalpha.com/api/termsofuse.html

//#define VERBOSE_DEBUGGING
//#define DEBUG_REPRESENTATION_INVARIANTS
//#define DEBUG_TOPOLOGY

namespace Eliah {
    internal static class Configuration {
        // Long-running tests. Verbose debugging is too verbose for these.
        internal static bool EnableBigTests => true;
        
        // Use Wolfram|Alpha to double-check results in TestRefForEach.
        internal static bool EnableWolframAlpha => true;
        
        // Make some stuff wrong in TestRefForEach, to test the tests.
        internal static bool InjectWrongDataInTestRefForEach => false;
    }

    public sealed class AltTreeMap<TKey, TValue>
            : IEnumerable<KeyValuePair<TKey, TValue>> {
        public delegate void ValueMutator(TKey key, ref TValue value);
        
        public AltTreeMap() : this(Comparer<TKey>.Default) { }
        
        public AltTreeMap(IComparer<TKey> comparer)
        {
            Comparer = comparer;
            MaybeCheckRI("created empty tree");
        }
        
        public AltTreeMap(AltTreeMap<TKey, TValue> other)
            : this(other.Comparer)
        {
            // FIXME: Do this iteratively with O(1) auxiliary space instead.
            static void Copy(Node src, out Node dest, Node? destParent)
            {
                var child = new Node(src.Key, src.Value, destParent);
                dest = child;
                
                if (src.Left != null)
                    Copy(src.Left, out dest.Left, dest);
                
                if (src.Right != null)
                    Copy(src.Right, out dest.Right, dest);
            }
            
            if (other._root != null) {
                Copy(other._root, out _root, null);
                Count = other.Count;
                MaybeCheckRI("populated initial nodes from existing tree");
            }
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
            MaybeDumpNodes();
        
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
        
        public void ForEach(ValueMutator action)
        {
            foreach (var node in GetNodesInOrder())
                action(node.Key, ref node.Value);
        }
        
        public void ForEachReverse(Action<TKey, TValue> action)
        {
            foreach (var node in GetNodesInReverseOrder())
                action(node.Key, node.Value);
        }
        
        public void ForEachReverse(ValueMutator action)
        {
            foreach (var node in GetNodesInReverseOrder())
                action(node.Key, ref node.Value);
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
        
        public IEnumerable<KeyValuePair<TKey, TValue>> Reverse()
            => GetNodesInReverseOrder().Select(node => node.Mapping);
        
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
            
            internal readonly TKey Key;
            
            internal TValue Value;
            
            internal KeyValuePair<TKey, TValue> Mapping
                => KeyValuePair.Create(Key, Value);
            
            internal Node? Parent { get; set; }
            
            internal Node? Left;
            
            internal Node? Right;
            
            private object ToDump()
                => new { Key, Value, Parent, Left, Right };
        }
        
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
                Contract.Assert(next.Parent != null);
                next.Parent.Left = next.Right;
                if (next.Right != null) next.Right.Parent = next.Parent;
                
                next.Right = node.Right;
                next.Right.Parent = next;
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
        private void MaybeCheckRI(string reason)
        {
            var count = 0;
            
            bool Check(Node node)
            {
                ++count;
                return CheckLeft(node) && CheckRight(node);
            }
            
            bool CheckLeft(Node node)
            {
                if (node.Left == null) return true;
                
                if (Comparer.Compare(node.Left.Key, node.Key) >= 0)
                    Log.Warn("Left child key not less than parent.");
                else if (node.Left.Parent != node)
                    Log.Warn("Left child has incorrect parent reference.");
                else if (!Check(node.Left))
                    Log.Warn("LEFT subtree contains invariant violation.");
                else return true;
                
                return false;
            }
            
            bool CheckRight(Node node)
            {
                if (node.Right == null) return true;
                
                if (Comparer.Compare(node.Key, node.Right.Key) >= 0)
                    Log.Warn("Right child key not greater than parent.");
                else if (node.Right.Parent != node)
                    Log.Warn("Right child has incorrect parent reference.");
                else if (!Check(node.Right))
                    Log.Warn("RIGHT subtree contains invariant violation.");
                else return true;
                
                return false;
            }
            
            Log.Note($"Checking RI because: {reason}");
            
            if (_root == null && Count == 0)
                Log.Note("Representation invariants OK. Tree is empty.");
            else if (_root == null) // Count != 0
                Log.Warn($"_root is null but Count is {Count}!");
            else if (_root.Parent != null)
                Log.Warn("The root of the tree thinks it has a parent node!");
            else if (!Check(_root))
                Log.Warn("Representation invariant(s) VIOLATED!");
            else if (count != Count)
                Log.Warn($"Tree has {count} nodes but thinks it has {Count}!");
            else
                Log.Note("Representation invariants seem OK.");
        }
        
        [Conditional("DEBUG_TOPOLOGY")]
        private void MaybeDumpNodes()
            => _root.Dump($"{this} @ {PseudoAddress} [v{_version}] nodes:");
        
        private string PseudoAddress => $"0x{GetHashCode():X}";
        
        private Node? _root = null;
        
        private ulong _version = 0uL;
    }
    
    /// <summary>Simple logger for printing debug information.</summary>
    /// <remarks>TODO: Use a real logging library instead.</remarks>
    internal static class Log {
        internal static void Warn(string message) => Print(message);
        
        [Conditional("VERBOSE_DEBUGGING")]
        internal static void Note(string message) => Print(message);
        
        /// <summary>Logs the message in some default manner.</summary>
        private static void Print(string message)
            => Console.WriteLine(message);
    }
    
    internal static class UnitTest {
        private static async Task Main()
        {
            TheTerms.ShowTop();
            await RunAllEnabledTests();
            TheTerms.ShowBottom();
        }
        
        private static async Task RunAllEnabledTests()
        {
            RunGeneralTests();
            TestDeletionSmall();
            await MaybeRunBigTests();
        }
    
        private static void RunGeneralTests()
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
            tree.Reverse().ToList() // Verifies Reverse returns IEnumerable<T>.
                .Dump($"after building, size {tree.Count} (reversed)");
            
            var copy = new AltTreeMap<string, int>(tree);
            
            tree.TestEnumeratorInvalidation("ham", "waffles", -40);
            tree.TestConditionalGetMethods("foo", "Foo", "bar", "Bar",
                                           "waffles", "toast");
            tree.Dump($"after adding \"waffles\"");
            
            tree.TestRemove("bar");
            tree.TestRemove("foo");
            tree.TestRemove("waffles");
            tree.TestRemove("quux");
            tree.TestRemove("waffles");
            
            tree.Clear();
            tree.Dump($"after clearing, size {tree.Count}");
            tree.Reverse()
                .Dump($"after clearing, size {tree.Count} (reversed)");
            
            copy.Dump($"{nameof(copy)}, after changes to {nameof(tree)}");
            copy.Clear();
            copy.Dump($"{nameof(copy)}, after itself being cleared");
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
            keys.Select(key => new {
                key,
                result = tree.TryGetValue(key, out var value),
                value
            }).Dump(nameof(TestTryGetValue));
        }
        
        private static void TestGetOrDefault<TKey, TValue>(
                this AltTreeMap<TKey, TValue> tree, params TKey[] keys)
        {
            keys.Select(key => new { key, value = tree.GetOrDefault(key) })
                .Dump(nameof(TestGetOrDefault));
        }
        
        private static void TestRemove<TKey, TValue>(
                this AltTreeMap<TKey, TValue> tree, TKey key)
        {
            if (tree.Remove(key))
                tree.Dump($"key \"{key}\" removed, new size {tree.Count}");
            else
                "".Dump($"key \"{key}\" not found to remove");
        }
        
        private static async Task MaybeRunBigTests()
        {
            if (!Configuration.EnableBigTests) return;
            
            var primes = await TestDeletionBig();
            await TestRefForEach(primes);
        }
        
        private static void TestDeletionSmall()
        {
            const int window_size = 20;
            const int total_size = 100;
            const int outside_window_size = total_size - window_size;
            
            var permutation = Enumerable.Range(0, total_size).Shuffle();
            
            var window = new AltTreeMap<int, int>();
            
            foreach (var right in Enumerable.Range(0, window_size))
                window.Add(permutation[right], right);
            
            foreach (var left in Enumerable.Range(0, outside_window_size)) {
                var removed = window.Remove(permutation[left]);
                Contract.Assert(removed);
                
                var right = left + window_size;
                window.Add(permutation[right], right);
            }
            
            string.Join(", ", window).Dump("right side");
        }
        
        private static async Task<AltTreeMap<long, int?>> TestDeletionBig()
        {
            const long upper_bound = 10_000_000L;
        
            var known_task = GetPrimesFromRuby(upper_bound);
            var primes = GetPrimes(upper_bound);
            var known = await known_task;
            
            CheckMargins(primes, known);
            
            if (primes.Count != known.Length) {
                new { Found = primes.Count, Actual = known.Length }
                    .Dump("Found the WRONG number of primes!");
            } else {
                primes.Select(kv => kv.Key).SequenceEqual(known)
                      .Dump($"Were all {primes.Count:N0} primes correct?");
            }
            
            return primes;
        }
        
        private static async Task TestRefForEach(AltTreeMap<long, int?> primes)
        {
            var count = 0;
            primes.ForEach((long key, ref int? value) => value = ++count);
            
            // The known values for these and other primes could come from the
            // output of the Ruby script. Listing them here provides a tiny bit
            // of redundancy in the face of possible bugs in that script.
            //
            // TODO: After implementing AltTreeMap value bisection (usable
            // when values, taken in key order, are monotone relative to some
            // comparator), include non-primes here and make test show their
            // pi values as well.
            var known = new (int prime, int knownPi)[] {
                    (       17,       7),
                    (       31,      11),
                    (     1013,     170),
                    (  100_019,    9594),
                    (1_000_033,  78_500),
                    (3_000_029, 216_818),
                    (5_999_993, 412_849),
                    (7_499_981, 508_261),
                    (8_999_971, 602_487),
                    (9_999_973, 664_578),
            };
            
            if (Configuration.InjectWrongDataInTestRefForEach) {
                "Injecting wrong data, for testing."
                    .Dump($"In {nameof(TestRefForEach)}");
            
                // True positive (wrong tree value).
                --primes[known[4].prime];
                
                // False positive (wrong known value).
                --known[7].knownPi;
                
                // False negative (wrong tree value and wrong known value).
                --primes[known[9].prime];
                --known[9].knownPi;
            }
            
            // TODO: When the local test is much more involved than now,
            // we can do it and await Wolfram|Alpha results at the same time.
            var wolframResults = await known.Select(kv => kv.prime)
                                            .WolframAlphaSelect("PrimePI");
            
            known.Shuffle() // Might smoke out lookup-order-sensitive bugs.
                 .Select(kv => (prime: kv.prime,
                                pi: primes[kv.prime],
                                knownPi: kv.knownPi))
                 .OrderBy(row => row.prime)
                 .Zip(wolframResults.Select(int.Parse), (row, knownPi2) => {
                        var correct = (row.pi == row.knownPi ? "yes" : "No!");
                        
                        string ORLY;
                        if (row.knownPi != knownPi2)
                            ORLY = "NO WAI!!";
                        else if (row.pi == knownPi2)
                            ORLY = "ya rly";
                        else
                            ORLY = "NO RLY";
                        
                        return new { row.prime, row.pi, correct, ORLY };
                   })
                 .Dump("some primes and their ordinals", noTotals: true);
            
            // FIXME: Render HTML explaining that the values in the ORLY column
            // are from Wolfram|Alpha and giving a hyperlink to the results
            // page for the query. (If the API gives the URL for this, use
            // that. Otherwise, build the URL from wolframResults.QueryText.)
            wolframResults.FullResult
                          .Dump("Click to expand full Wolfram|Alpha results.",
                                depth: 0);
        }
        
        private static void
        CheckMargins<TValue>(AltTreeMap<long, TValue> primes, long[] known)
        {
            const int margin = 100;
            
            static string Check<T>(IEnumerable<T> lhs, IEnumerable<T> rhs)
                => lhs.SequenceEqual(rhs) ? "correct" : "WRONG!!!";
            
            var lows = primes.Select(kv => kv.Key).Take(margin).ToArray();
            var low_info = Check(lows, known[..margin]);
            string.Join(", ", lows).Dump($"very lowest primes, {low_info}");
            
            var highs = primes.Reverse().Select(kv => kv.Key)
                              .Take(margin).Reverse().ToArray();
            var high_info = Check(highs, known[^margin..]);
            string.Join(", ", highs).Dump($"fairly low primes, {high_info}");
        }
        
        private static AltTreeMap<long, int?> GetPrimes(long upperBound)
        {
            var primes = GetShuffledOdds(3L, upperBound);
            primes.Add(2L, null);
            
            checked {
                for (var i = 3L; i <= upperBound; i += 2L) {
                    for (var j = i * i; j <= upperBound; j += i * 2L)
                        primes.Remove(j);
                }
            }
            
            return primes;
        }
        
        private static AltTreeMap<long, int?>
        GetShuffledOdds(long fromInclusive, long toInclusive)
        {
            if (fromInclusive % 2L == 0L) {
                checked {
                    ++fromInclusive;
                }
            }
            
            IEnumerable<long> GetOdds()
            {
                for (var odd = fromInclusive; odd <= toInclusive; odd += 2L)
                    yield return odd;
            }
            
            var odds = new AltTreeMap<long, int?>();
            foreach (var odd in GetOdds().Shuffle()) odds.Add(odd, null);
            return odds;
        }
        
        private static async Task<long[]> GetPrimesFromRuby(long upperBound)
        {
            const string interpreter = "ruby";
            const string scriptName = "primes.rb";
            var argument = upperBound.ToString();
            
            string CmdLine()
                => $"{interpreter} {Scripts.GetPath(scriptName)} {argument}";
        
            var (status, stdout, stderr) =
                    await Scripts.Run(interpreter, scriptName, argument);
            
            if (!string.IsNullOrWhiteSpace(stderr))
                stderr.Dump($"\"{CmdLine()}\" standard error stream");
            
            if (status != 0) {
                throw new Exception(
                        $"\"{CmdLine()}\" failed with exit code {status}.");
            }
            
            var tokens = stdout.Split(default(char[]?),
                                      StringSplitOptions.RemoveEmptyEntries);
            return Array.ConvertAll(tokens, long.Parse);
        }
    }
    
    internal static class Scripts {
        internal static async Task<(int status, string stdout, string stderr)>
        Run(string interpreter, string scriptName, params string[] args)
        {
            var proc = new Process();
            
            foreach (var arg in args.Prepend(GetPath(scriptName)))
                proc.StartInfo.ArgumentList.Add(arg);
            
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.FileName = interpreter;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.UseShellExecute = false;
            
            proc.Start();
            var stdout = proc.StandardOutput.ReadToEndAsync();
            var stderr = proc.StandardError.ReadToEndAsync();
            proc.WaitForExit();
            
            return (proc.ExitCode, await stdout, await stderr);
        }
        
        internal static string GetPath(string scriptName)
            => Path.Combine(GetDirectory(), scriptName);
        
        private static string GetDirectory()
            => Path.GetDirectoryName(Util.CurrentQueryPath)
                ?? throw new FileNotFoundException(
                    message: "Can't guess script location - "
                             + "is this an unsaved LINQPad query?");
    }
    
    /// <summary>Functions for querying the Wolfram|Alpha API.</summary>
    internal static class WolframAlphaQueries {
        /// <summary>
        /// Runs a single Wolfram Langage map query via Wolfram|Alpha.
        /// </summary>
        /// <param name="arguments">The arguments (preimages) to pass.</param>
        /// <param name="function">
        /// The name of the Wolfram Language function to map through.
        /// </param>
        /// <returns>Images of argument under the specified function.</returns>
        /// <remarks>
        /// The function name is not quoted or validated. Arbitrary query code,
        /// in the Wolfram Language or otherwise, could be passed in.
        /// </remarks>
        internal static async Task<WolframAlphaSelectResults>
        WolframAlphaSelect<T>(this IEnumerable<T> arguments, string function)
        {
            // FIXME: Extract this File.ReadAllText logic into the Scripts class.
            //        Call the new function from GetTermsHtml() and from here.
            var engine = new WolframAlpha(File.ReadAllText(Scripts.GetPath("AppID")).Trim());
            
            return await Task.Run(() => WolframAlphaSelectResults.Retrieve(
                                            engine, function, arguments));
        }
    }
    
    /// <summary>
    /// Results of a WolframAlphaQueries.WolframAlphaSelect operation.
    /// </summary>
    /// <remarks>
    /// Enumerating this gives the output of the selection operation.
    /// It also provides properties to access the raw query text and the full
    /// result object.
    /// </remarks>
    internal sealed class WolframAlphaSelectResults : IEnumerable<string> {
        /// <summary>
        /// Runs a single Wolfram Language map query via Wolfram|Alpha and
        /// constructs an object that wraps the results.
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="function">
        /// The name of the Wolfram Language function to map through.
        /// </param>
        /// <param name="arguments">The arguments (preimages) to pass.</param>
        /// <remarks>
        /// Aside from it use as a helper function for the extension method
        /// WolframAlphaQueries.WolframAlphaSelect, this should only be called
        /// directly if synchronous operation--with a non-async caller--is
        /// needed.
        /// </remarks>
        internal static WolframAlphaSelectResults Retrieve<T>(
                WolframAlpha engine, string function, IEnumerable<T> arguments)
            => new WolframAlphaSelectResults(
                engine, $"{function}/@{arguments.Brace()}");
        
        /// <summary>Enumerates outputs of the selection operation.</summary>
        /// <returns>An enumerator that walks through each output.</returns>
        public IEnumerator<string> GetEnumerator()
            => OutputSequence.AsEnumerable().GetEnumerator();
        
        System.Collections.IEnumerator
        System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        
        /// <summary>The query text sent via the Wolfram|Alpha API.</summary>
        internal string QueryText { get; }
        
        /// <summary>All information returned by Wolfram|Alpha.</summary>
        internal QueryResult FullResult { get; }
        
        private static string[] ExtractSequence(QueryResult result)
            => result.Pods
                     .Where(pod => pod.ID == "Result")
                     .First()
                     .SubPods
                     .First()
                     .Plaintext
                     .Unbrace();
        
        private WolframAlphaSelectResults(WolframAlpha engine, string query)
        {
            QueryText = query;
            
            FullResult = engine.Query(query)
                    ?? throw new WolframAlphaSelectException(
                        "Wolfram|Alpha query had no result", null, null);
            
            if (!FullResult.Success) {
                throw new WolframAlphaSelectException(
                        "Wolfram|Alpha query result indicated failure",
                        null, FullResult);
            }
            
            try {
                OutputSequence = ExtractSequence(FullResult);
            }
            catch (InvalidOperationException exception) {
                // FIXME: Actually figure out what exceptions to catch here.
                throw new WolframAlphaSelectException(
                        "Can't extract output sequence from Wolfram|Alpha"
                            + " results",
                        exception, FullResult);
            }
        }
        
        private string[] OutputSequence { get; }
    }
    
    [Serializable]
    internal class WolframAlphaSelectException : InvalidOperationException {
        internal WolframAlphaSelectException(string message,
                                             Exception? innerException,
                                             QueryResult? fullResult)
            : base(message, innerException) => FullResult = fullResult;
        
        internal QueryResult? FullResult { get; }
    }
    
    internal static class Bracing {
        internal static string Brace<T>(this IEnumerable<T> items)
            => $"{{{string.Join(", ", items)}}}";
        
        internal static string[] Unbrace(this string bracedExpression)
            => unwrap.Match(bracedExpression).Groups[1].ToString().Split(", ");
        
        private static readonly Regex unwrap =
            new Regex(@"{(.+)}", RegexOptions.Compiled);
    }
    
    internal static class ListExtensions {
        internal static IList<T> Shuffle<T>(this IEnumerable<T> items)
            => items.ToList().Shuffle();
    
        internal static IList<T> Shuffle<T>(this IList<T> items)
        {
            for (var right = items.Count; right > 1; --right)
                items.Swap(ThreadSafeRandom.Next(right), right - 1);
            
            return items;
        }
    
        internal static void Swap<T>(this IList<T> items, int i, int j)
            => (items[i], items[j]) = (items[j], items[i]);
    }
    
    internal static class ThreadSafeRandom {
        internal static int Next() => Prng.Next();
        
        internal static int Next(int maxValue) => Prng.Next(maxValue);
        
        internal static int Next(int minValue, int maxValue)
            => Prng.Next(minValue, maxValue);
        
        internal static void NextBytes(byte[] buffer)
            => Prng.NextBytes(buffer);
        
        internal static double NextDouble() => Prng.NextDouble();
    
        private static Random Prng => _prng.Value!;
        
        private static Random CreatePrng()
        {
            var buffer = new byte[sizeof(int)];
            _csprng.GetBytes(buffer);
            return new Random(BitConverter.ToInt32(buffer, 0));
        }
    
        // RNGCryptoServiceProvider is thread-safe (unlike Random), so use it
        // to generate seeds for the Random instances local to each thread.
        private static RNGCryptoServiceProvider _csprng =
            new RNGCryptoServiceProvider();
        
        private static ThreadLocal<Random> _prng =
            new ThreadLocal<Random>(CreatePrng);
    }
    
    internal static class TheTerms {
        internal static void ShowTop()
        {
            // FIXME: <p> and <strong> should get their formatting from CSS,
            // not physical tags. Figure out how to get LINQPad to use a custom
            // stylesheet (for an indiviual dumped result).
            var html = GetTermsHtml().Replace("<p>", "<br/><p>")
                                     .Replace("<strong>", "<strong><b>")
                                     .Replace("</strong>", "</b></strong>");
            
            Util.RawHtml(html).Dump();
            Console.WriteLine();
        }
        
        internal static void ShowBottom()
        {
            var msg = $"See the top of the page for this software's {Name}.";
            
            var html = "<br/><hr/>"
                        + $@"<p><a href=""#terms-heading"">{msg}</a></p>";
            
            Util.RawHtml(html).Dump();
        }
        
        private static string GetTermsHtml()
            => Regex.Match(File.ReadAllText(Scripts.GetPath($"{Name}.html")),
                           $@"{Guide("BEGIN")}(.+?){Guide("END")}",
                           RegexOptions.Singleline)
                    .Groups[1].ToString();
        
        private static string Guide(string command)
            => Regex.Escape($@"<!-- {GuideIntroducer}: {command} -->");
        
        private const string Name = "Terms of Use";
        
        private const string GuideIntroducer = "EXTRACTION FOR IN-APP DISPLAY";
    }
}