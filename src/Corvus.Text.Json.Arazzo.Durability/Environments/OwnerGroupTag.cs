// <copyright file="OwnerGroupTag.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>
/// The internal tag key an environment's owner group is stamped under (ADR 0065), and the reader of that tag's value.
/// Both live here so every surface that asks which tenant owns an environment asks it the same way.
/// </summary>
/// <remarks>
/// <para>
/// The key is the deployment's reserved internal prefix followed by the <c>tenant</c> dimension, so a deployment that
/// configures a prefix of <c>corp:</c> stamps owner groups under <c>corp:tenant</c>. Two surfaces deriving that
/// independently is the defect this type exists to prevent. A surface reading a key the writer does not stamp finds no
/// owner group anywhere, reports every environment as belonging to nobody, and says so silently: the tenancy gate stops
/// counting owner groups it cannot see, and a per-tenant quota collapses onto one shared counter. Neither reports an
/// error, because "no owner group" is a legitimate answer for an environment nobody claims.
/// </para>
/// <para>
/// The owner group is written as a management tag rather than a column because reach in this system is expressed in
/// identity terms throughout (ADR 0016). Reading it is therefore a walk of the tag set rather than a property read, and
/// the walk's cost is why <see cref="Read"/> is documented for the resolution path and <see cref="IsTenantOwned"/> for
/// the scan.
/// </para>
/// </remarks>
public static class OwnerGroupTag
{
    /// <summary>The dimension an owner group is stamped under, after the deployment's reserved internal prefix.</summary>
    /// <remarks>A constant so a caller composing it with a constant prefix still folds to a literal at compile time.</remarks>
    public const string Dimension = "tenant";

    /// <summary>Gets <see cref="Dimension"/> as UTF-8.</summary>
    public static ReadOnlySpan<byte> DimensionUtf8 => "tenant"u8;

    /// <summary>Gets the tag key for a deployment that configures no prefix, which is <see cref="SecurityShell.DefaultInternalPrefix"/>
    /// followed by <see cref="DimensionUtf8"/>.</summary>
    /// <remarks>A literal rather than a built array: with no configured prefix the key cannot differ from this, so there
    /// is nothing to derive and nothing to allocate.</remarks>
    public static ReadOnlySpan<byte> DefaultKeyUtf8 => "sys:tenant"u8;

    /// <summary>Builds the tag key for a deployment's reserved internal prefix.</summary>
    /// <param name="internalTagPrefix">The deployment's reserved internal tag prefix.</param>
    /// <returns>The owner-group tag key as UTF-8.</returns>
    /// <remarks>Built without an intermediate concatenated string. The result is derived from immutable configuration,
    /// so a caller holds it for the life of its configuration rather than rebuilding it per row.</remarks>
    public static byte[] KeyFor(string internalTagPrefix)
    {
        ArgumentNullException.ThrowIfNull(internalTagPrefix);

        int prefixLength = Encoding.UTF8.GetByteCount(internalTagPrefix);
        byte[] key = new byte[prefixLength + DimensionUtf8.Length];
        Encoding.UTF8.GetBytes(internalTagPrefix, key);
        DimensionUtf8.CopyTo(key.AsSpan(prefixLength));
        return key;
    }

    /// <summary>Whether <paramref name="environment"/> carries a non-empty owner group.</summary>
    /// <param name="environment">The environment.</param>
    /// <param name="ownerGroupKeyUtf8">The deployment's owner-group tag key.</param>
    /// <returns><see langword="true"/> if an owner group is stamped and non-empty.</returns>
    /// <remarks>The scan's form of the question. It answers without materializing the value, so a page of environments
    /// can be walked without allocating per row.</remarks>
    public static bool IsTenantOwned(in Environment environment, ReadOnlySpan<byte> ownerGroupKeyUtf8)
    {
        SecurityTagSet.Utf8Enumerator e = environment.ManagementTagsValue.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                if (e.CurrentKey.SequenceEqual(ownerGroupKeyUtf8) && !e.CurrentValue.IsEmpty)
                {
                    return true;
                }
            }
        }
        finally
        {
            e.Dispose();
        }

        return false;
    }

    /// <summary>Reads the owner group <paramref name="environment"/> is stamped with.</summary>
    /// <param name="environment">The environment.</param>
    /// <param name="ownerGroupKeyUtf8">The deployment's owner-group tag key.</param>
    /// <returns>The owner group, or <see langword="null"/> when the environment carries none. An environment carrying
    /// none belongs to nobody the deployment can name, which is a legitimate state and not an error.</returns>
    /// <remarks>
    /// This materializes the value, so it belongs on a resolution path that caches its answer rather than on one that
    /// runs per row. The value is copied inside the enumeration deliberately: <c>CurrentValue</c> may point into the
    /// enumerator's pooled unescape scratch, which <c>Dispose</c> returns to the pool.
    /// </remarks>
    public static string? Read(in Environment environment, ReadOnlySpan<byte> ownerGroupKeyUtf8)
    {
        SecurityTagSet.Utf8Enumerator e = environment.ManagementTagsValue.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                if (e.CurrentKey.SequenceEqual(ownerGroupKeyUtf8) && !e.CurrentValue.IsEmpty)
                {
                    return Encoding.UTF8.GetString(e.CurrentValue);
                }
            }
        }
        finally
        {
            e.Dispose();
        }

        return null;
    }
}