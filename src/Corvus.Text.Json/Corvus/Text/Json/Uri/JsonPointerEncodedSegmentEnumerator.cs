// <copyright file="JsonPointerEncodedSegmentEnumerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json;

/// <summary>
/// A zero-allocation enumerator that yields the encoded reference tokens (segments) of a JSON Pointer
/// as defined by RFC 6901.
/// </summary>
/// <remarks>
/// <para>
/// Each yielded <see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;byte&gt;</see> is an <b>encoded</b> segment:
/// <c>~0</c> and <c>~1</c> escape sequences are preserved as-is. Callers that need decoded property
/// names must pass each segment through <see cref="Utf8JsonPointer.DecodeSegment(ReadOnlySpan{byte}, Span{byte})"/>.
/// </para>
/// <para>
/// Enumeration semantics:
/// <list type="bullet">
/// <item><description><c>""</c> (empty pointer, root document) — yields zero segments.</description></item>
/// <item><description><c>"/"</c> — yields one empty segment (the property whose name is the empty string).</description></item>
/// <item><description><c>"//"</c> — yields two empty segments.</description></item>
/// <item><description><c>"/a/"</c> — yields segments <c>"a"</c> and <c>""</c>.</description></item>
/// </list>
/// </para>
/// </remarks>
public ref struct JsonPointerEncodedSegmentEnumerator
{
    private readonly ReadOnlySpan<byte> _pointer;
    private int _position;
    private int _segmentStart;
    private int _segmentEnd;
    private bool _started;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPointerEncodedSegmentEnumerator"/> struct.
    /// </summary>
    /// <param name="validatedPointer">A validated JSON Pointer (RFC 6901) as a UTF-8 byte span.</param>
    internal JsonPointerEncodedSegmentEnumerator(ReadOnlySpan<byte> validatedPointer)
    {
        _pointer = validatedPointer;
        _position = 0;
        _segmentStart = 0;
        _segmentEnd = 0;
        _started = false;
    }

    /// <summary>
    /// Gets the current encoded segment.
    /// </summary>
    /// <remarks>
    /// The returned span contains the <b>encoded</b> reference token. Escape sequences (<c>~0</c>, <c>~1</c>)
    /// are preserved. Use <see cref="Utf8JsonPointer.DecodeSegment(ReadOnlySpan{byte}, Span{byte})"/> to decode.
    /// </remarks>
    public ReadOnlySpan<byte> Current => _pointer.Slice(_segmentStart, _segmentEnd - _segmentStart);

    /// <summary>
    /// Returns this instance as its own enumerator, enabling <c>foreach</c> usage.
    /// </summary>
    /// <returns>This enumerator instance.</returns>
    public JsonPointerEncodedSegmentEnumerator GetEnumerator() => this;

    /// <summary>
    /// Advances the enumerator to the next encoded segment.
    /// </summary>
    /// <returns><see langword="true"/> if a segment was found; <see langword="false"/> if enumeration is complete.</returns>
    public bool MoveNext()
    {
        if (!_started)
        {
            _started = true;

            // An empty pointer (root document) has zero segments.
            if (_pointer.Length == 0)
            {
                return false;
            }

            // Skip the leading '/' of the first reference token.
            _position = 1;
        }
        else
        {
            // We are positioned at the '/' separator of the next segment, or at the end.
            if (_position >= _pointer.Length)
            {
                return false;
            }

            // Skip the '/' separator.
            _position++;
        }

        _segmentStart = _position;

        // Scan forward to the next '/' or end of the pointer.
        while (_position < _pointer.Length && _pointer[_position] != (byte)'/')
        {
            _position++;
        }

        _segmentEnd = _position;
        return true;
    }
}