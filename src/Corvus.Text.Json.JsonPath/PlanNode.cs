// <copyright file="PlanNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// Base class for execution plan step nodes. The plan is a tree compiled from
/// the JSONPath AST that the <see cref="PlanInterpreter"/> walks at evaluation
/// time. Plan nodes are allocated once at compile time and cached; the interpreter
/// itself is a ref struct that allocates nothing on the managed heap.
/// </summary>
internal abstract class PlanNode
{
}

/// <summary>
/// Terminal step: append the current node to the result buffer.
/// </summary>
internal sealed class EmitStep : PlanNode
{
    /// <summary>
    /// Shared singleton instance — every terminal in the plan tree points here.
    /// </summary>
    internal static readonly EmitStep Instance = new();
}

// ── Singleton navigation ─────────────────────────────────────────────
//
// These steps handle the common case where a child segment has exactly one
// name or index selector. They call TryGetProperty / index access (which
// internally enumerates the object / array once) and are the optimal shape
// for single-property drilldowns like $.store.book[0].title.

/// <summary>
/// Navigate to a named property of the current node, then continue.
/// <para>
/// <c>TryGetProperty</c> internally enumerates object properties to find
/// the match. This is the right shape for a single name on a single
/// object — the cost is one enumeration of that object.
/// </para>
/// </summary>
internal sealed class NavigateNameStep : PlanNode
{
    internal NavigateNameStep(byte[] utf8Name, PlanNode continuation)
    {
        Utf8Name = utf8Name;
        Continuation = continuation;
    }

    /// <summary>Gets the unescaped UTF-8 property name.</summary>
    internal byte[] Utf8Name { get; }

    /// <summary>Gets the plan node to execute on the matched property value.</summary>
    internal PlanNode Continuation { get; }
}

/// <summary>
/// Navigate to an array element by index, then continue.
/// </summary>
internal sealed class NavigateIndexStep : PlanNode
{
    internal NavigateIndexStep(long index, PlanNode continuation)
    {
        Index = index;
        Continuation = continuation;
    }

    /// <summary>Gets the array index (may be negative for end-relative).</summary>
    internal long Index { get; }

    /// <summary>Gets the plan node to execute on the matched element.</summary>
    internal PlanNode Continuation { get; }
}

/// <summary>
/// Fused chain of singleton navigations (name or index lookups) followed by
/// a continuation. The planner emits this when consecutive child segments each
/// have a single name or index selector — replacing a deep delegate chain with
/// a flat loop. Each step navigates into a <em>different</em> nested object,
/// so the enumerations cannot be fused further.
/// </summary>
internal sealed class SingletonChainStep : PlanNode
{
    internal SingletonChainStep(SingularNav[] steps, PlanNode continuation)
    {
        Steps = steps;
        Continuation = continuation;
    }

    /// <summary>Gets the ordered navigation steps.</summary>
    internal SingularNav[] Steps { get; }

    /// <summary>Gets the plan node to execute after the chain completes.</summary>
    internal PlanNode Continuation { get; }
}

/// <summary>
/// A single navigation step within a <see cref="SingletonChainStep"/>: either
/// a named property lookup or an indexed array access.
/// </summary>
internal readonly struct SingularNav
{
    internal SingularNav(byte[] utf8Name)
    {
        Utf8Name = utf8Name;
        Index = 0;
        IsName = true;
    }

    internal SingularNav(long index)
    {
        Utf8Name = null;
        Index = index;
        IsName = false;
    }

    /// <summary>Gets the UTF-8 property name, or null for index steps.</summary>
    internal byte[]? Utf8Name { get; }

    /// <summary>Gets the array index (only meaningful when <see cref="IsName"/> is false).</summary>
    internal long Index { get; }

    /// <summary>Gets a value indicating whether this is a name lookup (true) or index access (false).</summary>
    internal bool IsName { get; }
}

// ── Fused multi-name enumeration ─────────────────────────────────────
//
// When a child segment has multiple name selectors like ['a','b','c'], calling
// TryGetProperty for each name would enumerate the object N times. Instead,
// NameSetStep enumerates the object ONCE, hashing each property name to find
// matches in the dispatch table, buffering matched values in selector-order
// slots, and breaking early when all names have been found.

