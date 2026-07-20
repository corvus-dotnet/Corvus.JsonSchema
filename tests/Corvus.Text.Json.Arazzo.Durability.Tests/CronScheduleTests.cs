// <copyright file="CronScheduleTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Tests for <see cref="CronSchedule"/> — a cron/calendar cadence, evaluated DST-correctly in its time zone.</summary>
[TestClass]
public sealed class CronScheduleTests
{
    [TestMethod]
    public void Daily_cron_returns_the_next_occurrence_strictly_after_the_given_instant()
    {
        var schedule = new CronSchedule("0 9 * * *", TimeZoneInfo.Utc);

        // Before the day's occurrence: the same day at 09:00.
        schedule.GetNextOccurrence(new DateTimeOffset(2026, 6, 15, 8, 0, 0, TimeSpan.Zero))
            .ShouldBe(new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero));

        // Exactly on an occurrence: strictly after, so the next day (matches the ISchedule contract).
        schedule.GetNextOccurrence(new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero))
            .ShouldBe(new DateTimeOffset(2026, 6, 16, 9, 0, 0, TimeSpan.Zero));
    }

    [TestMethod]
    public void Weekday_cron_only_fires_on_weekdays()
    {
        var schedule = new CronSchedule("0 9 * * 1-5", TimeZoneInfo.Utc);

        // Walk a fortnight of occurrences: every one is a weekday at 09:00, and the weekend is skipped.
        DateTimeOffset cursor = new(2026, 6, 13, 0, 0, 0, TimeSpan.Zero); // a Saturday
        for (int i = 0; i < 10; i++)
        {
            DateTimeOffset occurrence = schedule.GetNextOccurrence(cursor).ShouldNotBeNull();
            occurrence.Hour.ShouldBe(9);
            occurrence.DayOfWeek.ShouldNotBe(DayOfWeek.Saturday);
            occurrence.DayOfWeek.ShouldNotBe(DayOfWeek.Sunday);
            cursor = occurrence;
        }
    }

    [TestMethod]
    public void Cadence_is_evaluated_in_its_time_zone_across_a_dst_spring_forward()
    {
        // "09:00 daily" in US Eastern. On 2025-03-09 the clocks spring forward (02:00 EST -> 03:00 EDT), so the
        // UTC offset shifts from -05:00 to -04:00: 09:00 local moves from 14:00 UTC to 13:00 UTC. A naive fixed-UTC
        // cron would drift to 08:00 local; a DST-correct one keeps 09:00 local, making that day's gap 23 hours.
        var eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var schedule = new CronSchedule("0 9 * * *", eastern);

        DateTimeOffset before = schedule.GetNextOccurrence(new DateTimeOffset(2025, 3, 8, 0, 0, 0, TimeSpan.Zero)).ShouldNotBeNull();
        DateTimeOffset after = schedule.GetNextOccurrence(before).ShouldNotBeNull();

        before.UtcDateTime.ShouldBe(new DateTime(2025, 3, 8, 14, 0, 0, DateTimeKind.Utc)); // 09:00 EST
        after.UtcDateTime.ShouldBe(new DateTime(2025, 3, 9, 13, 0, 0, DateTimeKind.Utc));  // 09:00 EDT
        (after - before).ShouldBe(TimeSpan.FromHours(23));
    }

    [TestMethod]
    public void A_seconds_field_is_honoured_when_requested()
    {
        var schedule = new CronSchedule("15 0 9 * * *", TimeZoneInfo.Utc, includeSeconds: true);

        schedule.GetNextOccurrence(new DateTimeOffset(2026, 6, 15, 8, 0, 0, TimeSpan.Zero))
            .ShouldBe(new DateTimeOffset(2026, 6, 15, 9, 0, 15, TimeSpan.Zero));
    }

    [TestMethod]
    public void An_invalid_expression_is_rejected_at_construction()
    {
        Should.Throw<Cronos.CronFormatException>(() => new CronSchedule("not a cron", TimeZoneInfo.Utc));
    }
}