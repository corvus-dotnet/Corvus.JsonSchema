// <copyright file="EcmaRegexLinearity.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// The proof that a match of a pattern takes time in proportion to the length of the text, which the code generator
/// and the source generator use to leave such a pattern without a match timeout.
/// </summary>
internal static class EcmaRegexLinearity
{
    /// <summary>The most ways the parts of a pattern with no repetition may match in, together.</summary>
    private const int MaxWays = 256;

    /// <summary>The most pairs of ranges two sets are compared by, before they are taken to overlap.</summary>
    private const long MaxComparisons = 1_000_000;

    private enum ItemKind
    {
        /// <summary><c>$</c>.</summary>
        End,

        /// <summary>A repetition of one character or set that can iterate.</summary>
        Loop,

        /// <summary>A part with no repetition that can iterate: a character, a set, an alternation or an optional part.</summary>
        Block,
    }

    /// <summary>
    /// Whether a match of a pattern is proved to take time in proportion to the length of the text, so that it needs
    /// no match timeout. A pattern that is not proved so may still be linear, and keeps the timeout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The proof is on the tree of the pattern, and holds for the translation, which writes each node of the kinds
    /// it accepts as a bounded piece of .NET pattern. A pattern is proved linear when:
    /// </para>
    /// <list type="bullet">
    /// <item>it has no backreference, lookaround, word boundary or modifier of the m flag;</item>
    /// <item>every repetition that can iterate repeats one character or one set of characters, so no repetition is
    /// inside another;</item>
    /// <item>the parts with no repetition can match in at most <see cref="MaxWays"/> ways in all;</item>
    /// <item>each alternative at the top that starts with <c>^</c>, which is tried only where the text starts, has
    /// after each repetition either no repetition, or nothing that can start with a character of the repetition's
    /// set (or a rest that can match nothing), so that taking a character back from the repetition fails at once;</item>
    /// <item>each other alternative, which is tried at every place in the text, has at most one repetition, as its
    /// last part.</item>
    /// </list>
    /// </remarks>
    /// <param name="ecmaPattern">The pattern, as the schema has it.</param>
    /// <returns><see langword="true"/> when the pattern is proved linear.</returns>
    internal static bool IsLinear(string ecmaPattern)
    {
        if (ecmaPattern.Length > EcmaRegexLimits.MaxLength)
        {
            return false;
        }

        try
        {
            EcmaRegexNode root;
            try
            {
                root = new EcmaRegexParser(ecmaPattern.AsSpan(), unicode: true).Parse();
            }
            catch (EcmaRegexSyntaxException)
            {
                root = new EcmaRegexParser(ecmaPattern.AsSpan(), unicode: false).Parse();
            }

            return TreeIsLinear(root);
        }
        catch (EcmaRegexSyntaxException)
        {
            return false;
        }
        catch (EcmaRegexPatternTooLargeException)
        {
            return false;
        }
    }

