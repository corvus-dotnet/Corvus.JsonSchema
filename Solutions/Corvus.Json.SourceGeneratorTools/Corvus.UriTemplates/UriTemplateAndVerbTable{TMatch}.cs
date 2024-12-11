// <copyright file="UriTemplateAndVerbTable{TMatch}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.UriTemplates;

/// <summary>
/// Matches a URI against a table of URI templates and returns a result value.
/// </summary>
/// <typeparam name="TMatch">The type of the match result.</typeparam>
public class UriTemplateAndVerbTable<TMatch>
{
    private UriTemplateTable<MatchWithVerb<TMatch>> innerTable;

    private UriTemplateAndVerbTable(UriTemplateTable<MatchWithVerb<TMatch>> innerTable)
    {
        this.innerTable = innerTable;
    }

    /// <summary>
    /// Try to match the uri against the URI templates in the table.
    /// </summary>
    /// <param name="uri">The URI to match.</param>
    /// <param name="verb">The verb to match.</param>
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
    public bool TryMatch(ReadOnlySpan<char> uri, ReadOnlySpan<char> verb, [MaybeNullWhen(false)] out TemplateMatchResult<TMatch> match, bool requiresRootedMatch = false)
    {
        if (this.innerTable.TryMatch(uri, out TemplateMatchResult<MatchWithVerb<TMatch>> result, requiresRootedMatch))
        {
            if (result.Result.TryMatch(verb, out TMatch? value))
            {
                match = new TemplateMatchResult<TMatch>(value, result.Parser);
                return true;
            }
        }

        // No result, so return the default.
        match = default;
        return false;
    }

#if !NET8_0_OR_GREATER
    /// <summary>
    /// Try to match the uri against the URI templates in the table.
    /// </summary>
    /// <param name="uri">The URI to match.</param>
    /// <param name="verb">The verb to match.</param>
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
    public bool TryMatch(string uri, string verb, [MaybeNullWhen(false)] out TemplateMatchResult<TMatch> match, bool requiresRootedMatch = false)
    {
        return this.TryMatch(uri.AsSpan(), verb.AsSpan(), out match, requiresRootedMatch);
    }
#endif

    /// <summary>
    /// A factory for creating <see cref="UriTemplateAndVerbTable{TMatch}"/> instances.
    /// </summary>
    public sealed class Builder
    {
        // These are the parsers we have created from strings.
        private Dictionary<string, IUriTemplateParser> createdParsers;
        private Dictionary<IUriTemplateParser, MatchWithVerb<TMatch>.Builder> parsersToVerbBuilders;

        /// <summary>
        /// Initializes a new instance of the <see cref="Builder"/> class.
        /// </summary>
        internal Builder()
        {
            this.createdParsers = new();
            this.parsersToVerbBuilders = new();
        }

        /// <summary>
        /// Add a match for a uriTemplate and verb.
        /// </summary>
        /// <param name="uriTemplate">The URI template for which to add the match.</param>
        /// <param name="verb">The verb for which to add the match.</param>
        /// <param name="match">The match to add for this combination.</param>
        public void Add(ReadOnlySpan<char> uriTemplate, string verb, TMatch match)
        {
            string uriTemplateString = uriTemplate.ToString();
            if (!this.createdParsers.TryGetValue(uriTemplateString, out IUriTemplateParser? value))
            {
                value = UriTemplateParserFactory.CreateParser(uriTemplate);
                this.createdParsers.Add(uriTemplateString, value);
            }

            this.Add(value, verb, match);
        }

        /// <summary>
        /// Add a match for a uriTemplate and verb.
        /// </summary>
        /// <param name="uriTemplate">The URI template for which to add the match.</param>
        /// <param name="verb">The verb for which to add the match.</param>
        /// <param name="match">The match to add for this combination.</param>
        public void Add(string uriTemplate, string verb, TMatch match)
        {
            if (!this.createdParsers.TryGetValue(uriTemplate, out IUriTemplateParser? value))
            {
                value = UriTemplateParserFactory.CreateParser(uriTemplate);
                this.createdParsers.Add(uriTemplate, value);
            }

            this.Add(value, verb, match);
        }

        /// <summary>
        /// Add a match for a uriTemplate and verb.
        /// </summary>
        /// <param name="uriTemplate">The URI template for which to add the match.</param>
        /// <param name="verb">The verb for which to add the match.</param>
        /// <param name="match">The match to add for this combination.</param>
        public void Add(IUriTemplateParser uriTemplate, string verb, TMatch match)
        {
            if (!this.parsersToVerbBuilders.TryGetValue(uriTemplate, out MatchWithVerb<TMatch>.Builder? builder))
            {
                builder = MatchWithVerb.CreateBuilder<TMatch>();
                this.parsersToVerbBuilders.Add(uriTemplate, builder);
            }

            builder.Add(verb, match);
        }

        /// <summary>
        /// Converts the builder into a table.
        /// </summary>
        /// <returns>The <see cref="UriTemplateAndVerbTable{TMatch}"/>.</returns>
        public UriTemplateAndVerbTable<TMatch> ToTable()
        {
            UriTemplateTable<MatchWithVerb<TMatch>>.Builder innerTableBuilder = UriTemplateTable.CreateBuilder<MatchWithVerb<TMatch>>();
            foreach (KeyValuePair<IUriTemplateParser, MatchWithVerb<TMatch>.Builder> item in this.parsersToVerbBuilders)
            {
                innerTableBuilder.Add(item.Key, item.Value.ToMatchWithVerb());
            }

            return new(innerTableBuilder.ToTable());
        }
    }
}