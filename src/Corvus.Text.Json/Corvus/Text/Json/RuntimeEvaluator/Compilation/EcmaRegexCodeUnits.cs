// <copyright file="EcmaRegexCodeUnits.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.CodeGeneration;

namespace Corvus.Text.Json.RuntimeEvaluator.Compilation;

/// <summary>
/// The proof that a pattern valid only without the <c>u</c> flag gives the same answers read by code point as by
/// UTF-16 code unit, so that the runtime evaluator's matchers, which read code points, can match it.
/// </summary>
internal static class EcmaRegexCodeUnits
{
    private const int FirstSurrogate = 0xD800;
    private const int LastSurrogate = 0xDFFF;

    /// <summary>
    /// Whether matching a pattern that is valid only without the <c>u</c> flag by UTF-16 code unit, as ECMA-262 does,
    /// can give a different answer from matching it by code point, for some text. When it cannot, the pattern can be
    /// matched by a matcher that reads code points.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only a character beyond the Basic Multilingual Plane, which is two code units, or a lone surrogate, is read
    /// differently. A positive class that holds no surrogate matches neither unit of such a character, and no such
    /// character as a code point. A complement (the dot, a negated class, <c>\D</c>, <c>\W</c>, <c>\S</c>) that
    /// holds every surrogate matches such a character as a code point, and both its units, as two iterations, where
    /// it is the body of a repetition of no upper count whose least count is 0 or 1, and the strings such a
    /// repetition matches are the same either way. Anywhere else, or as a complement that leaves a surrogate out, a
    /// set with some surrogates and not others, or a surrogate written in the pattern, the reading can change the
    /// answer: <c>^.$</c> matches one such character by code point and not by code unit, and
    /// <c>^[^\ud800-\udfff]+$</c> matches it by code point and not by code unit.
    /// </para>
    /// </remarks>
    /// <param name="ecmaPattern">The pattern, as the schema has it.</param>
    /// <returns><see langword="true"/> unless the answers are proved the same.</returns>
    internal static bool CanChangeTheAnswer(string ecmaPattern)
    {
        if (ecmaPattern.Length > EcmaRegexLimits.MaxLength)
        {
            return true;
        }

        try
        {
            return NodeCanChangeTheAnswer(new EcmaRegexParser(ecmaPattern.AsSpan(), unicode: false).Parse(), inLoop: false);
        }
        catch (EcmaRegexSyntaxException)
        {
            return true;
        }
        catch (EcmaRegexPatternTooLargeException)
        {
            return true;
        }
    }

    /// <summary>Whether reading by code unit can change the answer of the node.</summary>
    /// <param name="node">The node.</param>
    /// <param name="inLoop">Whether the node is the body, or a group around the body, of a repetition of no upper
    /// count whose least count is 0 or 1.</param>
    private static bool NodeCanChangeTheAnswer(EcmaRegexNode node, bool inLoop)
    {
        switch (node.Kind)
        {
            case EcmaRegexNodeKind.Char:
                return node.CodePoint is >= FirstSurrogate and <= LastSurrogate;

            case EcmaRegexNodeKind.Set:
                // A matcher that reads code points reads the set as the pattern writes it. A complement then holds
                // every character beyond the Basic Multilingual Plane, and a positive class none. The code units of
                // such a character are both in a complement that holds every surrogate, and in a positive class
                // that holds none, neither is. A set the parser keeps no written form of is not proved.
                if (node.Items is null)
                {
                    return true;
                }

                int surrogates = Surrogates(node.Set);
                return node.Negated
                    ? !(inLoop && surrogates == LastSurrogate - FirstSurrogate + 1)
                    : surrogates != 0;

            case EcmaRegexNodeKind.Group:
                return NodeCanChangeTheAnswer(node.Subs[0], inLoop);

            case EcmaRegexNodeKind.Repeat:
                return NodeCanChangeTheAnswer(node.Subs[0], node.Max == EcmaRegexNode.Unbounded && node.Min <= 1);

            default:
                foreach (EcmaRegexNode sub in node.Subs)
                {
                    if (NodeCanChangeTheAnswer(sub, inLoop: false))
                    {
                        return true;
                    }
                }

                return false;
        }
    }

    /// <summary>The number of surrogates in a set of ranges.</summary>
    private static int Surrogates(int[] set)
    {
        int count = 0;
        for (int k = 0; k + 1 < set.Length; k += 2)
        {
            int first = Math.Max(set[k], FirstSurrogate);
            int last = Math.Min(set[k + 1], LastSurrogate);
            if (first <= last)
            {
                count += last - first + 1;
            }
        }

        return count;
    }
}