/// <summary>
/// Fused multi-name selector: enumerates the current object exactly once,
/// using hash dispatch to collect named properties, then processes results
/// in selector declaration order.
/// </summary>
/// <remarks>
/// <para>
/// For <c>['a','b','c']</c>, the interpreter:
/// <list type="number">
///   <item>Allocates <c>N</c> <see cref="JsonElement"/> slots on the stack (or pool for large N).</item>
///   <item>Enumerates object properties once, hashing each name and looking up in the dispatch table.</item>
///   <item>When a name matches, stores the value in the corresponding slot and increments matchCount.</item>
///   <item>When <c>matchCount == N</c>, breaks out of the enumeration early.</item>
///   <item>After enumeration, iterates slots in selector order, calling each non-empty slot's continuation.</item>
/// </list>
/// </para>
/// <para>
/// This preserves RFC 9535 selector-order semantics: results appear in
/// selector declaration order (a, b, c) regardless of the document order
/// of properties in the JSON object.
/// </para>
/// </remarks>
internal sealed class NameSetStep : PlanNode
{
    internal NameSetStep(NameSetEntry[] entries, NameDispatchTable dispatch)
    {
        Entries = entries;
        Dispatch = dispatch;
    }

    /// <summary>
    /// Gets the entries in selector declaration order. Each entry holds the
    /// UTF-8 name and the continuation to execute when the property is found.
    /// The index into this array is the "slot index" used by the dispatch table.
    /// </summary>
    internal NameSetEntry[] Entries { get; }

    /// <summary>
    /// Gets the hash-dispatch table that maps a property name to its slot index
    /// in <see cref="Entries"/>.
    /// </summary>
    internal NameDispatchTable Dispatch { get; }
}

/// <summary>
/// An entry in a <see cref="NameSetStep"/> for a single name selector,
/// holding its UTF-8 name and continuation plan node.
/// </summary>
internal readonly struct NameSetEntry
{
    internal NameSetEntry(byte[] utf8Name, PlanNode continuation)
    {
        Utf8Name = utf8Name;
        Continuation = continuation;
    }

    /// <summary>Gets the unescaped UTF-8 property name.</summary>
    internal byte[] Utf8Name { get; }

    /// <summary>Gets the plan node to execute on the matched value.</summary>
    internal PlanNode Continuation { get; }
}

/// <summary>
/// A lightweight hash-dispatch table mapping UTF-8 property names to integer
/// slot indices. Uses a hash function optimised for short property names
/// with separate chaining for collision resolution.
/// </summary>
/// <remarks>
/// <para>
/// The table is built once at plan-compile time and reused for every evaluation.
/// Lookup is O(1) average-case via <see cref="TryGetSlotIndex"/>.
/// </para>
/// <para>
/// RFC 9535 allows duplicate names in union selectors (e.g. <c>['a','a']</c>).
/// If the same name appears multiple times, only the first occurrence in
/// selector order will be found by <see cref="TryGetSlotIndex"/>. The
/// interpreter handles duplicates by calling this once per property during
/// enumeration and tracking which slots have already been filled.
/// </para>
/// </remarks>
internal sealed class NameDispatchTable
{
    private readonly int _bucketCount;
    private readonly int[] _buckets;
    private readonly DispatchEntry[] _entries;
    private readonly byte[][] _names;

    internal NameDispatchTable(byte[][] names)
    {
        _names = names;
        int count = names.Length;
        _bucketCount = GetPrime(count);
        _buckets = new int[_bucketCount];
        _entries = new DispatchEntry[count];

        for (int i = 0; i < count; i++)
        {
            ulong hashCode = HashName(names[i]);
            ref int bucket = ref _buckets[(int)(hashCode % (ulong)_bucketCount)];
            _entries[i] = new DispatchEntry(hashCode, bucket - 1, i);
            bucket = i + 1; // 1-based
        }
    }

    /// <summary>
    /// Gets the number of entries (names) in the dispatch table.
    /// </summary>
    internal int Count => _entries.Length;

