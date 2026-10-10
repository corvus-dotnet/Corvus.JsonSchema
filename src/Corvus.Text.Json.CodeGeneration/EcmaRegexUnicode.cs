// <copyright file="EcmaRegexUnicode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Unicode for ECMA-262 patterns: the sets of the <c>\p{...}</c> escapes, the characters of a group name, and the
/// case folding of a case-insensitive group. The data is <see cref="EcmaRegexUnicodeData"/>, of one Unicode version,
/// so a pattern means the same whichever runtime translates it and whichever runs the translation.
/// </summary>
/// <remarks>
/// A set of code points is an <c>int[]</c> of inclusive ranges, sorted, that neither overlap nor touch. A set is read
/// from its table the first time it is asked for and then kept. Nothing here runs while a string is matched.
/// </remarks>
internal static class EcmaRegexUnicode
{
    /// <summary>The last code point.</summary>
    public const int Max = 0x10FFFF;

    // What a property expression names: nothing, a General_Category value, a binary property, a script by its Script
    // property, or a script by its Script_Extensions property. The index of the name is in the low bits.
    private const int None = -1;
    private const int Category = 1 << 16;
    private const int Binary = 2 << 16;
    private const int Script = 3 << 16;
    private const int ScriptExtensions = 4 << 16;

    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz+/";

    /// <summary>
    /// The characters of the Basic Multilingual Plane whose full uppercase form is more than one character, which
    /// the Canonicalize of a pattern with no flag leaves alone (they are the single characters of SpecialCasing.txt).
    /// </summary>
    private static readonly int[] MultipleUppercase =
    [
        0xDF, 0xDF, 0x149, 0x149, 0x1F0, 0x1F0, 0x390, 0x390, 0x3B0, 0x3B0, 0x587, 0x587, 0x1E96, 0x1E9A, 0x1F50,
        0x1F50, 0x1F52, 0x1F52, 0x1F54, 0x1F54, 0x1F56, 0x1F56, 0x1F80, 0x1FAF, 0x1FB2, 0x1FB4, 0x1FB6, 0x1FB7, 0x1FBC,
        0x1FBC, 0x1FC2, 0x1FC4, 0x1FC6, 0x1FC7, 0x1FCC, 0x1FCC, 0x1FD2, 0x1FD3, 0x1FD6, 0x1FD7, 0x1FE2, 0x1FE4, 0x1FE6,
        0x1FE7, 0x1FF2, 0x1FF4, 0x1FF6, 0x1FF7, 0x1FFC, 0x1FFC, 0xFB00, 0xFB06, 0xFB13, 0xFB17,
    ];

    private static readonly int[]?[] Tables = new int[EcmaRegexUnicodeData.TableCount][];
    private static readonly Dictionary<int, int[]> Categories = [];
    private static readonly object Sync = new();
    private static int[]? idStart;
    private static int[]? idContinue;
    private static Folding? unicodeFolding;
    private static Folding? legacyFolding;

    /// <summary>The set of the first <paramref name="count"/> numbers of <paramref name="pairs"/>, read as inclusive ranges in any order.</summary>
    /// <param name="pairs">The ranges.</param>
    /// <param name="count">How many numbers of <paramref name="pairs"/> to read.</param>
    /// <returns>The set.</returns>
    public static int[] SetOf(int[] pairs, int count)
    {
        int n = count / 2;
        long[] keys = new long[n];
        for (int i = 0; i < n; i++)
        {
            keys[i] = ((long)pairs[2 * i] << 32) | (uint)pairs[(2 * i) + 1];
        }

        Array.Sort(keys);
        int[] merged = new int[count];
        int m = 0;
        foreach (long key in keys)
        {
            int lo = (int)(key >> 32);
            int hi = (int)key;
            if (m > 0 && lo <= merged[m - 1] + 1)
            {
                if (hi > merged[m - 1])
                {
                    merged[m - 1] = hi;
                }

                continue;
            }

            merged[m++] = lo;
            merged[m++] = hi;
        }

        if (m != count)
        {
            Array.Resize(ref merged, m);
        }

        return merged;
    }

    /// <summary>Every code point up to <paramref name="max"/> that is not in the set.</summary>
    /// <param name="set">The set.</param>
    /// <param name="max">The last code point of the universe.</param>
    /// <returns>The complement.</returns>
    public static int[] Complement(int[] set, int max = Max)
    {
        int[] result = new int[set.Length + 2];
        int m = 0;
        int next = 0;
        for (int i = 0; i < set.Length && set[i] <= max; i += 2)
        {
            if (set[i] > next)
            {
                result[m++] = next;
                result[m++] = set[i] - 1;
            }

            next = set[i + 1] + 1;
        }

        if (next <= max)
        {
            result[m++] = next;
            result[m++] = max;
        }

        Array.Resize(ref result, m);
        return result;
    }

