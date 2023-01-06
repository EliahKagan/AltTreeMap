<Query Kind="Program">
  <Namespace>System.Diagnostics.CodeAnalysis</Namespace>
  <Namespace>System.Diagnostics.Contracts</Namespace>
  <Namespace>System.Runtime.CompilerServices</Namespace>
  <Namespace>System.Security.Cryptography</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <RuntimeVersion>6.0</RuntimeVersion>
</Query>

// AltTreeMap - A tree map implementation and some ad-hoc unit tests.

// Copyright (c) 2020, 2023 Eliah Kagan
//
// Permission to use, copy, modify, and/or distribute this software for any
// purpose with or without fee is hereby granted.
//
// THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
// WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
// MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY
// SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
// WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION
// OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN
// CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

#nullable enable

// When defined, compile calls to the CheckRI delegate, which will still be
// null at runtime unless Log.LoggingRequested is set to true. Undefining this
// conditional symbol may yield a small (but probably measurable) speedup.
#define DEBUG_REPRESENTATION_INVARIANTS

// When defined, compile calls to the DumpNodes delegate, which will still be
// null at runtime unless Log.LoggingRquested is set to true, and which, even
// when non-null, will only do anything if Configuration.VerboseDebugging
// evaluates to true. Undefining this conditional symbol might yield a small
// speedup, but it would most likely be negligible, as DumpNodes calls don't
// occur in hot code.
#define DEBUG_TOPOLOGY

namespace Ekgn {
    /// <summary>
    /// Knobs for some debugging- and testing-related behavior.
    /// </summary>
    /// <remarks>
    /// This class collects properties that are fixed at compile time by
    /// editing the code contained here. The reasons these are given as
    /// properties and not <c>#define</c>s are so the compiler can always check
    /// more code paths, and because <c>#define</c>s are cumbersome in some
    /// situations (e.g., <c>async</c> methods can't have <c>Conditional</c>
    /// attributes, so <c>#if</c> would have to be used). They are properties
    /// rather than <c>const</c>s to avoid warnings about unreachable code.
    /// </remarks>
    internal static class Configuration {
        /// <summary>Print debug messages some of the time.</summary>
        /// <remarks>
        /// Setting this to <c>false</c> currently turns off all debug checks
        /// and debugging output.
        /// </remarks>
        internal static bool EnableDebugging => false;

        /// <summary>
        /// Don't limit debug messages to errors and warnings.
        /// </summary>
        /// <remarks>
        /// <see cref="EnableDebugging"/> must still be <c>true</c>.
        /// </remarks>
        internal static bool EnableVerboseDebugging => false;

        /// <summary>Turns on long-running tests.</summary>
        /// <remarks>
        /// Verbose debugging is too verbose for these tests. Also, these tests
        /// will take a very long time when representation-invariant debugging
        /// is turned on.
        /// </remarks>
        internal static bool EnableBigTests => true;

        /// <summary>
        /// Make some stuff wrong in <c>TestRefForEach</c>, to test the tests.
        /// </summary>
        internal static bool InjectWrongDataInTestRefForEach => false;
    }

    /// <summary>BST implementation of an ordered associative array.</summary>
    /// <remarks>This version is not self-balancing.</remarks>
    public sealed class AltTreeMap<TKey, TValue>
            : IEnumerable<KeyValuePair<TKey, TValue>> {
        public delegate void ValueMutator(TKey key, ref TValue value);

        public AltTreeMap() : this(Comparer<TKey>.Default) { }

        public AltTreeMap(IComparer<TKey> comparer)
        {
            Comparer = comparer;
#if DEBUG_REPRESENTATION_INVARIANTS
            CheckRI?.Invoke("created empty tree");
#endif
        }

        public AltTreeMap(AltTreeMap<TKey, TValue> other)
            : this(other.Comparer)
        {
            // TODO: Do this iteratively with O(1) auxiliary space instead.
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
#if DEBUG_REPRESENTATION_INVARIANTS
                CheckRI?.Invoke("populated initial nodes from existing tree");
#endif
            }
        }