    /// <summary>
    /// Looks up a property name and returns the slot index in the
    /// <see cref="NameSetStep.Entries"/> array.
    /// </summary>
    /// <param name="utf8Name">The unescaped UTF-8 property name from the JSON being evaluated.</param>
    /// <param name="slotIndex">When found, the zero-based slot index; otherwise -1.</param>
    /// <returns><see langword="true"/> if the name was found in the table.</returns>
    internal bool TryGetSlotIndex(ReadOnlySpan<byte> utf8Name, out int slotIndex)
    {
        ulong hashCode = HashName(utf8Name);
        int i = _buckets[(int)(hashCode % (ulong)_bucketCount)] - 1;
        uint collisionCount = 0;

        while ((uint)i < (uint)_entries.Length)
        {
            ref readonly DispatchEntry entry = ref _entries[i];
            if (entry.HashCode == hashCode &&
                utf8Name.SequenceEqual(_names[entry.SlotIndex]))
            {
                slotIndex = entry.SlotIndex;
                return true;
            }

            i = entry.Next;
            if (++collisionCount > (uint)_entries.Length)
            {
                break;
            }
        }

        slotIndex = -1;
        return false;
    }

    /// <summary>
    /// Computes a hash code for a UTF-8 property name, optimised by length.
    /// Same algorithm as <c>PropertySchemaMatchers.PropertyMap.GetHashCode</c>.
    /// </summary>
    private static ulong HashName(ReadOnlySpan<byte> key)
    {
        int length = key.Length;

        return length switch
        {
            7 => MemoryMarshal.Read<uint>(key.Slice(0, 4))
                    + ((ulong)key[4] << 32)
                    + ((ulong)key[5] << 40)
                    + ((ulong)key[6] << 48),
            6 => MemoryMarshal.Read<uint>(key.Slice(0, 4))
                    + ((ulong)key[4] << 32)
                    + ((ulong)key[5] << 40),
            5 => MemoryMarshal.Read<uint>(key.Slice(0, 4))
                    + ((ulong)key[4] << 32),
            4 => MemoryMarshal.Read<uint>(key.Slice(0, 4)),
            3 => ((ulong)key[2] << 16)
                    + ((ulong)key[1] << 8)
                    + key[0],
            2 => ((ulong)key[1] << 8)
                    + key[0],
            1 => key[0],
            0 => 0,
            _ => ((ulong)((length + key[7] + key[key.Length - 1]) % 256) << 56)
                    + MemoryMarshal.Read<uint>(key.Slice(0, 4))
                    + ((ulong)key[4] << 32)
                    + ((ulong)key[5] << 40)
                    + ((ulong)key[6] << 48),
        };
    }

    /// <summary>
    /// Gets a prime number at least as large as <paramref name="min"/>.
    /// Inlined subset of the primes table from System.Collections.HashHelpers.
    /// </summary>
    private static int GetPrime(int min)
    {
        ReadOnlySpan<int> primes =
        [
            3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197,
            239, 293, 353, 431, 521, 631, 761, 919, 1103, 1327, 1597, 1931,
        ];

        foreach (int prime in primes)
        {
            if (prime >= min)
            {
                return prime;
            }
        }

        // For very large counts (unlikely for JSONPath selectors), fall back to
        // next odd number. Not mathematically prime but adequate for distribution.
        return min | 1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct DispatchEntry
    {
        internal DispatchEntry(ulong hashCode, int next, int slotIndex)
        {
            HashCode = hashCode;
            Next = next;
            SlotIndex = slotIndex;
        }

        internal ulong HashCode { get; }

        internal int Next { get; }

        internal int SlotIndex { get; }
    }
}

// ── Iteration steps ──────────────────────────────────────────────────

/// <summary>
/// Wildcard selector: iterate all children of the current node and execute
/// the body on each.
/// </summary>
internal sealed class WildcardStep : PlanNode
{
    internal WildcardStep(PlanNode body)
    {
        Body = body;
    }

    /// <summary>Gets the plan node to execute on each child.</summary>
    internal PlanNode Body { get; }
}

/// <summary>
/// Array slice selector: iterate a slice of array elements and execute the
/// body on each.
/// </summary>
internal sealed class SliceStep : PlanNode
{
    internal SliceStep(long? start, long? end, long? step, PlanNode body)
    {
        Start = start;
        End = end;
        Step = step;
        Body = body;
    }

