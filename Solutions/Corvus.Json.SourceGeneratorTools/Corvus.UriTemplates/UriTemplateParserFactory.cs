// <copyright file="UriTemplateParserFactory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text.RegularExpressions;

namespace Corvus.UriTemplates;

/// <summary>
/// Parses a UriTemplate.
/// </summary>
public static class UriTemplateParserFactory
{
    //// Note that we use Regular Expressions to build parse sequences, not to parse the actual results.
    //// That is done using a zero-allocation model, with callbacks for the parameters we find.
    private const string Varname = "[a-zA-Z0-9_]*";
    private const string Op = "(?<op>[+#./;?&]?)";
    private const string Var = "(?<var>(?:(?<lvar>" + Varname + ")[*]?,?)*)";
    private const string Varspec = "(?<varspec>{" + Op + Var + "})";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    private static readonly Regex FindParam = new(Varspec, RegexOptions.Compiled, DefaultTimeout);
    private static readonly Regex TemplateConversion = new(@"([^{]|^)\?", RegexOptions.Compiled, DefaultTimeout);

    /// <summary>
    /// A pattern element in a URI template.
    /// </summary>
    private interface IUriTemplatePatternElement
    {
        /// <summary>
        /// Non-greedily consume the given segment.
        /// </summary>
        /// <typeparam name="TState">The type of the state from the caller.</typeparam>
        /// <param name="escapedTemplate">The escaped URI template. (Enables us to avoid making copies of parameter names.)</param>
        /// <param name="segment">The segment to consume.</param>
        /// <param name="charsConsumed">The number of characters consumed.</param>
        /// <param name="parameterCallback">The callback when a parameter is discovered.</param>
        /// <param name="tail">The tail for this segment.</param>
        /// <param name="state">The state from the caller.</param>
        /// <returns>True if the segment was consumed successfully, otherwise false.</returns>
        bool Consume<TState>(string escapedTemplate, ReadOnlySpan<char> segment, out int charsConsumed, ParameterCallback<TState>? parameterCallback, ref Consumer tail, ref TState state);

        /// <summary>
        /// Non-greedily consume the given segment.
        /// </summary>
        /// <typeparam name="TState">The type of the state from the caller.</typeparam>
        /// <param name="escapedTemplate">The escaped URI template. (Enables us to avoid making copies of parameter names.)</param>
        /// <param name="segment">The segment to consume.</param>
        /// <param name="segmentOffset">The offset in the original URI at which <paramref name="segment"/> starts.</param>
        /// <param name="charsConsumed">The number of characters consumed.</param>
        /// <param name="parameterCallback">The callback when a parameter is discovered.</param>
        /// <param name="tail">The tail for this segment.</param>
        /// <param name="state">The state from the caller.</param>
        /// <returns>True if the segment was consumed successfully, otherwise false.</returns>
        bool Consume<TState>(string escapedTemplate, ReadOnlySpan<char> segment, int segmentOffset, out int charsConsumed, ParameterCallbackWithRange<TState>? parameterCallback, ref Consumer tail, ref TState state);
    }

    /// <summary>
    /// Create a URI parser for a URI template.
    /// </summary>
    /// <param name="uriTemplate">The URI template for which to create the parser.</param>
    /// <returns>An instance of a parser for the given URI template.</returns>
    /// <remarks>
    /// Note that this operation allocates memory, but <see cref="IUriTemplateParser.ParseUri{TState}(in ReadOnlySpan{char}, ParameterCallback{TState}, ref TState, bool)"/>
    /// is a low-allocation method. Ideally, you should cache the results of calling this method for a given URI template.
    /// </remarks>
    public static IUriTemplateParser CreateParser(ReadOnlySpan<char> uriTemplate)
    {
        (string escapedUriTemplate, IUriTemplatePatternElement[] elements) = CreateParserElements(uriTemplate);
        return new UriParser(escapedUriTemplate, elements);
    }

