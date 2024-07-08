// <copyright file="JsonReferenceBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;

namespace Corvus.Json;

/// <summary>
/// A decomposed JsonReference to help you build / deconstruct references.
/// </summary>
public readonly ref struct JsonReferenceBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonReferenceBuilder"/> struct.
    /// </summary>
    /// <param name="scheme">The scheme.</param>
    /// <param name="authority">The authority.</param>
    /// <param name="path">The path.</param>
    /// <param name="query">The query.</param>
    /// <param name="fragment">The fragment.</param>
    /// <remarks>
    /// A builder/deconstructor for a JsonReference.
    /// <code>
    /// <![CDATA[
    ///     foo://example.com:8042/over/there?name=ferret#nose
    ///     \_/   \______________/\_________/ \_________/ \__/
    ///      |           |            |            |        |
    ///    scheme     authority     path          query  fragment
    /// ]]>
    /// </code>
    /// </remarks>
    public JsonReferenceBuilder(ReadOnlySpan<char> scheme, ReadOnlySpan<char> authority, ReadOnlySpan<char> path, ReadOnlySpan<char> query, ReadOnlySpan<char> fragment)
    {
        this.Scheme = scheme;
        this.Authority = authority;
        this.Path = path;
        this.Query = query;
        this.Fragment = fragment;
    }

    /// <summary>
    /// Gets the scheme.
    /// </summary>
    public ReadOnlySpan<char> Scheme { get; }

    /// <summary>
    /// Gets a value indicating whether the reference has a scheme component.
    /// </summary>
    public readonly bool HasScheme => this.Scheme.Length > 0;

    /// <summary>
    /// Gets the authority.
    /// </summary>
    public ReadOnlySpan<char> Authority { get; }

    /// <summary>
    /// Gets a value indicating whether the reference has an authority component.
    /// </summary>
    public readonly bool HasAuthority => this.Authority.Length > 0;

    /// <summary>
    /// Gets the path.
    /// </summary>
    public ReadOnlySpan<char> Path { get; }

    /// <summary>
    /// Gets a value indicating whether the reference has a path component.
    /// </summary>
    public readonly bool HasPath => this.Path.Length > 0;

    /// <summary>
    /// Gets the query.
    /// </summary>
    public ReadOnlySpan<char> Query { get; }

    /// <summary>
    /// Gets a value indicating whether the reference has a query component.
    /// </summary>
    public readonly bool HasQuery => this.Query.Length > 0;

    /// <summary>
    /// Gets the fragment.
    /// </summary>
    public ReadOnlySpan<char> Fragment { get; }

    /// <summary>
    /// Gets a value indicating whether the reference has a query component.
    /// </summary>
    public readonly bool HasFragment => this.Fragment.Length > 0;

    /// <summary>
    /// Gets the host.
    /// </summary>
    public readonly ReadOnlySpan<char> Host => this.FindHost();

    /// <summary>
    /// Gets the port.
    /// </summary>
    public readonly ReadOnlySpan<char> Port => this.FindPort();

    /// <summary>
    /// Gets a reference builder from a reference.
    /// </summary>
    /// <param name="reference">The reference from which to create the builder.</param>
    /// <returns>A <see cref="JsonReferenceBuilder"/> initialized from the given string.</returns>
    public static JsonReferenceBuilder From(string reference)
    {
        return new JsonReference(reference).AsBuilder();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.AsReference().ToString();
    }

    /// <summary>
    /// Gets the JsonReference corresponding to this builder.
    /// </summary>
    /// <returns>The <see cref="JsonReference"/> built from this builder.</returns>
    public readonly JsonReference AsReference()
    {
        int totalLength = 0;
        if (this.Scheme.Length > 0)
        {
            // Scheme plus ':'
            totalLength += this.Scheme.Length + 1;
        }

        if (this.Authority.Length > 0)
        {
            // '//' plus authority
            totalLength += this.Authority.Length + 2;
        }

        if (this.Path.Length > 0 && this.Path[0] != '/' && this.Authority.Length > 0)
        {
            totalLength += this.Path.Length + 1;
        }
        else
        {
            totalLength += this.Path.Length;
        }

        if (this.Query.Length > 0)
        {
            // '?' plus query
            totalLength += this.Query.Length + 1;
        }

        if (this.Fragment.Length > 0)
        {
            // '#' plus fragment
            totalLength += this.Fragment.Length + 1;
        }

        if (totalLength == 0)
        {
            return JsonReference.RootFragment;
        }

        var reference = new Memory<char>(new char[totalLength]);
        int index = 0;
        if (this.Scheme.Length > 0)
        {
            this.Scheme.CopyTo(reference.Span.Slice(index, this.Scheme.Length));
            index += this.Scheme.Length;
            reference.Span[index] = ':';
            index++;
        }

        if (this.Authority.Length > 0)
        {
            reference.Span[index] = '/';
            reference.Span[index + 1] = '/';
            index += 2;
            this.Authority.CopyTo(reference.Span.Slice(index, this.Authority.Length));
            index += this.Authority.Length;
        }

        if (this.Path.Length > 0)
        {
            if (this.Path[0] != '/' && this.Authority.Length > 0)
            {
                reference.Span[index] = '/';
                index++;
            }

            this.Path.CopyTo(reference.Span.Slice(index, this.Path.Length));
            index += this.Path.Length;
        }

        if (this.Query.Length > 0)
        {
            reference.Span[index] = '?';
            index++;
            this.Query.CopyTo(reference.Span.Slice(index, this.Query.Length));
            index += this.Query.Length;
        }

        if (this.Fragment.Length > 0)
        {
            reference.Span[index] = '#';
            index++;
            this.Fragment.CopyTo(reference.Span.Slice(index, this.Fragment.Length));
        }

        return new JsonReference(reference);
    }

    private readonly ReadOnlySpan<char> FindHost()
    {
        if (!this.HasAuthority)
        {
            return [];
        }

        int index = 0;
        while (index < this.Authority.Length && this.Authority[index] != ':')
        {
            index++;
        }

        return this.Authority[0..index];
    }

    private readonly ReadOnlySpan<char> FindPort()
    {
        if (!this.HasAuthority)
        {
            return [];
        }

        int index = 0;
        while (index < this.Authority.Length && this.Authority[index] != ':')
        {
            index++;
        }

        if (index == this.Authority.Length)
        {
            return [];
        }

        return this.Authority[(index + 1)..];
    }
}