        public IComparer<TKey> Comparer { get; }

        public int Count { get; private set; } = 0;

        public TValue this[TKey key]
        {
            get {
                var node = Search(key, out _);

                if (node == null)
                    throw new KeyNotFoundException("Key not found.");

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
                                            message: "Key already exists.");
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
#if DEBUG_TOPOLOGY
            DumpNodes?.Invoke();
#endif
            _root = null;
            Count = 0;
            InvalidateEnumerators();
#if DEBUG_REPRESENTATION_INVARIANTS
            CheckRI?.Invoke("cleared all nodes");
#endif
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

        public TValue? GetOrDefault(TKey key, TValue? value = default)
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
#if DEBUG_REPRESENTATION_INVARIANTS
            CheckRI?.Invoke($"removed node with key: {key}");
#endif
            return true;
        }

        public IEnumerable<KeyValuePair<TKey, TValue>> Reverse()
            => GetNodesInReverseOrder().Select(node => node.Mapping);

        public bool TryGetValue(TKey key,
                                [MaybeNullWhen(false)] out TValue value)
        {
            var child = Search(key, out _);

            if (child == null) {
                value = default;
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
                } else if (comp != 0) {
                    parent = child;
                    child = ref parent.Right;
                } else break;
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
#if DEBUG_REPRESENTATION_INVARIANTS
            CheckRI?.Invoke($"emplaced ({key}, {value})");
#endif
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

#if DEBUG_REPRESENTATION_INVARIANTS // So unguarded calls fail to compile.
        private Action<string>? CheckRI
            => Log.LoggingRequested ? DoCheckRI : default(Action<string>?);
#endif

        private void DoCheckRI(string reason)
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

#if DEBUG_TOPOLOGY // So unguarded calls fail to compile.
        private Action? DumpNodes
            => Log.LoggingRequested ? DoDumpNodes : default(Action?);
#endif

        private void DoDumpNodes()
        {
            // Only dump nodes if debugging verbosely.
            if (Configuration.EnableVerboseDebugging)
                _root.Dump($"{this} @ {PseudoAddress} [v{_version}] nodes:");
        }

        private string PseudoAddress => $"0x{GetHashCode():X}";

        private Node? _root = null;

        private ulong _version = 0uL;
    }

    /// <summary>Simple logger for printing debug information.</summary>
    /// <remarks>TODO: Use a real logging library instead.</remarks>
    internal static class Log {
        /// <summary>Prints a warning message, indicating a problem.</summary>
        internal static void Warn(string message)
            => Console.WriteLine(message);

        /// <summary>Prints a notice, not indicating a problem.</summary>
        /// <remarks>Notices are emitted only with verbose debugging.</remarks>
        internal static void Note(string message)
        {
            if (Configuration.EnableVerboseDebugging)
                Console.WriteLine(message);
        }

        /// <summary>
        /// For use by code outside this class to determine if logging should
        /// be done. Currently, no methods in this class check this property.
        /// </summary>
        /// <remarks>Not thread-safe without memory barriers.</remarks>
        internal static bool LoggingRequested { get; set; } = false;
    }

