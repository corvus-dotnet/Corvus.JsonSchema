// <copyright file="StaticAmbientIdentityDimensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// An <see cref="IAmbientIdentityDimensions"/> that resolves the <strong>same</strong> ambient dimensions for every
/// request (design §16.5.5) — the fixed-context provider. Use it for a single-tenant deployment whose tenant is a
/// deployment constant rather than per-request, and in tests/benchmarks that exercise the stamping paths without an
/// <c>HttpContext</c>.
/// </summary>
/// <remarks>
/// The set is built (and its UTF-8 forms pre-encoded) once at construction; <see cref="Resolve"/> returns that same
/// instance, so it allocates nothing per call.
/// </remarks>
public sealed class StaticAmbientIdentityDimensions : IAmbientIdentityDimensions
{
    private readonly AmbientDimensionSet set;
    private readonly string[] governedKeys;

    /// <summary>Initializes a new instance of the <see cref="StaticAmbientIdentityDimensions"/> class.</summary>
    /// <param name="tags">The fixed ambient dimensions (<c>sys:</c>-prefixed keys) every request resolves to; empty for a no-op provider.</param>
    public StaticAmbientIdentityDimensions(IReadOnlyList<SecurityTag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        this.set = AmbientDimensionSet.Create(tags);

        var governed = new HashSet<string>(StringComparer.Ordinal);
        foreach (SecurityTag tag in tags)
        {
            governed.Add(tag.Key);
        }

        this.governedKeys = [.. governed];
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> GovernedKeys => this.governedKeys;

    /// <inheritdoc/>
    public AmbientDimensionSet Resolve() => this.set;
}