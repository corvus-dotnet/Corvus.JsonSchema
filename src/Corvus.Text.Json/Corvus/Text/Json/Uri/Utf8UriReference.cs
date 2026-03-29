// <copyright file="Utf8UriReference.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

/// <summary>
/// A UTF-8 URI Reference.
/// </summary>
/// <remarks>
/// <code>
/// <![CDATA[
/// foo:// user@example.com:8042/over/there?name= ferret#nose
/// \_/   \___________________/\_________/ \_________/ \__/
/// |           |                 |           |       |
/// scheme     authority           path       query  fragment
/// \___/\______________/
/// |        |
/// user      host (including port)
/// ]]>
/// </code>
/// </remarks>
public readonly ref struct Utf8UriReference
{
    internal readonly Utf8UriTools.Flags _flags;

    internal readonly Utf8UriOffset _offsets;

    internal readonly ReadOnlySpan<byte> _originalUriReference;

    private Utf8UriReference(ReadOnlySpan<byte> uri)
    {
        _originalUriReference = uri;
        IsValid = Utf8UriTools.ParseUriInfo(_originalUriReference, Utf8UriKind.RelativeOrAbsolute, requireAbsolute: false, allowIri: false, out _offsets, out _flags);
    }

    private Utf8UriReference(ReadOnlyMemory<byte> originalUriReference, Utf8UriOffset offsets, Utf8UriTools.Flags flags)
        : this()
    {
        _offsets = offsets;
        _flags = flags;
        _originalUriReference = originalUriReference.Span;
    }

    internal static Utf8UriReference CreateUriReferenceUnsafe(ReadOnlyMemory<byte> uriReference, Utf8UriOffset offsets, Utf8UriTools.Flags flags)
    {
        return new(uriReference, offsets, flags);
    }

    /// <summary>
    /// Gets the authority component of the reference.
    /// </summary>
    public ReadOnlySpan<byte> Authority => _originalUriReference.Slice(_offsets.User, _offsets.Path - _offsets.User);

    /// <summary>
    /// Gets the fragment component of the reference.
    /// </summary>
    public ReadOnlySpan<byte> Fragment => HasFragment ? _originalUriReference.Slice(_offsets.Fragment + 1, _offsets.End - _offsets.Fragment - 1) : [];

    /// <summary>
    /// Gets a value indicating whether this reference has an authority.
    /// </summary>
    public bool HasAuthority => _offsets.Path - _offsets.User > 0;

    /// <summary>
    /// Gets a value indicating whether this reference has a fragment.
    /// </summary>
    public bool HasFragment => _offsets.End - _offsets.Fragment > 0;

    /// <summary>
    /// Gets a value indicating whether this reference has a host.
    /// </summary>
    public bool HasHost => _offsets.Path - _offsets.Host > 0;

    /// <summary>
    /// Gets a value indicating whether this reference has a path.
    /// </summary>
    public bool HasPath => _offsets.Query - _offsets.Path > 0;

    /// <summary>
    /// Gets a value indicating whether this reference has a port.
    /// </summary>
    public bool HasPort => _offsets.Port > 0 && (_offsets.Path - _offsets.Port > 0);

    /// <summary>
    /// Gets a value indicating whether this reference has a query.
    /// </summary>
    public bool HasQuery => _offsets.Fragment - _offsets.Query > 0;

    /// <summary>
    /// Gets a value indicating whether this reference has a scheme.
    /// </summary>
    public bool HasScheme => _offsets.User - _offsets.Scheme > 0;

    /// <summary>
    /// Gets a value indicating whether this reference has a user.
    /// </summary>
    public bool HasUser => _offsets.Host - _offsets.User > 0;

    /// <summary>
    /// Gets the host component of the reference (includes both host and port).
    /// </summary>
    public ReadOnlySpan<byte> Host => HasPort ? _originalUriReference.Slice(_offsets.Host, _offsets.Port - _offsets.Host) : _originalUriReference.Slice(_offsets.Host, _offsets.Path - _offsets.Host);

    /// <summary>
    /// Gets a value indicating whether this is the default port for the scheme.
    /// </summary>
    public bool IsDefaultPort => (_flags & Utf8UriTools.Flags.NotDefaultPort) == 0;

    /// <summary>
    /// Gets a value indicating whether this is a relative reference.
    /// </summary>
    public bool IsRelative => !HasScheme;

    /// <summary>
    /// Gets a value indicating whether this is a valid reference.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the original string.
    /// </summary>
    public ReadOnlySpan<byte> OriginalUriReference => _originalUriReference;

    /// <summary>
    /// Gets the path component of the reference.
    /// </summary>
    public ReadOnlySpan<byte> Path => HasPath ? _originalUriReference.Slice(_offsets.Path, _offsets.Query - _offsets.Path) : [];

    /// <summary>
    /// Gets the port component of the reference as a byte span.
    /// </summary>
    public ReadOnlySpan<byte> Port => HasPort ? _originalUriReference.Slice(_offsets.Port + 1, _offsets.Path - _offsets.Port - 1) : [];

    /// <summary>
    /// Gets the port value as an integer.
    /// </summary>
    public int PortValue => _offsets.PortValue;

    /// <summary>
    /// Gets the query component of the reference.
    /// </summary>
    public ReadOnlySpan<byte> Query => HasQuery ? _originalUriReference.Slice(_offsets.Query + 1, _offsets.Fragment - _offsets.Query - 1) : [];

    /// <summary>
    /// Gets the scheme component of the reference.
    /// </summary>
    public ReadOnlySpan<byte> Scheme
    {
        get
        {
            if (!HasScheme)
            {
                return [];
            }

            // Distinguish between ':' and ':// '
            if (_originalUriReference[_offsets.User - 1] == '/')
            {
                return _originalUriReference.Slice(_offsets.Scheme, _offsets.User - _offsets.Scheme - 3);
            }

            return _originalUriReference.Slice(_offsets.Scheme, _offsets.User - _offsets.Scheme - 1);
        }
    }

    /// <summary>
    /// Gets the user component of the reference.
    /// </summary>
    public ReadOnlySpan<byte> User => HasUser ? _originalUriReference.Slice(_offsets.User, _offsets.Host - _offsets.User - 1) : [];

    /// <summary>
    /// Creates a new UTF-8 URI Reference from the specified URI bytes.
    /// </summary>
    /// <param name="uri">The URI bytes to create the reference from.</param>
    /// <returns>A new UTF-8 URI Reference.</returns>
    /// <exception cref="ArgumentException">Thrown when the URI is invalid.</exception>
    public static Utf8UriReference CreateUriReference(ReadOnlySpan<byte> uri)
    {
        if (!TryCreateUriReference(uri, out Utf8UriReference reference))
        {
            ThrowHelper.ThrowArgumentException(SR.InvalidJsonReference);
        }

        return reference;
    }

    /// <summary>
    /// Tries to create a new UTF-8 URI Reference from the specified URI bytes.
    /// </summary>
    /// <param name="uri">The URI bytes from which to create the UTF-8 URI from.</param>
    /// <param name="utf8UriReference">When this method returns, contains the created UTF-8 URI reference if successful; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if the UTF-8 URI Reference was created successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryCreateUriReference(ReadOnlySpan<byte> uri, out Utf8UriReference utf8UriReference)
    {
        utf8UriReference = new(uri);
        return utf8UriReference.IsValid;
    }

    /// <summary>
    /// Gets the value as a <see cref="Uri"/>.
    /// </summary>
    /// <returns>The URI representation of the reference.</returns>
    public Uri GetUri()
    {
        return new Uri(JsonReaderHelper.TranscodeHelper(_originalUriReference), UriKind.RelativeOrAbsolute);
    }

    /// <summary>
    /// Gets the URI reference in canonical form for display.
    /// </summary>
    /// <param name="buffer">The buffer into which to write the result in canonical form with the encoded characters decoded for display.</param>
    /// <param name="writtenBytes">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the result was successfully written to the buffer; otherwise, <see langword="false"/>.</returns>
    public bool TryFormatDisplay(Span<byte> buffer, out int writtenBytes)
    {
        return Utf8UriTools.TryFormatDisplay(_originalUriReference, _offsets, _flags, buffer, out writtenBytes);
    }

    /// <summary>
    /// Gets the URI reference in canonical form.
    /// </summary>
    /// <param name="buffer">The buffer into which to write the result in canonical form with reserved characters encoded.</param>
    /// <param name="writtenBytes">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the result was successfully written to the buffer; otherwise, <see langword="false"/>.</returns>
    public bool TryFormatCanonical(Span<byte> buffer, out int writtenBytes)
    {
        return Utf8UriTools.TryFormatCanonical(_originalUriReference, _offsets, _flags, allowIri: false, buffer, out writtenBytes);
    }

    /// <summary>
    /// Applies the given URI reference to the current (base) URI and writes the result to the provided buffer.
    /// It uses the rules of RFC 3986 Section 5.2 to resolve the reference against the base URI, including handling
    /// of relative references and merging of paths as needed.
    /// </summary>
    /// <param name="uriReference">The URI reference to apply.</param>
    /// <param name="buffer">The buffer to which to write the backing for the result. This needs to have a lifetime scoped to that
    /// of the resulting reference.</param>
    /// <param name="result">The resulting URI.</param>
    /// <returns><see langword="true"/> if the result was successfully written and produced a valid URI; otherwise, <see langword="false"/>.</returns>
    public bool TryApply(in Utf8UriReference uriReference, Span<byte> buffer, out Utf8Uri result)
    {
        if (!IsRelative && Utf8UriTools.TryApply(_originalUriReference, _offsets, _flags, uriReference._originalUriReference, uriReference._offsets, uriReference._flags, buffer, out int writtenBytes))
        {
            return Utf8Uri.TryCreateUri(buffer.Slice(0, writtenBytes), out result);
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Applies the given URI reference to the current (base) URI and writes the result to the provided buffer.
    /// It uses the rules of RFC 3986 Section 5.2 to resolve the reference against the base URI, including handling
    /// of relative references and merging of paths as needed.
    /// </summary>
    /// <param name="uri">The URI to apply.</param>
    /// <param name="buffer">The buffer to which to write the backing for the result. This needs to have a lifetime scoped to that
    /// of the resulting reference.</param>
    /// <param name="result">The resulting URI.</param>
    /// <returns><see langword="true"/> if the result was successfully written and produced a valid URI; otherwise, <see langword="false"/>.</returns>
    public bool TryApply(in Utf8Uri uri, Span<byte> buffer, out Utf8Uri result)
    {
        if (!IsRelative && Utf8UriTools.TryApply(_originalUriReference, _offsets, _flags, uri._originalUri, uri._offsets, uri._flags, buffer, out int writtenBytes))
        {
            return Utf8Uri.TryCreateUri(buffer.Slice(0, writtenBytes), out result);
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Returns a string representation of the URI reference in display format.
    /// </summary>
    /// <returns>A string representation of the URI reference.</returns>
    public override string ToString()
    {
        Span<byte> buffer = stackalloc byte[2048];
        if (TryFormatDisplay(buffer, out int written))
        {
            return JsonReaderHelper.GetTextFromUtf8(buffer.Slice(0, written));
        }

        return string.Empty;
    }
}