    internal static class UnitTest {
        private static async Task Main()
        {
            if (Configuration.EnableDebugging) Log.LoggingRequested = true;
            RunGeneralTests();
            TestDeletionSmall();

            Log.LoggingRequested = false;
            await MaybeRunBigTestsAsync();
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

            tree.Dump($"after building, size {tree.Count}", noTotals: true);

            tree.Reverse()
                .ToList() // Verifies Reverse returns IEnumerable<T>.
                .Dump($"after building, size {tree.Count} (reversed)",
                      noTotals: true);

            var copy = new AltTreeMap<string, int>(tree);

            tree.TestEnumeratorInvalidation("ham", "waffles", -40);
            tree.TestConditionalGetMethods("foo", "Foo", "bar", "Bar",
                                           "waffles", "toast");
            tree.Dump($"after adding \"waffles\"", noTotals: true);

            tree.TestRemove("bar");
            tree.TestRemove("foo");
            tree.TestRemove("waffles");
            tree.TestRemove("quux");
            tree.TestRemove("waffles");

            tree.TestDuplicateAdd("ham", 42);
            tree.TestAbsentKey("waffles");

            tree.Clear();
            tree.Dump($"after clearing, size {tree.Count}");
            tree.Reverse()
                .Dump($"after clearing, size {tree.Count} (reversed)");

            copy.Dump($"{nameof(copy)}, after changes to {nameof(tree)}",
                      noTotals: true);
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
            } catch (InvalidOperationException e) {
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
            }).Dump(nameof(TestTryGetValue), noTotals: true);
        }

        private static void TestGetOrDefault<TKey, TValue>(
                this AltTreeMap<TKey, TValue> tree, params TKey[] keys)
        {
            keys.Select(key => new { key, value = tree.GetOrDefault(key) })
                .Dump(nameof(TestGetOrDefault), noTotals: true );
        }

        private static void TestRemove<TKey, TValue>(
                this AltTreeMap<TKey, TValue> tree, TKey key)
        {
            if (tree.Remove(key)) {
                tree.Dump($"key \"{key}\" removed, new size {tree.Count}",
                          noTotals: true);
            } else {
                "".Dump($"key \"{key}\" not found to remove");
            }
        }

        private static void TestDuplicateAdd<TKey, TValue>(
                this AltTreeMap<TKey, TValue> tree, TKey key, TValue value)
        {
            try {
                tree.Add(key, value);
                "".Dump("Should not have been able to add duplicate key!");
            } catch (ArgumentException e) {
                e.Dump("Correctly threw on attempt to add duplicate key.");
            }
        }

        private static void TestAbsentKey<TKey, TValue>(
                this AltTreeMap<TKey, TValue> tree, TKey key)
        {
            try {
                tree[key].Dump(
                        "Should not have been able to index with this key!");
            } catch (KeyNotFoundException e) {
                e.Dump("Correctly threw on attempt to index with absent key.");
            }
        }

        private static async Task MaybeRunBigTestsAsync()
        {
            if (!Configuration.EnableBigTests) return;

            var primes = await TestDeletionBigAsync();
            TestRefForEach(primes);
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

        private static async Task<AltTreeMap<long, int?>>
        TestDeletionBigAsync()
        {
            const long upper_bound = 10_000_000L;

            var known_task = GetPrimesFromRubyAsync(upper_bound);
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

        private static void TestRefForEach(AltTreeMap<long, int?> primes)
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

            known.Shuffle() // Might smoke out lookup-order-sensitive bugs.
                 .Select(kv => (prime: kv.prime,
                                pi: primes[kv.prime],
                                knownPi: kv.knownPi))
                 .OrderBy(row => row.prime)
                 .Select(row => new {
                        row.prime,
                        row.pi,
                        correct = (row.pi == row.knownPi ? "yes" : "NO!")
                     })
                 .Dump("some primes and their ordinals", noTotals: true);
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

        private static async Task<long[]>
        GetPrimesFromRubyAsync(long upperBound)
        {
            const string interpreter = "ruby";
            const string scriptName = "primes.rb";
            var argument = upperBound.ToString();

            string CmdLine()
                => $"{interpreter} {Scripts.GetPath(scriptName)} {argument}";

            var (status, stdout, stderr) =
                    await Scripts.RunAsync(interpreter, scriptName, argument);

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
        RunAsync(string interpreter, string scriptName, params string[] args)
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
            await proc.WaitForExitAsync();

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

        // Unlike Random, System.Security.Cryptography.RandomNumberGenerator is
        // thread safe. I use it to seed Random instances local to each thread.
        private static RandomNumberGenerator _csprng =
            RandomNumberGenerator.Create();

        private static ThreadLocal<Random> _prng =
            new ThreadLocal<Random>(CreatePrng);
    }
}