    /// <summary>Every code point that is in either set.</summary>
    /// <param name="a">The first set.</param>
    /// <param name="b">The second set.</param>
    /// <returns>The union.</returns>
    public static int[] Union(int[] a, int[] b)
    {
        if (a.Length == 0)
        {
            return b;
        }

        if (b.Length == 0)
        {
            return a;
        }

        int[] pairs = new int[a.Length + b.Length];
        Array.Copy(a, pairs, a.Length);
        Array.Copy(b, 0, pairs, a.Length, b.Length);
        return SetOf(pairs, pairs.Length);
    }

    /// <summary>The code points of the set that lie in the range.</summary>
    /// <param name="set">The set.</param>
    /// <param name="lo">The first code point of the range.</param>
    /// <param name="hi">The last code point of the range.</param>
    /// <returns>The part of the set in the range.</returns>
    public static int[] Slice(int[] set, int lo, int hi)
    {
        int[] result = new int[set.Length];
        int m = 0;
        for (int i = 0; i < set.Length; i += 2)
        {
            int a = Math.Max(set[i], lo);
            int b = Math.Min(set[i + 1], hi);
            if (a <= b)
            {
                result[m++] = a;
                result[m++] = b;
            }
        }

        Array.Resize(ref result, m);
        return result;
    }

    /// <summary>Whether the set holds the code point.</summary>
    /// <param name="set">The set.</param>
    /// <param name="c">The code point.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public static bool Contains(int[] set, int c)
    {
        int lo = 0;
        int hi = set.Length / 2;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (c > set[(2 * mid) + 1])
            {
                lo = mid + 1;
            }
            else if (c < set[2 * mid])
            {
                hi = mid;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the two sets share a code point.</summary>
    /// <param name="a">The first set.</param>
    /// <param name="b">The second set.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    public static bool Intersects(int[] a, int[] b)
    {
        int i = 0;
        int j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (a[i + 1] < b[j])
            {
                i += 2;
            }
            else if (b[j + 1] < a[i])
            {
                j += 2;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the two sets hold the same code points.</summary>
    /// <param name="a">The first set.</param>
    /// <param name="b">The second set.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    public static bool SetEquals(int[] a, int[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Resolves the expression between the braces of <c>\p{...}</c> as ECMA-262 does: a name and a value of
    /// General_Category, Script or Script_Extensions joined by <c>=</c>, or the lone name of a General_Category
    /// value or of a binary property. Names are matched exactly.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="categoryTables">For a General_Category value, the tables it is the union of, one bit to a table, and otherwise 0.</param>
    /// <returns>The set of the property, or <see langword="null"/> when ECMA-262 does not define it.</returns>
    public static int[]? Property(ReadOnlySpan<char> expression, out int categoryTables)
    {
        categoryTables = 0;
        int resolved = Resolve(expression);
        if (resolved == None)
        {
            return null;
        }

        int index = resolved & 0xFFFF;
        switch (resolved & ~0xFFFF)
        {
            case Category:
                categoryTables = EcmaRegexUnicodeData.CategoryTables[index];
                return CategorySet(categoryTables);
            case Binary:
                return Table(EcmaRegexUnicodeData.BinaryTables[index]);
            case Script:
                return Table(EcmaRegexUnicodeData.ScriptTables[index]);
            default:
                return Table(EcmaRegexUnicodeData.ScriptExtensionTables[index]);
        }
    }

    /// <summary>Whether a code point may start a group name: ID_Start, <c>$</c> or <c>_</c>.</summary>
    /// <param name="c">The code point.</param>
    /// <returns><see langword="true"/> when it may.</returns>
    public static bool IsNameStart(int c)
    {
        if (c < 128)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '$' || c == '_';
        }

        idStart ??= Property("ID_Start".AsSpan(), out _);
        return Contains(idStart!, c);
    }

    /// <summary>Whether a code point may continue a group name: ID_Continue, <c>$</c>, U+200C or U+200D.</summary>
    /// <param name="c">The code point.</param>
    /// <returns><see langword="true"/> when it may.</returns>
    public static bool IsNamePart(int c)
    {
        if (c < 128)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '$' || c == '_';
        }

        if (c == 0x200C || c == 0x200D)
        {
            return true;
        }

        idContinue ??= Property("ID_Continue".AsSpan(), out _);
        return Contains(idContinue!, c);
    }

    /// <summary>
    /// The set of every character equivalent to some member of the set, under the Canonicalize of the u flag grammar
    /// (<paramref name="unicode"/>) or of a pattern with no flag.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <param name="unicode">Whether the pattern is read with the u flag grammar.</param>
    /// <returns>The set with every equivalent character added, which is <paramref name="set"/> itself when there is none to add.</returns>
    /// <remarks>
    /// ECMA-262 defines case-insensitive matching through a Canonicalize function: two characters match when they
    /// canonicalize to the same character. With the u flag grammar it is Unicode simple case folding. With no flag it
    /// is the uppercase mapping of one UTF-16 code unit, when that is again one code unit, and it never maps a
    /// character outside ASCII into ASCII.
    /// </remarks>
    public static int[] FoldClosure(int[] set, bool unicode)
    {
        Folding f = GetFolding(unicode);
        int[] extra = new int[16];
        int m = 0;
        for (int i = 0; i < set.Length; i += 2)
        {
            int k = Array.BinarySearch(f.Points, set[i]);
            if (k < 0)
            {
                k = ~k;
            }

            for (; k < f.Points.Length && f.Points[k] <= set[i + 1]; k++)
            {
                foreach (int member in f.Classes[k])
                {
                    if (!Contains(set, member))
                    {
                        if (m + 2 > extra.Length)
                        {
                            Array.Resize(ref extra, extra.Length * 2);
                        }

                        extra[m++] = member;
                        extra[m++] = member;
                    }
                }
            }
        }

        if (m == 0)
        {
            return set;
        }

        Array.Resize(ref extra, m);
        return Union(set, extra);
    }

    /// <summary>The characters that are equivalent to some other character under the Canonicalize of a grammar.</summary>
    /// <param name="unicode">Whether the pattern is read with the u flag grammar.</param>
    /// <returns>The set.</returns>
    public static int[] Cased(bool unicode) => GetFolding(unicode).Set;

    /// <summary>The set of a table of <see cref="EcmaRegexUnicodeData"/>.</summary>
    private static int[] Table(int number)
    {
        lock (Sync)
        {
            int[]? set = Tables[number];
            if (set is null)
            {
                string text = EcmaRegexUnicodeData.Table(number);
                int[] result = new int[text.Length];
                int m = 0;
                int at = 0;
                int next = 0;
                while (at < text.Length)
                {
                    int lo = next + Number(text, ref at);
                    int hi = lo + Number(text, ref at);
                    result[m++] = lo;
                    result[m++] = hi;
                    next = hi + 1;
                }

                Array.Resize(ref result, m);
                set = result;
                Tables[number] = set;
            }

            return set;
        }
    }

    /// <summary>Reads one number of a table's text at <paramref name="at"/>, which it moves past the number.</summary>
    private static int Number(string text, ref int at)
    {
        int value = 0;
        int shift = 0;
        while (true)
        {
            int digit = Alphabet.IndexOf(text[at++]);
            value |= (digit & 31) << shift;
            if (digit < 32)
            {
                return value;
            }

            shift += 5;
        }
    }

    private static int Find(string[] names, ReadOnlySpan<char> name)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (name.SequenceEqual(names[i].AsSpan()))
            {
                return i;
            }
        }

        return -1;
    }

