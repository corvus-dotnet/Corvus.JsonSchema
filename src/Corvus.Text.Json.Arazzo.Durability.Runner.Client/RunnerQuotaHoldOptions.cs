// <copyright file="RunnerQuotaHoldOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>
/// How long a runner will hold when the runner API refuses a request against a quota, and how many times (ADR 0065
/// decision 3).
/// </summary>
/// <remarks>
/// <para>
/// A quota refusal is the one non-2xx a runner may wait out rather than fail the advance for. That exemption exists
/// because a chatty but legitimate workflow must not be faulted mid-advance after its external calls have already
/// landed: the work is done, and only the record of it is being refused.
/// </para>
/// <para>
/// The exemption is bounded, and the bound is the whole point. A fabricated refusal is indistinguishable from a real one
/// to a runner, the background renewer keeps the lease alive so the run never fails over or faults, and a quota hold is
/// routine enough to raise no audit event. An unbounded hold would therefore be a silent, targeted stall primitive: the
/// run sits holding external side effects it never checkpointed, and nothing anywhere reports a problem. Past these
/// bounds the advance fails like any other non-2xx, which is loud.
/// </para>
/// </remarks>
public sealed class RunnerQuotaHoldOptions
{
    /// <summary>Gets or sets the total time a single advance may spend held. Defaults to thirty seconds.</summary>
    /// <remarks>Spent across every operation of one advance rather than reset per request, so a server refusing each of
    /// a load, a renewal, and a save cannot multiply the stall by the number of operations an advance happens to make.</remarks>
    public TimeSpan MaximumHold { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets how many times a single advance may be held. Defaults to four.</summary>
    /// <remarks>A separate bound from <see cref="MaximumHold"/> because they fail differently: a server returning a
    /// very short <c>Retry-After</c> repeatedly would spin under a time bound alone, and one returning a single long
    /// wait would stall under an attempt bound alone.</remarks>
    public int MaximumAttempts { get; set; } = 4;

    /// <summary>Gets or sets the longest single wait to honour. Defaults to ten seconds.</summary>
    /// <remarks>
    /// The <c>Retry-After</c> is chosen by the control plane, and ADR 0065 has the runner and the control plane in a
    /// relationship of mutual distrust. An unclamped value lets whoever answers the request park a runner for as long as
    /// they like with a single header, so it is treated as advice and capped here.
    /// </remarks>
    public TimeSpan MaximumSingleWait { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets the wait to use when a refusal carries no usable <c>Retry-After</c>. Defaults to one second.</summary>
    public TimeSpan DefaultWait { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Bounds a server-supplied retry interval to what this runner will honour.</summary>
    /// <param name="retryAfterSeconds">The seconds the refusal asked for, or <see langword="null"/> when it carried none.</param>
    /// <returns>The interval to wait.</returns>
    public TimeSpan BoundWait(long? retryAfterSeconds)
    {
        if (retryAfterSeconds is not { } seconds || seconds <= 0)
        {
            return this.DefaultWait;
        }

        TimeSpan asked = TimeSpan.FromSeconds(seconds);
        return asked > this.MaximumSingleWait ? this.MaximumSingleWait : asked;
    }
}