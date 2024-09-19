// <copyright file="MemberName.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// The name of a member in a scope.
/// </summary>
/// <remarks>
/// This is used by the <see cref="CodeGenerator.GetOrAddMemberName(MemberName)"/>
/// to provide a unique name for a member in a scope, based on the specification in
/// the <see cref="MemberName"/>.
/// </remarks>
public abstract class MemberName(
    string fullyQualifiedScope,
    string baseName,
    Casing casing,
    string? prefix = null,
    string? suffix = null) : IEquatable<MemberName>
{
    /// <summary>
    /// Gets the fully qualified scope in which the name was produced.
    /// </summary>
    public string FullyQualifiedScope { get; } = fullyQualifiedScope;

    /// <summary>
    /// Gets the type of casing for the name.
    /// </summary>
    public Casing Casing { get; } = casing;

    /// <summary>
    /// Gets the invariant base name for the member.
    /// </summary>
    public string BaseNameLowerInvariant { get; } = baseName.ToLowerInvariant();

    /// <summary>
    /// Gets the base name for the member.
    /// </summary>
    public string BaseName => baseName;

    /// <summary>
    /// Gets the prefix for the name.
    /// </summary>
    public string? Prefix { get; } = prefix;

    /// <summary>
    /// Gets the suffix for the name.
    /// </summary>
    public string? Suffix { get; } = suffix;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is MemberName other)
        {
            return this.Equals(other);
        }

        return false;
    }

    /// <inheritdoc/>
    public bool Equals(MemberName? other)
    {
        if (other is null)
        {
            return false;
        }

        return
            this.BaseNameLowerInvariant == other.BaseNameLowerInvariant &&
            this.Casing == other.Casing &&
            this.Prefix == other.Prefix &&
            this.Suffix == other.Suffix &&
            this.FullyQualifiedScope == other.FullyQualifiedScope;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(
            this.BaseNameLowerInvariant,
            this.Casing,
            this.Prefix,
            this.Suffix,
            this.FullyQualifiedScope);
    }

    /// <summary>
    /// Build a name from this naming plan.
    /// </summary>
    /// <returns>The base name built from the member name.</returns>
    public abstract string BuildName();
}