    private static int Resolve(ReadOnlySpan<char> expression)
    {
        int eq = expression.IndexOf('=');
        if (eq < 0)
        {
            int category = Find(EcmaRegexUnicodeData.CategoryNames, expression);
            if (category >= 0)
            {
                return Category | category;
            }

            int binary = Find(EcmaRegexUnicodeData.BinaryNames, expression);
            return binary >= 0 ? Binary | binary : None;
        }

        ReadOnlySpan<char> name = expression.Slice(0, eq);
        ReadOnlySpan<char> value = expression.Slice(eq + 1);
        if (name.SequenceEqual("General_Category".AsSpan()) || name.SequenceEqual("gc".AsSpan()))
        {
            int category = Find(EcmaRegexUnicodeData.CategoryNames, value);
            return category >= 0 ? Category | category : None;
        }

        if (name.SequenceEqual("Script".AsSpan()) || name.SequenceEqual("sc".AsSpan()))
        {
            int script = Find(EcmaRegexUnicodeData.ScriptNames, value);
            return script >= 0 ? Script | script : None;
        }

        if (name.SequenceEqual("Script_Extensions".AsSpan()) || name.SequenceEqual("scx".AsSpan()))
        {
            int script = Find(EcmaRegexUnicodeData.ScriptNames, value);
            return script >= 0 ? ScriptExtensions | script : None;
        }

        return None;
    }

    /// <summary>The union of the General_Category tables whose bits are set.</summary>
    private static int[] CategorySet(int tables)
    {
        if ((tables & (tables - 1)) == 0)
        {
            return Table(TrailingZeros(tables));
        }

        lock (Sync)
        {
            if (!Categories.TryGetValue(tables, out int[]? set))
            {
                int length = 0;
                for (int bits = tables; bits != 0; bits &= bits - 1)
                {
                    length += Table(TrailingZeros(bits)).Length;
                }

                int[] pairs = new int[length];
                int m = 0;
                for (int bits = tables; bits != 0; bits &= bits - 1)
                {
                    int[] part = Table(TrailingZeros(bits));
                    Array.Copy(part, 0, pairs, m, part.Length);
                    m += part.Length;
                }

                set = SetOf(pairs, m);
                Categories[tables] = set;
            }

            return set;
        }
    }