    private static bool TreeIsLinear(EcmaRegexNode root)
    {
        EcmaRegexNode[] branches = root.Kind == EcmaRegexNodeKind.Alt ? root.Subs : [root];
        long ways = 0;
        foreach (EcmaRegexNode branch in branches)
        {
            List<Item> items = [];
            bool anchored = false;
            if (!Flatten(branch, items, ref anchored))
            {
                return false;
            }

            long branchWays = 1;
            foreach (Item item in items)
            {
                branchWays = Math.Min(branchWays * item.Ways, MaxWays + 1);
            }

            ways += branchWays;
            if (ways > MaxWays || !(anchored ? AnchoredIsLinear(items) : UnanchoredIsLinear(items)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Taking a character back from a repetition fails at once when the rest cannot start with a character of
    /// its set; and where the rest has no repetition, trying it again costs the same each time.
    /// </summary>
    private static bool AnchoredIsLinear(List<Item> items)
    {
        for (int k = 0; k < items.Count; k++)
        {
            if (items[k].Kind != ItemKind.Loop)
            {
                continue;
            }

            bool restHasLoop = false;
            for (int j = k + 1; j < items.Count; j++)
            {
                restHasLoop |= items[j].Kind == ItemKind.Loop;
            }

            if (restHasLoop && !RestCannotStartWith(items, k + 1, items[k].First![0]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// An alternative tried at every place has a bounded cost at each, so it may have no repetition but its last
    /// part, which ends the match however much of the text it takes.
    /// </summary>
    private static bool UnanchoredIsLinear(List<Item> items)
    {
        for (int k = 0; k < items.Count; k++)
        {
            if (items[k].Kind == ItemKind.Loop && k != items.Count - 1)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether no way of matching the items from <paramref name="start"/> on starts with a character of
    /// <paramref name="set"/>, or the items can match nothing anywhere (so the match ends there).
    /// </summary>
    private static bool RestCannotStartWith(List<Item> items, int start, int[] set)
    {
        // A rest that can match nothing wherever it starts ends the match at the first try of the repetition,
        // with all it can take.
        bool restMatchesNothing = true;
        for (int j = start; j < items.Count && restMatchesNothing; j++)
        {
            restMatchesNothing = items[j].Kind != ItemKind.End && items[j].Nullable;
        }

        if (restMatchesNothing)
        {
            return true;
        }

        for (int j = start; j < items.Count; j++)
        {
            Item item = items[j];
            if (item.Kind == ItemKind.End)
            {
                // Nothing after $ consumes a character.
                return true;
            }

            foreach (int[] first in item.First!)
            {
                if (Overlaps(first, set))
                {
                    return false;
                }
            }

            if (!item.Nullable)
            {
                return true;
            }
        }

        // Every item of the rest can match nothing and none starts with a character of the set.
        return true;
    }

    /// <summary>Reads a branch into its items, and fails for what the proof does not accept.</summary>
    private static bool Flatten(EcmaRegexNode node, List<Item> items, ref bool anchored)
    {
        switch (node.Kind)
        {
            case EcmaRegexNodeKind.Cat:
                foreach (EcmaRegexNode sub in node.Subs)
                {
                    if (!Flatten(sub, items, ref anchored))
                    {
                        return false;
                    }
                }

                return true;

            case EcmaRegexNodeKind.Group:
                return Flatten(node.Subs[0], items, ref anchored);

            case EcmaRegexNodeKind.Empty:
                return true;

            case EcmaRegexNodeKind.Bol:
                // ^ is accepted only as the first part of an alternative at the top.
                if (anchored || items.Count != 0)
                {
                    return false;
                }

                anchored = true;
                return true;

            case EcmaRegexNodeKind.Eol:
                items.Add(new Item(ItemKind.End, null, nullable: true, ways: 1));
                return true;

            case EcmaRegexNodeKind.Repeat when node.Max != 0 && node.Max != 1:
                if (AtomSet(node.Subs[0]) is not int[] set)
                {
                    return false;
                }

                items.Add(new Item(ItemKind.Loop, [set], nullable: node.Min == 0, ways: 1));
                return true;

            default:
                if (!Block(node, out List<int[]> firsts, out bool nullable, out long ways))
                {
                    return false;
                }

                items.Add(new Item(ItemKind.Block, firsts, nullable, ways));
                return true;
        }
    }

    /// <summary>
    /// Measures a part with no repetition that can iterate: the sets it can start with, whether it can match
    /// nothing, and the number of ways it can match.
    /// </summary>
    private static bool Block(EcmaRegexNode node, out List<int[]> firsts, out bool nullable, out long ways)
    {
        firsts = [];
        nullable = false;
        ways = 1;
        switch (node.Kind)
        {
            case EcmaRegexNodeKind.Char:
                firsts.Add([node.CodePoint, node.CodePoint]);
                return true;

            case EcmaRegexNodeKind.Set:
                firsts.Add(node.Set);
                return true;

            case EcmaRegexNodeKind.Empty:
                nullable = true;
                return true;

            case EcmaRegexNodeKind.Group:
                return Block(node.Subs[0], out firsts, out nullable, out ways);

            case EcmaRegexNodeKind.Repeat when node.Max == 0:
                nullable = true;
                return true;

            case EcmaRegexNodeKind.Repeat when node.Max == 1:
                if (!Block(node.Subs[0], out firsts, out nullable, out ways))
                {
                    return false;
                }

                if (node.Min == 0)
                {
                    nullable = true;
                    ways = Math.Min(ways + 1, MaxWays + 1);
                }

                return true;

            case EcmaRegexNodeKind.Alt:
                ways = 0;
                foreach (EcmaRegexNode sub in node.Subs)
                {
                    if (!Block(sub, out List<int[]> subFirsts, out bool subNullable, out long subWays))
                    {
                        return false;
                    }

                    firsts.AddRange(subFirsts);
                    nullable |= subNullable;
                    ways = Math.Min(ways + subWays, MaxWays + 1);
                }

                return true;

            case EcmaRegexNodeKind.Cat:
                nullable = true;
                foreach (EcmaRegexNode sub in node.Subs)
                {
                    if (!Block(sub, out List<int[]> subFirsts, out bool subNullable, out long subWays))
                    {
                        return false;
                    }

                    if (nullable)
                    {
                        firsts.AddRange(subFirsts);
                    }

                    nullable &= subNullable;
                    ways = Math.Min(ways * subWays, MaxWays + 1);
                }

                return true;

            default:
                // A repetition that can iterate, an anchor, a boundary, a lookaround or a backreference.
                return false;
        }
    }

    /// <summary>The set of a part that matches exactly one character, or null.</summary>
    private static int[]? AtomSet(EcmaRegexNode node)
    {
        return node.Kind switch
        {
            EcmaRegexNodeKind.Char => [node.CodePoint, node.CodePoint],
            EcmaRegexNodeKind.Set => node.Set,
            EcmaRegexNodeKind.Group => AtomSet(node.Subs[0]),
            _ => null,
        };
    }

    /// <summary>Whether two sets of ranges (pairs of first and last) have a member in common.</summary>
    private static bool Overlaps(int[] a, int[] b)
    {
        if ((long)(a.Length / 2) * (b.Length / 2) > MaxComparisons)
        {
            return true;
        }

        for (int x = 0; x + 1 < a.Length; x += 2)
        {
            for (int y = 0; y + 1 < b.Length; y += 2)
            {
                if (a[x] <= b[y + 1] && b[y] <= a[x + 1])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private readonly struct Item(ItemKind kind, List<int[]>? first, bool nullable, long ways)
    {
        public ItemKind Kind { get; } = kind;

        /// <summary>Gets the sets the item can start with: for a loop, its one set.</summary>
        public List<int[]>? First { get; } = first;

        public bool Nullable { get; } = nullable;

        public long Ways { get; } = ways;
    }
}