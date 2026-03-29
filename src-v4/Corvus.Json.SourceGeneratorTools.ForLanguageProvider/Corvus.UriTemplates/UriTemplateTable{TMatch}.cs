// <copyright file="UriTemplateTable{TMatch}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.UriTemplates;

/// <summary>
/// Matches a URI against a table of URI templates and returns a result value.
/// </summary>
/// <typeparam name="TMatch">The type of the value to be matched.</typeparam>
public sealed class UriTemplateTable<TMatch>
{
    private readonly IUriTemplateParser[] parsers;
    private readonly TMatch[] matches;

    private UriTemplateTable(IUriTemplateParser[] parsers, TMatch[] matches)
    {
        this.parsers = parsers;
        this.matches = matches;
    }

    /// <summary>
    /// Gets the number of entries in the table.
    /// </summary>
    public int Length => this.parsers.Length;

#if !NET8_0_OR_GREATER
    /// <summary>
    /// Try to match the uri against the URI templates in the table.
    /// </summary>
    /// <param name="uri">The URI to match.</param>
    /// <param name="match">The matched result.</param>
    /// <param name="requiresRootedMatch">If true, then the template requires a rooted match and will not ignore prefixes. This is more efficient when using a fully-qualified template.</param>
    /// <returns><see langword="true"/> if the URI matched a value in the table.</returns>
    /// <remarks>
    /// <para>
    /// This will find the first match in the table.
    /// </para>
    /// <para>
    /// While the <paramref name="match"/> result is <see cref="IDisposable"/> you need only dispose it if the method returned <see langword="true"/>.
    /// It is, however, safe to dispose in either case.
    /// </para>
    /// </remarks>
    public bool TryMatch(string uri, [MaybeNullWhen(false)] out TemplateMatchResult<TMatch> match, in bool requiresRootedMatch = false)
    {
        return this.TryMatch(uri.AsSpan(), out match, requiresRootedMatch);
    }
#endif

    /// <summary>
    /// Try to match the uri against the URI templates in the table.
    /// </summary>
    /// <param name="uri">The URI to match.</param>
    /// <param name="match">The matched result.</param>
    /// <param name="requiresRootedMatch">If true, then the template requires a rooted match and will not ignore prefixes. This is more efficient when using a fully-qualified template.</param>
    /// <returns><see langword="true"/> if the URI matched a value in the table.</returns>
    /// <remarks>
    /// <para>
    /// This will find the first match in the table.
    /// </para>
    /// <para>
    /// While the <paramref name="match"/> result is <see cref="IDisposable"/> you need only dispose it if the method returned <see langword="true"/>.
    /// It is, however, safe to dispose in either case.
    /// </para>
    /// </remarks>
    public bool TryMatch(ReadOnlySpan<char> uri, [MaybeNullWhen(false)] out TemplateMatchResult<TMatch> match, in bool requiresRootedMatch = false)
    {
        for (int i = 0; i < this.parsers.Length; ++i)
        {
            IUriTemplateParser parser = this.parsers[i];
            if (parser.IsMatch(uri, requiresRootedMatch))
            {
                match = new(this.matches[i], parser);
                return true;
            }
        }

        match = default;
        return false;
    }

    /// <summary>
    /// A builder for a <see cref="UriTemplateTable{TMatch}"/>.
    /// </summary>
    public class Builder
    {
        private readonly List<IUriTemplateParser> parsers;
        private readonly List<TMatch> matches;

        /// <summary>
        /// Initializes a new instance of the <see cref="Builder"/> struct.
        /// </summary>
        internal Builder()
        {
            this.parsers = new();
            this.matches = new();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Builder"/> struct.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the table.</param>
        /// <remarks>
        /// Provide the initial capacity of the table if known. This helps minimize
        /// the overhead of re-allocation.
        /// </remarks>
        internal Builder(int initialCapacity)
        {
            this.parsers = new(initialCapacity);
            this.matches = new(initialCapacity);
        }

        /// <summary>
        /// Gets the length of the table builder.
        /// </summary>
        public int Count => this.parsers.Count;

        /// <summary>
        /// Add a uri template and its corresponding match.
        /// </summary>
        /// <param name="uriTemplate">The URI template to add.</param>
        /// <param name="match">The corresponding match to provide if the parser matches.</param>
        public void Add(string uriTemplate, TMatch match)
        {
            this.parsers.Add(UriTemplateParserFactory.CreateParser(uriTemplate));
            this.matches.Add(match);
        }

        /// <summary>
        /// Add a uri template and its corresponding match.
        /// </summary>
        /// <param name="uriTemplate">The URI template to add.</param>
        /// <param name="match">The corresponding match to provide if the parser matches.</param>
        public void Add(ReadOnlySpan<char> uriTemplate, TMatch match)
        {
            this.parsers.Add(UriTemplateParserFactory.CreateParser(uriTemplate));
            this.matches.Add(match);
        }

        /// <summary>
        /// Add a parser and its corresponding match.
        /// </summary>
        /// <param name="parser">The parser to add.</param>
        /// <param name="match">The corresponding match to provide if the parser matches.</param>
        public void Add(IUriTemplateParser parser, TMatch match)
        {
            this.parsers.Add(parser);
            this.matches.Add(match);
        }

        /// <summary>
        /// Convert the builder into a table.
        /// </summary>
        /// <returns>The resulting table.</returns>
        public UriTemplateTable<TMatch> ToTable()
        {
#pragma warning disable SA1010 // Opening square brackets should be spaced correctly - analyzers not up to date
            return new([.. this.parsers], [.. this.matches]);
#pragma warning restore SA1010 // Opening square brackets should be spaced correctly
        }
    }
}