    private static int TrailingZeros(int bits)
    {
        int n = 0;
        while ((bits & 1) == 0)
        {
            bits >>= 1;
            n++;
        }

        return n;
    }

    /// <summary>A case mapping of <see cref="EcmaRegexUnicodeData"/> as runs of start, length, delta and stride.</summary>
    private static int[] Mapping(string text)
    {
        int[] result = new int[text.Length];
        int m = 0;
        int at = 0;
        int next = 0;
        while (at < text.Length)
        {
            int start = next + Number(text, ref at);
            int length = Number(text, ref at) + 1;
            int delta = Number(text, ref at);
            result[m++] = start;
            result[m++] = length;
            result[m++] = (delta & 1) != 0 ? -(delta >> 1) : delta >> 1;
            result[m++] = Number(text, ref at) + 1;
            next = start + length;
        }

        Array.Resize(ref result, m);
        return result;
    }

    /// <summary>What a mapping makes of a code point.</summary>
    private static int Map(int[] runs, int c)
    {
        int lo = 0;
        int hi = runs.Length / 4;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            int start = runs[4 * mid];
            if (c < start)
            {
                hi = mid;
            }
            else if (c >= start + runs[(4 * mid) + 1])
            {
                lo = mid + 1;
            }
            else
            {
                return (c - start) % runs[(4 * mid) + 3] == 0 ? c + runs[(4 * mid) + 2] : c;
            }
        }

        return c;
    }

    private static Folding GetFolding(bool unicode)
    {
        lock (Sync)
        {
            Folding? f = unicode ? unicodeFolding : legacyFolding;
            if (f is not null)
            {
                return f;
            }

            Dictionary<int, List<int>> byKey = [];
            if (unicode)
            {
                int[] runs = Mapping(EcmaRegexUnicodeData.SimpleFolding);
                for (int r = 0; r < runs.Length; r += 4)
                {
                    for (int c = runs[r]; c < runs[r] + runs[r + 1]; c += runs[r + 3])
                    {
                        int key = c + runs[r + 2];
                        Add(byKey, key, key);
                        Add(byKey, key, c);
                    }
                }
            }
            else
            {
                int[] runs = Mapping(EcmaRegexUnicodeData.Uppercase);
                for (int c = 0; c <= 0xFFFF; c++)
                {
                    int upper = Contains(MultipleUppercase, c) ? c : Map(runs, c);
                    if ((c >= 128 && upper < 128) || upper > 0xFFFF)
                    {
                        upper = c;
                    }

                    Add(byKey, upper, c);
                }
            }

            int count = 0;
            foreach (List<int> members in byKey.Values)
            {
                if (members.Count > 1)
                {
                    count += members.Count;
                }
            }

            int[] points = new int[count];
            int m = 0;
            foreach (List<int> members in byKey.Values)
            {
                if (members.Count > 1)
                {
                    members.CopyTo(points, m);
                    m += members.Count;
                }
            }

            Array.Sort(points);
            int[][] classes = new int[count][];
            foreach (List<int> members in byKey.Values)
            {
                if (members.Count > 1)
                {
                    int[] shared = [.. members];
                    foreach (int member in shared)
                    {
                        classes[Array.BinarySearch(points, member)] = shared;
                    }
                }
            }

            int[] pairs = new int[2 * count];
            for (int i = 0; i < count; i++)
            {
                pairs[2 * i] = points[i];
                pairs[(2 * i) + 1] = points[i];
            }

            f = new Folding(points, classes, SetOf(pairs, pairs.Length));
            if (unicode)
            {
                unicodeFolding = f;
            }
            else
            {
                legacyFolding = f;
            }

            return f;
        }
    }

    private static void Add(Dictionary<int, List<int>> byKey, int key, int member)
    {
        if (!byKey.TryGetValue(key, out List<int>? members))
        {
            byKey[key] = [member];
            return;
        }

        if (!members.Contains(member))
        {
            members.Add(member);
        }
    }

    /// <summary>The characters that are equivalent to some other character, each with every member of its class.</summary>
    private sealed class Folding(int[] points, int[][] classes, int[] set)
    {
        /// <summary>Gets the characters, sorted.</summary>
        public int[] Points { get; } = points;

        /// <summary>Gets, for each of <see cref="Points"/>, the members of its class, itself among them.</summary>
        public int[][] Classes { get; } = classes;

        /// <summary>Gets <see cref="Points"/> as a set.</summary>
        public int[] Set { get; } = set;
    }
}