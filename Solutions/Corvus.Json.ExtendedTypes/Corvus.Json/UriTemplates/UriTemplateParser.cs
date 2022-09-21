// <copyright file="UriTemplateParser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Corvus.Json.UriTemplates;

/// <summary>
/// Parses a UriTemplate.
/// </summary>
public static class UriTemplateParser
{
    private const string Varname = "[a-zA-Z0-9_]*";
    private const string Op = "(?<op>[+#./;?&]?)";
    private const string Var = "(?<var>(?:(?<lvar>" + Varname + ")[*]?,?)*)";
    private const string Varspec = "(?<varspec>{" + Op + Var + "})";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    private static readonly Regex FindParam = new(Varspec, RegexOptions.Compiled, DefaultTimeout);
    private static readonly Regex TemplateConversion = new(@"([^{]|^)\?", RegexOptions.Compiled, DefaultTimeout);

    /// <summary>
    /// A callback when a parameter is found.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The string representation of the parameter.</param>
    public delegate void ParameterCallback(ReadOnlySpan<char> name, ReadOnlySpan<char> value);

    /// <summary>
    /// The interface implemented by a URI parser.
    /// </summary>
    public interface IUriParser
    {
        /// <summary>
        /// Parses the given URI, calling your parameter callback for each named parameter discovered.
        /// </summary>
        /// <param name="uri">The URI to parse.</param>
        /// <param name="parameterCallback">Called by the parser for each parameter that is discovered.</param>
        /// <returns><see langword="true"/> if the uri was successfully parsed, otherwise false.</returns>
        /// <remarks>
        /// <para>
        /// This is a low-allocation operation, but you should take care with your implementation of your
        /// <see cref="ParameterCallback"/> if you wish to minimize allocation in your call tree.
        /// </para>
        /// <para>
        /// The parameter callbacks occur as the parameters are matched. If the parse operation ultimately fails,
        /// those parameters are invalid, and should be disregarded.
        /// </para>
        /// </remarks>
        bool ParseUri(ReadOnlySpan<char> uri, ParameterCallback parameterCallback);
    }

    /// <summary>
    /// A pattern element in a URI template.
    /// </summary>
    private interface IUriTemplatePatternElement
    {
        /// <summary>
        /// Non-greedily consume the given segment.
        /// </summary>
        /// <param name="segment">The segment to consume.</param>
        /// <param name="charsConsumed">The number of characters consumed.</param>
        /// <param name="parameterCallback">The callback when a parameter is discovered.</param>
        /// <returns>True if the segment was consumed successfully, otherwise false.</returns>
        bool Consume(ReadOnlySpan<char> segment, out int charsConsumed, ParameterCallback parameterCallback);
    }

    /// <summary>
    /// Create a URI parser for a URI template.
    /// </summary>
    /// <param name="uriTemplate">The URI template for which to create the parser.</param>
    /// <returns>An instance of a parser for the given URI template.</returns>
    /// <remarks>
    /// Note that this operation allocates memory, but <see cref="IUriParser.ParseUri(ReadOnlySpan{char}, ParameterCallback)"/>
    /// is a low-allocation method. Ideally, you should cache the results of calling this method for a given URI template.
    /// </remarks>
    public static IUriParser CreateParser(ReadOnlySpan<char> uriTemplate)
    {
        return new Sequence(CreateParserElements(uriTemplate));
    }

    /// <summary>
    /// Create a URI parser for a URI template.
    /// </summary>
    /// <param name="uriTemplate">The URI template for which to create the parser.</param>
    /// <returns>An instance of a parser for the given URI template.</returns>
    /// <remarks>
    /// Note that this operation allocates memory, but <see cref="IUriParser.ParseUri(ReadOnlySpan{char}, ParameterCallback)"/>
    /// is a low-allocation method. Ideally, you should cache the results of calling this method for a given URI template.
    /// </remarks>
    public static IUriParser CreateParser(string uriTemplate)
    {
        return new Sequence(CreateParserElements(uriTemplate.AsSpan()));
    }

