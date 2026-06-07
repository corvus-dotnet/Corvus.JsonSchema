// <copyright file="CompiledInterpolationTemplate.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// A template containing embedded <c>{$expression}</c> fragments, parsed once into literal UTF-8
/// runs and pre-parsed runtime expressions so that interpolation is allocation-free on the hot path.
/// </summary>
/// <remarks>
/// This is the compile-once counterpart to the one-shot
/// <see cref="WorkflowExecutionContext.TryInterpolate(string, out string)"/>: the expensive parse
/// happens in <see cref="Compile"/>; <see cref="WorkflowExecutionContext.TryInterpolate(CompiledInterpolationTemplate, System.Buffers.IBufferWriter{byte})"/>
/// only appends. Code-generated executors bake the equivalent directly.
/// </remarks>
public sealed class CompiledInterpolationTemplate
{
    private readonly Segment[] segments;

    private CompiledInterpolationTemplate(Segment[] segments) => this.segments = segments;

    internal ReadOnlySpan<Segment> Segments => this.segments;

    /// <summary>
    /// Parses a template into its literal and embedded-expression segments.
    /// </summary>
    /// <param name="template">The template (for example <c>"https://{$inputs.host}/api"</c>).</param>
    /// <returns>The compiled template.</returns>
    public static CompiledInterpolationTemplate Compile(string template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var segments = new List<Segment>();
        ReadOnlySpan<char> span = template;
        int i = 0;

        while (i < span.Length)
        {
            if (span[i] == '{' && i + 1 < span.Length && span[i + 1] == '$')
            {
                int relativeClose = span[i..].IndexOf('}');
                if (relativeClose < 0)
                {
                    AddLiteral(segments, span[i..]);
                    break;
                }

                int closeIndex = i + relativeClose;
                segments.Add(new Segment(ArazzoExpression.Parse(span[(i + 1)..closeIndex].ToString())));
                i = closeIndex + 1;
                continue;
            }

            int next = IndexOfEmbeddedStart(span, i);
            if (next < 0)
            {
                AddLiteral(segments, span[i..]);
                break;
            }

            AddLiteral(segments, span[i..next]);
            i = next;
        }

        return new CompiledInterpolationTemplate([.. segments]);
    }

    private static void AddLiteral(List<Segment> segments, ReadOnlySpan<char> literal)
    {
        if (literal.IsEmpty)
        {
            return;
        }

        byte[] bytes = new byte[Encoding.UTF8.GetByteCount(literal)];
        Encoding.UTF8.GetBytes(literal, bytes);
        segments.Add(new Segment(bytes));
    }

    private static int IndexOfEmbeddedStart(ReadOnlySpan<char> span, int from)
    {
        for (int j = from; j < span.Length - 1; j++)
        {
            if (span[j] == '{' && span[j + 1] == '$')
            {
                return j;
            }
        }

        return -1;
    }

    internal readonly struct Segment
    {
        public Segment(byte[] literal)
        {
            this.Literal = literal;
            this.Expression = default;
        }

        public Segment(ArazzoExpression expression)
        {
            this.Literal = null;
            this.Expression = expression;
        }

        public byte[]? Literal { get; }

        public ArazzoExpression Expression { get; }

        public bool IsLiteral => this.Literal is not null;
    }
}