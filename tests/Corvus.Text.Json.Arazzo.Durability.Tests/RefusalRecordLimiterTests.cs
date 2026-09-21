// <copyright file="RefusalRecordLimiterTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The bound on refusal records (ADR 0070): a subject's probes are appended up to a bound in a window and counted past
/// it, the count is never lost, and neither the chain nor the limiter's own memory can be grown by whoever is probing.
/// </summary>
[TestClass]
public sealed class RefusalRecordLimiterTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void A_subject_is_admitted_up_to_its_bound_and_counted_past_it_and_the_count_is_reported_when_the_window_turns()
    {
        var limiter = new RefusalRecordLimiter(perSubject: 3, window: TimeSpan.FromMinutes(1));

        for (int i = 0; i < 3; i++)
        {
            limiter.Admit("mallory", T0.AddSeconds(i), out long none, out _).ShouldBeTrue();
            none.ShouldBe(0);
        }

        for (int i = 0; i < 5; i++)
        {
            limiter.Admit("mallory", T0.AddSeconds(10 + i), out long none, out _).ShouldBeFalse();
            none.ShouldBe(0);
        }

        // Another subject has its own bound.
        limiter.Admit("alice", T0.AddSeconds(20), out _, out _).ShouldBeTrue();

        // The window turns: the first refusal of the new one is admitted, and carries what the last one suppressed.
        limiter.Admit("mallory", T0.AddSeconds(61), out long suppressed, out string recordedAs).ShouldBeTrue();
        suppressed.ShouldBe(5);
        recordedAs.ShouldBe("mallory");

        // And is reported once.
        limiter.Admit("mallory", T0.AddSeconds(62), out long again, out _).ShouldBeTrue();
        again.ShouldBe(0);
    }

    [TestMethod]
    public void The_sweep_reports_a_flood_whose_subject_never_asks_again_and_forgets_subjects_that_have_gone()
    {
        var limiter = new RefusalRecordLimiter(perSubject: 1, window: TimeSpan.FromMinutes(1));
        limiter.Admit("mallory", T0, out _, out _);
        limiter.Admit("mallory", T0, out _, out _);
        limiter.Admit("mallory", T0, out _, out _);
        limiter.Admit("alice", T0, out _, out _);

        var early = new List<KeyValuePair<string, long>>();
        limiter.Sweep(T0.AddSeconds(30), early);
        early.ShouldBeEmpty();

        var flushed = new List<KeyValuePair<string, long>>();
        limiter.Sweep(T0.AddSeconds(61), flushed);
        flushed.ShouldHaveSingleItem().ShouldBe(new KeyValuePair<string, long>("mallory", 2));

        // Both are forgotten: a later refusal starts a fresh window with nothing carried over.
        limiter.Admit("mallory", T0.AddSeconds(62), out long carried, out _).ShouldBeTrue();
        carried.ShouldBe(0);
        var nothing = new List<KeyValuePair<string, long>>();
        limiter.Sweep(T0.AddSeconds(63), nothing);
        nothing.ShouldBeEmpty();
    }

    [TestMethod]
    public void A_full_table_shares_one_bucket_so_memory_is_fixed_the_bound_holds_and_no_count_is_dropped()
    {
        var limiter = new RefusalRecordLimiter(perSubject: 2, window: TimeSpan.FromMinutes(1), maxSubjects: 3);
        limiter.Admit("a", T0, out _, out _).ShouldBeTrue();
        limiter.Admit("b", T0, out _, out _).ShouldBeTrue();
        limiter.Admit("c", T0, out _, out _).ShouldBeTrue();

        // A caller that names itself anew on every request gets one shared bound, not a fresh one each time.
        int admitted = 0;
        for (int i = 0; i < 50; i++)
        {
            if (limiter.Admit("rotating-" + i, T0.AddSeconds(1), out _, out string recordedAs))
            {
                admitted++;
            }

            recordedAs.ShouldBe(RefusalRecordLimiter.OverflowSubject);
        }

        admitted.ShouldBe(2);

        // The subjects already tracked are unaffected.
        limiter.Admit("a", T0.AddSeconds(2), out _, out string own).ShouldBeTrue();
        own.ShouldBe("a");

        var flushed = new List<KeyValuePair<string, long>>();
        limiter.Sweep(T0.AddSeconds(90), flushed);
        flushed.ShouldHaveSingleItem().ShouldBe(new KeyValuePair<string, long>(RefusalRecordLimiter.OverflowSubject, 48));
    }
}