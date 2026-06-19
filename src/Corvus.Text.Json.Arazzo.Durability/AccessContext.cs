// <copyright file="AccessContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>The kind of access a control-plane operation needs against a row (§14.2).</summary>
public enum AccessVerb
{
    /// <summary>Read/observe a row (get, list, search).</summary>
    Read,

    /// <summary>Modify a row (resume, cancel, delete, update).</summary>
    Write,

    /// <summary>Bulk-reap rows (purge).</summary>
    Purge,
}

/// <summary>
/// The caller's resolved row-access grant (design §14.2/§14.4): the reach the principal has for each
/// <see cref="AccessVerb"/>, so read access can be defined independently of write and purge. The control-plane
/// client operations <b>require</b> an instance — there is no contextless/unscoped read on those surfaces, so an
/// unscoped read cannot exist to be misused. <see cref="System"/> is the explicit, named, full-reach grant for
/// the trusted system path (it is a credential, not the absence of one).
/// </summary>
/// <remarks>
/// A <see langword="null"/> reach for a verb means unrestricted for that verb; a non-null reach narrows it. The
/// control plane resolves an instance per request from the authenticated principal (via the deployment's
/// row-security policy); a deployment that configures no row security uses <see cref="System"/> throughout, so
/// behaviour is unchanged.
/// </remarks>
public sealed class AccessContext
{
    /// <summary>The full-reach grant for the trusted system path (no restriction on any verb).</summary>
    public static readonly AccessContext System = new(null, null, null);

    /// <summary>Initializes a new instance of the <see cref="AccessContext"/> class.</summary>
    /// <param name="readReach">The read reach (<see langword="null"/> = unrestricted).</param>
    /// <param name="writeReach">The write reach (<see langword="null"/> = unrestricted).</param>
    /// <param name="purgeReach">The purge reach (<see langword="null"/> = unrestricted).</param>
    public AccessContext(SecurityFilter? readReach, SecurityFilter? writeReach, SecurityFilter? purgeReach)
    {
        this.ReadReach = readReach;
        this.WriteReach = writeReach;
        this.PurgeReach = purgeReach;
    }

    /// <summary>Gets the read reach (<see langword="null"/> = unrestricted).</summary>
    public SecurityFilter? ReadReach { get; }

    /// <summary>Gets the write reach (<see langword="null"/> = unrestricted).</summary>
    public SecurityFilter? WriteReach { get; }

    /// <summary>Gets the purge reach (<see langword="null"/> = unrestricted).</summary>
    public SecurityFilter? PurgeReach { get; }

    /// <summary>Builds a grant whose reach is the same filter for every verb.</summary>
    /// <param name="reach">The reach for read, write, and purge (<see langword="null"/> = unrestricted).</param>
    /// <returns>A uniform access context.</returns>
    public static AccessContext Uniform(SecurityFilter? reach) => new(reach, reach, reach);

    /// <summary>Gets the reach filter for a verb (<see langword="null"/> = unrestricted).</summary>
    /// <param name="verb">The access verb.</param>
    /// <returns>The reach filter for the verb.</returns>
    public SecurityFilter? Reach(AccessVerb verb) => verb switch
    {
        AccessVerb.Read => this.ReadReach,
        AccessVerb.Write => this.WriteReach,
        AccessVerb.Purge => this.PurgeReach,
        _ => throw new ArgumentOutOfRangeException(nameof(verb)),
    };

    /// <summary>Whether a row carrying the given security tags is within reach for a verb.</summary>
    /// <param name="verb">The access verb.</param>
    /// <param name="securityTags">The row's security tags.</param>
    /// <returns><see langword="true"/> if the verb is permitted on the row (or its reach is unrestricted).</returns>
    /// <remarks>An unrestricted reach short-circuits before the holder is materialized; a scoped reach feeds the
    /// evaluator the materialized list at the leaf (the per-row filter cost retained by the design's Q0 decision).</remarks>
    public bool Admits(AccessVerb verb, SecurityTagSet securityTags)
        => this.Reach(verb)?.IsSatisfiedBy(securityTags) ?? true;
}