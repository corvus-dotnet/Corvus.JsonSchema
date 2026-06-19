// <copyright file="AmbientDimensionSet.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The ambient <c>sys:</c> identity dimensions resolved for one request context (design §16.5.5) — the
/// deployment-controlled tags (typically <c>sys:tenant=acme</c>) derived from the request itself rather than the IdP
/// token. An <strong>immutable, reusable</strong> value: an <see cref="IAmbientIdentityDimensions"/> provider builds one
/// instance per configured context at startup and returns that same instance for every matching request, so resolving a
/// request's ambient dimensions allocates nothing.
/// </summary>
/// <remarks>
/// It carries the tags in <strong>both</strong> forms the two stamping moments need: the managed <see cref="SecurityTag"/>
/// list for the string paths (a directory string mapper, <c>ResolveGranteeIdentity</c>, the runtime
/// <c>GetInternalTags</c>), and the pre-encoded UTF-8 key/value pairs for the bytes-to-bytes span path (the directory
/// projector's <see cref="IdentityBuilder"/>), so the span path appends the ambient dimensions without re-encoding a
/// managed <see cref="string"/> per request. The keys must be the <c>sys:</c>-prefixed identity dimensions the provider
/// governs.
/// </remarks>
public sealed class AmbientDimensionSet
{
    /// <summary>The empty set — no ambient dimension resolved for this context (the fail-closed result).</summary>
    public static readonly AmbientDimensionSet Empty = new([], [], []);

    private readonly SecurityTag[] tags;
    private readonly byte[][] keysUtf8;
    private readonly byte[][] valuesUtf8;

    private AmbientDimensionSet(SecurityTag[] tags, byte[][] keysUtf8, byte[][] valuesUtf8)
    {
        this.tags = tags;
        this.keysUtf8 = keysUtf8;
        this.valuesUtf8 = valuesUtf8;
    }

    /// <summary>Gets a value indicating whether no ambient dimension resolved (the fail-closed / unconfigured result).</summary>
    public bool IsEmpty => this.tags.Length == 0;

    /// <summary>Gets the ambient dimensions as managed tags (the string-path form).</summary>
    public IReadOnlyList<SecurityTag> Tags => this.tags;

    /// <summary>
    /// Builds a reusable set from its tags, pre-encoding each key/value to UTF-8 <strong>once</strong> (for the span
    /// path). Call this at startup per configured context, not per request.
    /// </summary>
    /// <param name="tags">The ambient dimensions (<c>sys:</c>-prefixed keys); empty yields <see cref="Empty"/>.</param>
    /// <returns>The reusable set.</returns>
    public static AmbientDimensionSet Create(IReadOnlyList<SecurityTag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        if (tags.Count == 0)
        {
            return Empty;
        }

        var owned = new SecurityTag[tags.Count];
        var keys = new byte[tags.Count][];
        var values = new byte[tags.Count][];
        for (int i = 0; i < tags.Count; i++)
        {
            SecurityTag tag = tags[i];
            ArgumentException.ThrowIfNullOrEmpty(tag.Key);
            owned[i] = tag;
            keys[i] = Encoding.UTF8.GetBytes(tag.Key);
            values[i] = Encoding.UTF8.GetBytes(tag.Value ?? string.Empty);
        }

        return new AmbientDimensionSet(owned, keys, values);
    }

    /// <summary>
    /// Appends the ambient dimensions to an identity being built <strong>bytes to bytes</strong> (the span path): each
    /// pre-encoded UTF-8 key/value is written straight into the pooled buffer through <paramref name="builder"/>, with no
    /// per-request managed <see cref="string"/>.
    /// </summary>
    /// <param name="builder">The identity builder writing into the pooled buffer.</param>
    public void WriteTo(ref IdentityBuilder builder)
    {
        for (int i = 0; i < this.keysUtf8.Length; i++)
        {
            builder.Add(this.keysUtf8[i], this.valuesUtf8[i]);
        }
    }
}