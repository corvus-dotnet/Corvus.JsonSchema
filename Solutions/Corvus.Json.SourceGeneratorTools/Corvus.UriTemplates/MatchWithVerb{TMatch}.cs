// <copyright file="MatchWithVerb{TMatch}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.UriTemplates;

/// <summary>
/// A match result for <see cref="UriTemplateTable{TMatch}"/> with an additional verb.
/// </summary>
/// <typeparam name="TMatch">The type of the match result.</typeparam>
public sealed class MatchWithVerb<TMatch>
{
    private readonly FrozenDictionary<string, TMatch> verbsToMatches;

    private MatchWithVerb(FrozenDictionary<string, TMatch> verbsToMatches)
    {
        this.verbsToMatches = verbsToMatches;
    }

    /// <summary>
    /// Try to match the given verb.
    /// </summary>
    /// <param name="verb">The verb to match.</param>
    /// <param name="match">The match for the verb.</param>
    /// <returns><see langword="true"/> if the verb was a match, otherwise false.</returns>
    public bool TryMatch(string verb, [MaybeNullWhen(false)] out TMatch match)
    {
        return this.verbsToMatches.TryGetValue(verb, out match);
    }

    /// <summary>
    /// Try to match the given verb.
    /// </summary>
    /// <param name="verb">The verb to match.</param>
    /// <param name="match">The match for the verb.</param>
    /// <returns><see langword="true"/> if the verb was a match, otherwise false.</returns>
    public bool TryMatch(ReadOnlySpan<char> verb, [MaybeNullWhen(false)] out TMatch match)
    {
        foreach (KeyValuePair<string, TMatch> kvp in this.verbsToMatches)
        {
#if NET8_0_OR_GREATER
            if (verb.Equals(kvp.Key, StringComparison.Ordinal))
#else
            if (verb.Equals(kvp.Key.AsSpan(), StringComparison.Ordinal))
#endif
            {
                match = kvp.Value;
                return true;
            }
        }

        match = default;
        return false;
    }

    /// <summary>
    /// A builder for a verb matcher.
    /// </summary>
    public sealed class Builder
    {
        private readonly Dictionary<string, TMatch> verbsToMatches;

        /// <summary>
        /// Initializes a new instance of the <see cref="Builder"/> class.
        /// </summary>
        internal Builder()
        {
            this.verbsToMatches = new Dictionary<string, TMatch>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Builder"/> class.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity for the matcher.</param>
        internal Builder(int initialCapacity)
        {
            this.verbsToMatches = new Dictionary<string, TMatch>(initialCapacity);
        }

        /// <summary>
        /// Add a verb to the matcher.
        /// </summary>
        /// <param name="verb">The verb to add.</param>
        /// <param name="match">The match.</param>
        public void Add(string verb, TMatch match)
        {
            this.verbsToMatches.Add(verb, match);
        }

        /// <summary>
        /// Convert the builder to an instance of a matcher.
        /// </summary>
        /// <returns>The instance of the matcher created from the builder.</returns>
        public MatchWithVerb<TMatch> ToMatchWithVerb()
        {
            return new MatchWithVerb<TMatch>(this.verbsToMatches.ToFrozenDictionary());
        }
    }
}