    private static IEnumerable<IUriTemplatePatternElement> CreateParserElements(ReadOnlySpan<char> uriTemplate)
    {
        string template = TemplateConversion.Replace(uriTemplate.ToString(), @"$+\?");
        ReadOnlySpan<char> templateSpan = template.AsSpan();
        List<IUriTemplatePatternElement> elements = new();

        int lastIndex = 0;
        foreach (Match match in FindParam.Matches(template).Cast<Match>())
        {
            if (match.Index > lastIndex)
            {
                // There must be a literal sequence in this gap
                elements.Add(new LiteralSequence(templateSpan[lastIndex..(match.Index - 1)]));
            }

            elements.Add(Match(match));
            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < templateSpan.Length)
        {
            // There must also be a literal sequence at the end
            elements.Add(new LiteralSequence(templateSpan[lastIndex..]));
        }

        return elements;

        static IUriTemplatePatternElement Match(Match m)
        {
            CaptureCollection captures = m.Groups["lvar"].Captures;
            string[] paramNames = ArrayPool<string>.Shared.Rent(captures.Count);
            try
            {
                int written = 0;
                foreach (Capture capture in captures.Cast<Capture>())
                {
                    if (!string.IsNullOrEmpty(capture.Value))
                    {
                        paramNames[written++] = capture.Value;
                    }
                }

                string[] paramNamesArray = paramNames[0..written];

                string op = m.Groups["op"].Value;
                return op switch
                {
                    "?" => new QueryExpressionSequence(paramNamesArray, '?'),
                    "&" => new QueryExpressionSequence(paramNamesArray, '&'),
                    "#" => new ExpressionSequence(paramNamesArray, '#'),
                    "/" => new ExpressionSequence(paramNamesArray, '/'),
                    "+" => new ExpressionSequence(paramNamesArray, '\0'),
                    _ => new ExpressionSequence(paramNamesArray, '\0'),
                };
            }
            finally
            {
                ArrayPool<string>.Shared.Return(paramNames);
            }
        }
    }

    /// <summary>
    /// Represents a sequence of pattern elements.
    /// </summary>
    private sealed class Sequence : IUriTemplatePatternElement, IUriParser
    {
        private readonly ImmutableArray<IUriTemplatePatternElement> elements;

        public Sequence(IEnumerable<IUriTemplatePatternElement> elements)
        {
            this.elements = elements.ToImmutableArray();
        }

        /// <inheritdoc/>
        public bool Consume(ReadOnlySpan<char> segment, out int charsConsumed, ParameterCallback parameterCallback)
        {
            charsConsumed = 0;
            foreach (IUriTemplatePatternElement element in this.elements)
            {
                if (!element.Consume(segment[charsConsumed..], out int localConsumed, parameterCallback))
                {
                    return false;
                }

                charsConsumed += localConsumed;
            }

            return true;
        }

        /// <inheritdoc/>
        public bool ParseUri(ReadOnlySpan<char> uri, ParameterCallback parameterCallback)
        {
            bool result = this.Consume(uri, out int charsConsumed, parameterCallback);

            // We have successfully parsed the uri if all of our elements successfully consumed
            // the contents they were expecting, and we have no characters left over.
            return result && charsConsumed == uri.Length;
        }
    }

    /// <summary>
    /// Represents a literal sequence in a URI template.
    /// </summary>
    private sealed class LiteralSequence : IUriTemplatePatternElement
    {
        private readonly ReadOnlyMemory<char> sequence;

        public LiteralSequence(ReadOnlySpan<char> sequence)
        {
            this.sequence = sequence.ToArray();
        }

        /// <inheritdoc/>
        public bool Consume(ReadOnlySpan<char> segment, out int charsConsumed, ParameterCallback parameterCallback)
        {
            if (segment.StartsWith(this.sequence.Span))
            {
                charsConsumed = this.sequence.Length;
                return true;
            }

            charsConsumed = 0;
            return false;
        }
    }

    /// <summary>
    /// Represents an expression sequence in a URI template.
    /// </summary>
    private sealed class ExpressionSequence : IUriTemplatePatternElement
    {
        private static readonly ReadOnlyMemory<char> FragmentTerminators = ",".AsMemory();
        private static readonly ReadOnlyMemory<char> SlashTerminators = "/?".AsMemory();
        private static readonly ReadOnlyMemory<char> QueryTerminators = "&#".AsMemory();
        private static readonly ReadOnlyMemory<char> SemicolonTerminators = ";/?#".AsMemory();
        private static readonly ReadOnlyMemory<char> DotTerminators = "./?#".AsMemory();
        private static readonly ReadOnlyMemory<char> AllOtherTerminators = "/?&".AsMemory();