    internal long? Start { get; }

    internal long? End { get; }

    internal long? Step { get; }

    /// <summary>Gets the plan node to execute on each slice element.</summary>
    internal PlanNode Body { get; }
}

/// <summary>
/// Filter selector: iterate children, evaluate a filter predicate on each,
/// and execute the body on those that pass.
/// </summary>
internal sealed class FilterStep : PlanNode
{
    internal FilterStep(FilterPlanNode predicate, PlanNode body)
    {
        Predicate = predicate;
        Body = body;
    }

    /// <summary>Gets the filter predicate plan.</summary>
    internal FilterPlanNode Predicate { get; }

    /// <summary>Gets the plan node to execute on each matching child.</summary>
    internal PlanNode Body { get; }
}

/// <summary>
/// Execute multiple selector plan nodes in order on the same input node.
/// Used for union segments with mixed selector types like <c>[0,1,*]</c>
/// or <c>['a', *, 0:2]</c>.
/// <para>
/// For segments where ALL selectors are name selectors, the planner emits
/// <see cref="NameSetStep"/> instead, which fuses the enumeration. This
/// node is the fallback for mixed types where fusion is not possible.
/// </para>
/// </summary>
internal sealed class MultiSelectorStep : PlanNode
{
    internal MultiSelectorStep(PlanNode[] selectors)
    {
        Selectors = selectors;
    }

    /// <summary>Gets the ordered selector plan nodes.</summary>
    internal PlanNode[] Selectors { get; }
}

// ── Descendant steps ─────────────────────────────────────────────────
//
// Descendant traversal uses an explicit frame stack (stackalloc → ArrayPool)
// rather than call recursion to avoid stack overflow on deep JSON.

/// <summary>
/// Descendant traversal optimised for a single name selector:
/// DFS through the JSON tree, checking each property name inline during
/// the object enumeration (no separate TryGetProperty call). This is the
/// common pattern for <c>$..propertyName</c>.
/// </summary>
internal sealed class DescendantNameStep : PlanNode
{
    internal DescendantNameStep(byte[] utf8Name, PlanNode continuation)
    {
        Utf8Name = utf8Name;
        Continuation = continuation;
    }

    /// <summary>Gets the UTF-8 property name to match inline during enumeration.</summary>
    internal byte[] Utf8Name { get; }

    /// <summary>Gets the plan node to execute on each match.</summary>
    internal PlanNode Continuation { get; }
}

/// <summary>
/// Descendant traversal optimised for multiple name selectors:
/// DFS through the JSON tree, at each object node enumerating properties
/// once with hash dispatch to collect all wanted names, then processing
/// results in selector order before recursing into children.
/// <para>
/// For <c>$..['a','b']</c>: at each object node, one enumeration collects
/// both 'a' and 'b', results are emitted in selector order (a then b),
/// then the DFS recurses into all child containers.
/// </para>
/// </summary>
internal sealed class DescendantNameSetStep : PlanNode
{
    internal DescendantNameSetStep(NameSetEntry[] entries, NameDispatchTable dispatch)
    {
        Entries = entries;
        Dispatch = dispatch;
    }

    /// <summary>Gets the entries in selector declaration order.</summary>
    internal NameSetEntry[] Entries { get; }

    /// <summary>Gets the hash dispatch table.</summary>
    internal NameDispatchTable Dispatch { get; }
}

/// <summary>
/// General descendant traversal: DFS through the JSON tree, applying an
/// array of selector plan nodes at each visited node.
/// </summary>
internal sealed class DescendantStep : PlanNode
{
    internal DescendantStep(PlanNode[] selectorPlans)
    {
        SelectorPlans = selectorPlans;
    }

    /// <summary>
    /// Gets the selector plan nodes to execute at each DFS node. Each selector
    /// has its continuation baked in, so a match flows through the downstream
    /// segments automatically.
    /// </summary>
    internal PlanNode[] SelectorPlans { get; }
}