    /// <summary>
    /// Create a URI parser for a URI template.
    /// </summary>
    /// <param name="uriTemplate">The URI template for which to create the parser.</param>
    /// <returns>An instance of a parser for the given URI template.</returns>
    /// <remarks>
    /// Note that this operation allocates memory, but <see cref="IUriTemplateParser.ParseUri{TState}(in ReadOnlySpan{char}, ParameterCallback{TState}, ref TState, bool)"/>
    /// is a low-allocation method. Ideally, you should cache the results of calling this method for a given URI template.
    /// </remarks>
    public static IUriTemplateParser CreateParser(string uriTemplate)
    {
        (string escapedUriTemplate, IUriTemplatePatternElement[] elements) = CreateParserElements(uriTemplate.AsSpan());
        return new UriParser(escapedUriTemplate, elements);
    }

    private static (string EscapedTemplate, IUriTemplatePatternElement[] Elements) CreateParserElements(ReadOnlySpan<char> uriTemplate)
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
                AddLiteral(templateSpan, elements, lastIndex, match);
            }

            elements.Add(Match(match));
            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < templateSpan.Length)
        {
            // There must also be a literal sequence at the end
            elements.Add(new LiteralSequence(templateSpan[lastIndex..]));
        }

        return (template, elements.ToArray());

        static int UnescapeQuestionMarkInPlace(Span<char> literal)
        {
            int readIndex = 0;
            int writeIndex = 0;

            while (readIndex < literal.Length)
            {
                if (readIndex < (literal.Length - 1) && literal[readIndex] == '\\' && literal[readIndex + 1] == '?')
                {
                    // Skip the escaping slash
                    readIndex++;
                    continue;
                }

                literal[writeIndex] = literal[readIndex];
                ++readIndex;
                ++writeIndex;
            }

            return writeIndex;
        }

        static IUriTemplatePatternElement Match(Match m)
        {
            CaptureCollection captures = m.Groups["lvar"].Captures;
            Range[] paramNameRanges = ArrayPool<Range>.Shared.Rent(captures.Count);
            try
            {
                int written = 0;
                foreach (Capture capture in captures.Cast<Capture>())
                {
                    if (!string.IsNullOrEmpty(capture.Value))
                    {
                        paramNameRanges[written++] = new(capture.Index, capture.Index + capture.Length);
                    }
                }

#if NET8_0_OR_GREATER
                Range[] paramNameRangesArray = paramNameRanges[0..written];
#else
                var paramNameRangesArray = new Range[written];
                paramNameRanges.AsSpan(0, written).CopyTo(paramNameRangesArray);
#endif

                string op = m.Groups["op"].Value;
                return op switch
                {
                    "?" => new QueryExpressionSequence(paramNameRangesArray, '?'),
                    "&" => new QueryExpressionSequence(paramNameRangesArray, '&'),
                    "#" => new ExpressionSequence(paramNameRangesArray, '#'),
                    "/" => new ExpressionSequence(paramNameRangesArray, '/'),
                    "+" => new ExpressionSequence(paramNameRangesArray, '\0'),
                    _ => new ExpressionSequence(paramNameRangesArray, '\0'),
                };
            }
            finally
            {
                ArrayPool<Range>.Shared.Return(paramNameRanges);
            }
        }

        static void AddLiteral(ReadOnlySpan<char> templateSpan, List<IUriTemplatePatternElement> elements, int lastIndex, Match match)
        {
            ReadOnlySpan<char> literal = templateSpan[lastIndex..match.Index];
            Span<char> unescaped = stackalloc char[literal.Length];
            literal.CopyTo(unescaped);
            int written = UnescapeQuestionMarkInPlace(unescaped);
            elements.Add(new LiteralSequence(unescaped[..written]));
        }
    }

    private readonly ref struct Consumer
    {
        private readonly ReadOnlySpan<IUriTemplatePatternElement> elements;
        private readonly int elementsLength;

        public Consumer(in ReadOnlySpan<IUriTemplatePatternElement> elements)
        {
            this.elements = elements;
            this.elementsLength = elements.Length;
        }

        public bool Consume<TState>(string escapedTemplate, bool requiresRootedMatch, in ReadOnlySpan<char> segment, out int charsConsumed, in ParameterCallback<TState>? parameterCallback, ref TState state)
        {
            int segmentLength = segment.Length;
            charsConsumed = 0;

            // First, we attempt to consume, advancing through the span until we reach a match
            // (Recall that na UriTemplate is normally allowed to match the tail of a string - any prefix can be ignored.)
            int consumedBySequence = 0;
            while (charsConsumed < segmentLength && !this.ConsumeCore(escapedTemplate, segment[charsConsumed..], out consumedBySequence, parameterCallback, ref state))
            {
                if (requiresRootedMatch)
                {
                    charsConsumed = segmentLength;
                }
                else
                {
                    // We didn't match at that location, so tell the parameter callback to reset the accumulated parameters,
                    // and advance a character
                    parameterCallback?.Invoke(true, default, default, ref state);
                    charsConsumed += consumedBySequence > 0 ? consumedBySequence : 1;
                }
            }

            if (charsConsumed == segmentLength)
            {
                // We didn't find a match, so we tell the parameter callback to reset the accumulated parameters,
                // and reset the characters consumed.
                parameterCallback?.Invoke(true, default, default, ref state);
                charsConsumed = 0;
                return false;
            }

            charsConsumed += consumedBySequence;
            return true;
        }

        public bool Consume<TState>(string escapedTemplate, bool requiresRootedMatch, in ReadOnlySpan<char> segment, out int charsConsumed, in ParameterCallbackWithRange<TState>? parameterCallback, ref TState state)
        {
            int segmentLength = segment.Length;
            charsConsumed = 0;

            // First, we attempt to consume, advancing through the span until we reach a match
            // (Recall that na UriTemplate is normally allowed to match the tail of a string - any prefix can be ignored.)
            int consumedBySequence = 0;
            while (charsConsumed < segmentLength && !this.ConsumeCore(escapedTemplate, segment[charsConsumed..], charsConsumed, out consumedBySequence, parameterCallback, ref state))
            {
                if (requiresRootedMatch)
                {
                    charsConsumed = segmentLength;
                }
                else
                {
                    // We didn't match at that location, so tell the parameter callback to reset the accumulated parameters,
                    // and advance a character
                    parameterCallback?.Invoke(true, default, default, ref state);
                    charsConsumed += consumedBySequence > 0 ? consumedBySequence : 1;
                }
            }

            if (charsConsumed == segmentLength)
            {
                // We didn't find a match, so we tell the parameter callback to reset the accumulated parameters,
                // and reset the characters consumed.
                parameterCallback?.Invoke(true, default, default, ref state);
                charsConsumed = 0;
                return false;
            }

            charsConsumed += consumedBySequence;
            return true;
        }

        public bool MatchesAsTail<TState>(string escapedTemplate, in ReadOnlySpan<char> segment, out int charsConsumed, ref TState state)
        {
            return this.ConsumeCore(escapedTemplate, segment, out charsConsumed, default(ParameterCallback<TState>), ref state);
        }

        private bool ConsumeCore<TState>(string escapedTemplate, in ReadOnlySpan<char> segment, out int charsConsumed, in ParameterCallback<TState>? parameterCallback, ref TState state)
        {
            charsConsumed = 0;

            for (int i = 0; i < this.elementsLength; ++i)
            {
                Consumer tail = new(this.elementsLength > (i + 1) ? this.elements[(i + 1)..] : default);
                if (!this.elements[i].Consume(escapedTemplate, segment[charsConsumed..], out int localConsumed, parameterCallback, ref tail, ref state))
                {
                    // We ensure that local consumed is set correctly by the target for where we can try again.
                    charsConsumed += localConsumed;
                    return false;
                }

                charsConsumed += localConsumed;
            }

            return true;
        }

        private bool ConsumeCore<TState>(string escapedTemplate, in ReadOnlySpan<char> segment, int segmentOffset, out int charsConsumed, in ParameterCallbackWithRange<TState>? parameterCallback, ref TState state)
        {
            charsConsumed = 0;

            for (int i = 0; i < this.elementsLength; ++i)
            {
                Consumer tail = new(this.elementsLength > (i + 1) ? this.elements[(i + 1)..] : default);
                if (!this.elements[i].Consume(escapedTemplate, segment[charsConsumed..], segmentOffset + charsConsumed, out int localConsumed, parameterCallback, ref tail, ref state))
                {
                    // We ensure that local consumed is set correctly by the target for where we can try again.
                    charsConsumed += localConsumed;
                    return false;
                }

                charsConsumed += localConsumed;
            }

            return true;
        }
    }

    /// <summary>
    /// Parses a uri using a set of <see cref="IUriTemplatePatternElement"/>.
    /// </summary>
    private sealed class UriParser : IUriTemplateParser
    {
        private readonly string escapedTemplate;
        private readonly IUriTemplatePatternElement[] elements;

        public UriParser(string escapedTemplate, in IUriTemplatePatternElement[] elements)
        {
            this.escapedTemplate = escapedTemplate;
            this.elements = elements;
        }

        /// <inheritdoc/>
        public bool IsMatch(in ReadOnlySpan<char> uri, bool requiresRootedMatch = false)
        {
            Consumer sequence = new(this.elements.AsSpan());
            int state = 0;
            bool result = sequence.Consume(this.escapedTemplate, requiresRootedMatch, uri, out int charsConsumed, default(ParameterCallback<int>), ref state);

            // We have successfully parsed the uri if all of our elements successfully consumed
            // the contents they were expecting, and we have no characters left over.
            return result && charsConsumed == uri.Length;
        }

#if !NET8_0_OR_GREATER
        /// <inheritdoc/>
        public bool IsMatch(string uri, bool requiresRootedMatch = false)
        {
            return this.IsMatch(uri.AsSpan(), requiresRootedMatch);
        }

        /// <inheritdoc/>
        public bool ParseUri<TState>(string uri, ParameterCallback<TState> parameterCallback, ref TState state, bool requiresRootedMatch = false)
        {
            return this.ParseUri(uri.AsSpan(), parameterCallback, ref state, requiresRootedMatch);
        }

        /// <inheritdoc/>
        public bool ParseUri<TState>(string uri, ParameterCallbackWithRange<TState> parameterCallback, ref TState state, bool requiresRootedMatch = false)
        {
            return this.ParseUri(uri.AsSpan(), parameterCallback, ref state, requiresRootedMatch);
        }
#endif

        /// <inheritdoc/>
        public bool ParseUri<TState>(
            in ReadOnlySpan<char> uri,
            ParameterCallbackWithRange<TState> parameterCallback,
            ref TState state,
            bool requiresRootedMatch = false)
        {
            Consumer sequence = new(this.elements.AsSpan());
            bool result = sequence.Consume(this.escapedTemplate, requiresRootedMatch, uri, out int charsConsumed, parameterCallback, ref state);

            // We have successfully parsed the uri if all of our elements successfully consumed
            // the contents they were expecting, and we have no characters left over.
            return result && charsConsumed == uri.Length;
        }

        /// <inheritdoc/>
        public bool ParseUri<TState>(in ReadOnlySpan<char> uri, ParameterCallback<TState> parameterCallback, ref TState state, bool requiresRootedMatch = false)
        {
            Consumer sequence = new(this.elements.AsSpan());
            bool result = sequence.Consume(this.escapedTemplate, requiresRootedMatch, uri, out int charsConsumed, parameterCallback, ref state);

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
        private readonly char[] sequence;

        public LiteralSequence(ReadOnlySpan<char> sequence)
        {
            this.sequence = sequence.ToArray();
        }

        /// <inheritdoc/>
        public bool Consume<TState>(string escapedTemplate, ReadOnlySpan<char> segment, out int charsConsumed, ParameterCallback<TState>? parameterCallback, ref Consumer tail, ref TState state)
        {
            return this.Consume(segment, out charsConsumed, ref tail);
        }

        /// <inheritdoc/>
        public bool Consume<TState>(string escapedTemplate, ReadOnlySpan<char> segment, int segmentOffset, out int charsConsumed, ParameterCallbackWithRange<TState>? parameterCallback, ref Consumer tail, ref TState state)
        {
            return this.Consume(segment, out charsConsumed, ref tail);
        }

        private bool Consume(ReadOnlySpan<char> segment, out int charsConsumed, ref Consumer tail)
        {
            int index = segment.IndexOf(this.sequence);
            if (index == 0)
            {
                charsConsumed = this.sequence.Length;
                return true;
            }

            if (index == -1)
            {
                // No point in looking ahead as the literal sequence doesn't appear anywhere.
                charsConsumed = this.sequence.Length;
                return false;
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
#if NET8_0_OR_GREATER
        private static readonly SearchValues<char> FragmentTerminators = SearchValues.Create(",");
        private static readonly SearchValues<char> SlashTerminators = SearchValues.Create("/?");
        private static readonly SearchValues<char> QueryTerminators = SearchValues.Create("&#");
        private static readonly SearchValues<char> SemicolonTerminators = SearchValues.Create(";/?#");
        private static readonly SearchValues<char> DotTerminators = SearchValues.Create("./?#");
        private static readonly SearchValues<char> AllOtherTerminators = SearchValues.Create("/?&");
#else
        private const string FragmentTerminators = ",";
        private const string SlashTerminators = "/?";
        private const string QueryTerminators = "&#";
        private const string SemicolonTerminators = ";/?#";
        private const string DotTerminators = "./?#";
        private const string AllOtherTerminators = "/?&";
#endif
        private readonly Range[] parameterNameRanges;
        private readonly char prefix;

#if NET8_0_OR_GREATER
        private readonly SearchValues<char> terminators;
#else
        private readonly string terminators;
#endif

        public ExpressionSequence(Range[] parameterNameRanges, char prefix)
        {
            this.parameterNameRanges = parameterNameRanges;
            this.prefix = prefix;
            this.terminators = GetTerminators(prefix);

#if NET8_0_OR_GREATER
            static SearchValues<char> GetTerminators(char prefix)
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
#else
            static string GetTerminators(char prefix)
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
#endif
        }

        private enum State
        {
            LookingForPrefix,
            LookingForParams,
        }

        /// <inheritdoc/>
        public bool Consume<TState>(string escapedTemplate, ReadOnlySpan<char> segment, out int charsConsumed, ParameterCallback<TState>? parameterCallback, ref Consumer tail, ref TState callbackState)
        {
            charsConsumed = 0;
            int parameterIndex = 0;
            State state = this.prefix != '\0' ? State.LookingForPrefix : State.LookingForParams;
            Range currentParameterNameRange = this.parameterNameRanges[parameterIndex];
            ReadOnlySpan<char> escapedTemplateSpan = escapedTemplate.AsSpan();
            ReadOnlySpan<char> currentParameterName = escapedTemplateSpan[currentParameterNameRange];
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
                        int segmentStart = charsConsumed;
                        int segmentEnd = segmentStart;

                        // Now we are looking ahead to the next terminator, or the end of the segment
                        while (segmentEnd < segment.Length)
                        {
#if NET8_0_OR_GREATER
                            if (this.terminators.Contains(segment[segmentEnd]))
#else
                            if (this.terminators.IndexOf(segment[segmentEnd]) >= 0)
#endif
                            {
                                // Break out of the while because we've found the end.
                                break;
                            }

                            segmentEnd++;
                        }

                        // Tell the world about this parameter (note that the span for the value could be empty).
                        parameterCallback?.Invoke(false, currentParameterName, segment[segmentStart..segmentEnd], ref callbackState);
                        charsConsumed = segmentEnd;
                        foundMatches = true;

                        // Start looking for the next parameter.
                        parameterIndex++;

                        // We've moved past the last parameter we're looking for.
                        if (parameterIndex >= this.parameterNameRanges.Length)
                        {
                            // We found at least this match!
                            return true;
                        }

                        // If we match the tail (the remaining segments in the match) we don't want to consume the next one.
                        if (tail.MatchesAsTail(escapedTemplate, segment[charsConsumed..], out int tailConsumed, ref callbackState) && (tailConsumed + charsConsumed == segment.Length))
                        {
                            // The tail matches the rest of the segment, so we will ignore our next parameter.
                            return true;
                        }

                        // Otherwise, start looking for the next parameter
                        currentParameterNameRange = this.parameterNameRanges[parameterIndex];
                        currentParameterName = escapedTemplateSpan[currentParameterNameRange];

                        state = this.prefix != '\0' ? State.LookingForPrefix : State.LookingForParams;
                        break;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public bool Consume<TState>(
            string escapedUriTemplate,
            ReadOnlySpan<char> segment,
            int segmentStartOffset,
            out int charsConsumed,
            ParameterCallbackWithRange<TState>? parameterCallback,
            ref Consumer tail,
            ref TState callbackState)
        {
            charsConsumed = 0;
            int parameterIndex = 0;
            State state = this.prefix != '\0' ? State.LookingForPrefix : State.LookingForParams;
            Range currentParameterNameRange = this.parameterNameRanges[parameterIndex];

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
                        int segmentStart = charsConsumed;
                        int segmentEnd = segmentStart;

                        // Now we are looking ahead to the next terminator, or the end of the segment
                        while (segmentEnd < segment.Length)
                        {
#if NET8_0_OR_GREATER
                            if (this.terminators.Contains(segment[segmentEnd]))
#else
                            if (this.terminators.IndexOf(segment[segmentEnd]) >= 0)
#endif
                            {
                                // Break out of the while because we've found the end.
                                break;
                            }

                            segmentEnd++;
                        }

                        // Tell the world about this parameter (note that the span for the value could be empty).
                        parameterCallback?.Invoke(false, new ParameterName(escapedUriTemplate, currentParameterNameRange), new Range(segmentStartOffset + segmentStart, segmentStartOffset + segmentEnd), ref callbackState);
                        charsConsumed = segmentEnd;
                        foundMatches = true;

                        // Start looking for the next parameter.
                        parameterIndex++;

                        // We've moved past the last parameter we're looking for.
                        if (parameterIndex >= this.parameterNameRanges.Length)
                        {
                            // We found at least this match!
                            return true;
                        }

                        // If we match the tail (the remaining segments in the match) we don't want to consume the next one.
                        if (tail.MatchesAsTail(escapedUriTemplate, segment[charsConsumed..], out int tailConsumed, ref callbackState) && (tailConsumed + charsConsumed == segment.Length))
                        {
                            // The tail matches the rest of the segment, so we will ignore our next parameter.
                            return true;
                        }

                        // Otherwise, start looking for the next parameter
                        currentParameterNameRange = this.parameterNameRanges[parameterIndex];

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
#if NET8_0_OR_GREATER
        private static readonly SearchValues<char> Terminators = SearchValues.Create("/?&");
#else
        private const string Terminators = "/?&";
#endif
        private readonly Range[] parameterNameRanges;
        private readonly char prefix;

        public QueryExpressionSequence(Range[] parameterNameRanges, char prefix)
        {
            this.parameterNameRanges = parameterNameRanges;
            this.prefix = prefix;
        }

        private enum State
        {
            LookingForPrefix,
            LookingForParams,
        }

        /// <inheritdoc/>
        public bool Consume<TState>(string escapedTemplate, ReadOnlySpan<char> segment, out int charsConsumed, ParameterCallback<TState>? parameterCallback, ref Consumer tail, ref TState callbackState)
        {
            charsConsumed = 0;
            int parameterIndex = 0;
            State state = State.LookingForPrefix;
            Range currentParameterNameRange = this.parameterNameRanges[parameterIndex];
            ReadOnlySpan<char> escapedTemplateSpan = escapedTemplate.AsSpan();
            ReadOnlySpan<char> currentParameterName = escapedTemplateSpan[currentParameterNameRange];
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
                            if (parameterIndex >= this.parameterNameRanges.Length)
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
                            currentParameterNameRange = this.parameterNameRanges[parameterIndex];
                            currentParameterName = escapedTemplateSpan[currentParameterNameRange];
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
                                if (parameterIndex >= this.parameterNameRanges.Length)
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

                                currentParameterNameRange = this.parameterNameRanges[parameterIndex];
                                currentParameterName = escapedTemplateSpan[currentParameterNameRange];
                            }
                            else
                            {
                                // The next character was '=' so now let's pick out the value
                                int segmentStart = charsConsumed + currentParameterName.Length + 1;
                                int segmentEnd = segmentStart;

                                // So we did match the parameter and reach '=' now we are looking ahead to the next terminator, or the end of the segment
                                while (segmentEnd < segment.Length)
                                {
#if NET8_0_OR_GREATER
                                    if (Terminators.Contains(segment[segmentEnd]))
#else
                                    if (Terminators.IndexOf(segment[segmentEnd]) >= 0)
#endif
                                    {
                                        // Break out because we've found the end.
                                        break;
                                    }

                                    segmentEnd++;
                                }

                                // Tell the world about this parameter (note that the span for the value could be empty).
                                parameterCallback?.Invoke(false, currentParameterName, segment[segmentStart..segmentEnd], ref callbackState);
                                charsConsumed = segmentEnd;
                                foundMatches = true;

                                // Start looking for the next parameter.
                                parameterIndex++;

                                // We've moved past the last parameter we're looking for.
                                if (parameterIndex >= this.parameterNameRanges.Length)
                                {
                                    // We found at least this match!
                                    return true;
                                }

                                // Otherwise, start looking for the next parameter
                                currentParameterNameRange = this.parameterNameRanges[parameterIndex];
                                currentParameterName = escapedTemplateSpan[currentParameterNameRange];

                                state = State.LookingForPrefix;
                            }
                        }

                        break;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public bool Consume<TState>(string escapedTemplate, ReadOnlySpan<char> segment, int segmentOffset, out int charsConsumed, ParameterCallbackWithRange<TState>? parameterCallback, ref Consumer tail, ref TState callbackState)
        {
            charsConsumed = 0;
            int parameterIndex = 0;
            State state = State.LookingForPrefix;
            Range currentParameterNameRange = this.parameterNameRanges[parameterIndex];
            ReadOnlySpan<char> escapedTemplateSpan = escapedTemplate.AsSpan();
            ReadOnlySpan<char> currentParameterName = escapedTemplateSpan[currentParameterNameRange];
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
                            if (parameterIndex >= this.parameterNameRanges.Length)
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
                            currentParameterNameRange = this.parameterNameRanges[parameterIndex];
                            currentParameterName = escapedTemplateSpan[currentParameterNameRange];
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
                                if (parameterIndex >= this.parameterNameRanges.Length)
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

                                currentParameterNameRange = this.parameterNameRanges[parameterIndex];
                                currentParameterName = escapedTemplateSpan[currentParameterNameRange];
                            }
                            else
                            {
                                // The next character was '=' so now let's pick out the value
                                int segmentStart = charsConsumed + currentParameterName.Length + 1;
                                int segmentEnd = segmentStart;

                                // So we did match the parameter and reach '=' now we are looking ahead to the next terminator, or the end of the segment
                                while (segmentEnd < segment.Length)
                                {
#if NET8_0_OR_GREATER
                                    if (Terminators.Contains(segment[segmentEnd]))
#else
                                    if (Terminators.IndexOf(segment[segmentEnd]) >= 0)
#endif
                                    {
                                        // Break out because we've found the end.
                                        break;
                                    }

                                    segmentEnd++;
                                }

                                // Tell the world about this parameter (note that the span for the value could be empty).
                                parameterCallback?.Invoke(false, new ParameterName(escapedTemplate, currentParameterNameRange), new Range(segmentOffset + segmentStart, segmentOffset + segmentEnd), ref callbackState);
                                charsConsumed = segmentEnd;
                                foundMatches = true;

                                // Start looking for the next parameter.
                                parameterIndex++;

                                // We've moved past the last parameter we're looking for.
                                if (parameterIndex >= this.parameterNameRanges.Length)
                                {
                                    // We found at least this match!
                                    return true;
                                }

                                // Otherwise, start looking for the next parameter
                                currentParameterNameRange = this.parameterNameRanges[parameterIndex];
                                currentParameterName = escapedTemplateSpan[currentParameterNameRange];

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