        private readonly ReadOnlyMemory<char>[] parameterNames;
        private readonly char prefix;
        private readonly ReadOnlyMemory<char> terminators;

        public ExpressionSequence(string[] parameterNames, char prefix)
        {
            this.parameterNames = parameterNames.Select(s => s.AsMemory()).ToArray();
            this.prefix = prefix;
            this.terminators = GetTerminators(prefix);

            static ReadOnlyMemory<char> GetTerminators(char prefix)
            {
                return prefix switch
                {
                    '#' => FragmentTerminators,
                    '/' => SlashTerminators,
                    '?' or '&' => QueryTerminators,
                    ';' => SemicolonTerminators,
                    '.' => DotTerminators,
                    _ => AllOtherTerminators,
                };
            }
        }

        private enum State
        {
            LookingForPrefix,
            LookingForParams,
        }

        /// <inheritdoc/>
        public bool Consume(ReadOnlySpan<char> segment, out int charsConsumed, ParameterCallback parameterCallback)
        {
            charsConsumed = 0;
            int parameterIndex = 0;
            ReadOnlySpan<char> terminatorsSpan = this.terminators.Span;
            State state = this.prefix != '\0' ? State.LookingForPrefix : State.LookingForParams;
            ReadOnlySpan<char> currentParameterName = this.parameterNames[parameterIndex].Span;
            char currentPrefix = this.prefix;
            bool foundMatches = false;
            while (charsConsumed < segment.Length)
            {
                switch (state)
                {
                    case State.LookingForPrefix:
                        if (segment[charsConsumed] == currentPrefix)
                        {
                            state = State.LookingForParams;
                            charsConsumed++;

                            // If we are a fragment parameter, subsequent
                            // parameters in the sequence use the ','
                            if (currentPrefix == '#')
                            {
                                currentPrefix = ',';
                            }
                        }
                        else
                        {
                            // If we found any matches, then that's good, and
                            // we return our chars consumed.
                            //
                            // On the other hand, if we found no matches before we reached the
                            // end of our search, we say we didn't consume any characters,
                            // but we still matched successfully, because these matches were all optional.
                            if (!foundMatches)
                            {
                                charsConsumed = 0;
                            }

                            return true;
                        }

                        break;

                    case State.LookingForParams:
                        // We found the prefix, so we need to find the next block until the terminator.
                        int segmentStart = charsConsumed + currentParameterName.Length + 1;
                        int segmentEnd = segmentStart;

                        // So we did match the parameter and reach '=' now we are looking ahead to the next terminator, or the end of the segment
                        while (segmentEnd < segment.Length)
                        {
                            char terminator = segment[segmentEnd];
                            if (terminatorsSpan.Contains(terminator))
                            {
                                // Wind back from the terminator, and break out of the while because we've found the end.
                                segmentEnd--;
                                break;
                            }

                            segmentEnd++;
                        }

                        // Tell the world about this parameter (note that the span for the value could be empty).
                        parameterCallback(currentParameterName, segment[segmentStart..segmentEnd]);
                        charsConsumed = segmentEnd + 1;
                        foundMatches = true;

                        // Start looking for the next parameter.
                        parameterIndex++;

                        // We've moved past the last parameter we're looking for.
                        if (parameterIndex >= this.parameterNames.Length)
                        {
                            // We found at least this match!
                            return true;
                        }

                        // Otherwise, start looking for the next parameter
                        currentParameterName = this.parameterNames[parameterIndex].Span;
                        state = this.prefix != '\0' ? State.LookingForPrefix : State.LookingForParams;
                        break;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Represents a query expression sequence in a URI template.
    /// </summary>
    private sealed class QueryExpressionSequence : IUriTemplatePatternElement
    {
        private readonly ReadOnlyMemory<char>[] parameterNames;
        private readonly char prefix;

        public QueryExpressionSequence(string[] parameterNames, char prefix)
        {
            this.parameterNames = parameterNames.Select(s => s.AsMemory()).ToArray();
            this.prefix = prefix;
        }

        private enum State
        {
            LookingForPrefix,
            LookingForParams,
        }

        /// <inheritdoc/>
        public bool Consume(ReadOnlySpan<char> segment, out int charsConsumed, ParameterCallback parameterCallback)
        {
            charsConsumed = 0;
            int parameterIndex = 0;
            State state = State.LookingForPrefix;
            ReadOnlySpan<char> currentParameterName = this.parameterNames[parameterIndex].Span;
            char currentPrefix = this.prefix;
            bool foundMatches = false;
            while (charsConsumed < segment.Length)
            {
                switch (state)
                {
                    case State.LookingForPrefix:
                        if (segment[charsConsumed] == currentPrefix)
                        {
                            state = State.LookingForParams;
                            charsConsumed++;

                            // If we are a query parameter, subsequent
                            // parameters in the sequence use the '&'
                            if (currentPrefix == '?')
                            {
                                currentPrefix = '&';
                            }
                        }
                        else
                        {
                            // If we found any matches, then that's good, and
                            // we return our chars consumed.
                            //
                            // On the other hand, if we found no matches before we reached the
                            // end of our search, we say we didn't consume any characters,
                            // but we still matched successfully, because these matches were all optional.
                            if (!foundMatches)
                            {
                                charsConsumed = 0;
                            }

                            return true;
                        }

                        break;

                    case State.LookingForParams:
                        // Now check the rest of the characters
                        if (!segment[charsConsumed..].StartsWith(currentParameterName))
                        {
                            parameterIndex++;

                            // We've moved past the last parameter we're looking for.
                            if (parameterIndex >= this.parameterNames.Length)
                            {
                                // If we found any matches, then that's good, and
                                // we return our chars consumed.
                                //
                                // On the other hand, if we found no matches before we reached the
                                // end of our search, we say we didn't consume any characters,
                                // but we still matched successfully, because these matches were all optional.
                                if (!foundMatches)
                                {
                                    charsConsumed = 0;
                                }
                                else
                                {
                                    // Back up so that we *didn't* consume the prefix character
                                    // as this must be associated with the next segment
                                    charsConsumed--;
                                }

                                return true;
                            }

                            // Go round again, but try the next parameter name.
                            currentParameterName = this.parameterNames[parameterIndex].Span;
                        }
                        else
                        {
                            // We found our name, so let's see if the next character is '='
                            if (segment[charsConsumed + currentParameterName.Length] != '=')
                            {
                                // If the next character wasn't '=' we don't match this segment at all
                                // so something has definitely gone awry! One possible case, for example, is that the
                                // current segment is a parameter that has a longer name, prefixed with our parameter name
                                // e.g. we are a value represented by '&foo=3' and it is '&fooBar=4'
                                parameterIndex++;

                                // We've moved past the last parameter we're looking for.
                                if (parameterIndex >= this.parameterNames.Length)
                                {
                                    // If we found any matches, then that's good, and
                                    // we return our chars consumed.
                                    //
                                    // On the other hand, if we found no matches before we reached the
                                    // end of our search, we say we didn't consume any characters,
                                    // but we still matched successfully, because these matches were all optional.
                                    if (!foundMatches)
                                    {
                                        charsConsumed = 0;
                                    }
                                    else
                                    {
                                        // Back up so that we *didn't* consume the prefix character
                                        // as this must be associated with the next segment
                                        charsConsumed--;
                                    }

                                    return true;
                                }

                                currentParameterName = this.parameterNames[parameterIndex].Span;
                            }
                            else
                            {
                                // The next character was '=' so now let's pick out the value
                                int segmentStart = charsConsumed + currentParameterName.Length + 1;
                                int segmentEnd = segmentStart;

                                // So we did match the parameter and reach '=' now we are looking ahead to the next terminator, or the end of the segment
                                while (segmentEnd < segment.Length)
                                {
                                    char terminator = segment[segmentEnd];
                                    if (terminator == '/' || terminator == '?' || terminator == '&')
                                    {
                                        // Wind back from the terminator, and break out because we've found the end.
                                        segmentEnd--;
                                        break;
                                    }

                                    segmentEnd++;
                                }

                                // Tell the world about this parameter (note that the span for the value could be empty).
                                parameterCallback(currentParameterName, segment[segmentStart..segmentEnd]);
                                charsConsumed = segmentEnd + 1;
                                foundMatches = true;

                                // Start looking for the next parameter.
                                parameterIndex++;

                                // We've moved past the last parameter we're looking for.
                                if (parameterIndex >= this.parameterNames.Length)
                                {
                                    // We found at least this match!
                                    return true;
                                }

                                // Otherwise, start looking for the next parameter
                                currentParameterName = this.parameterNames[parameterIndex].Span;
                                state = State.LookingForPrefix;
                            }
                        }

                        break;
                }
            }

            return true;
        }
    }
}