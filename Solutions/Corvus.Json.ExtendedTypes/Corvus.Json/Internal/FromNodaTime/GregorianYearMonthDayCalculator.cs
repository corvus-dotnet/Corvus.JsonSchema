// <copyright file="GregorianYearMonthDayCalculator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable

// Copyright 2013 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

namespace NodaTime.Calendars;

internal static class GregorianYearMonthDayCalculator
{
    internal const int MinGregorianYear = -9998;
    internal const int MaxGregorianYear = 9999;

    // We precompute useful values for each month between these years, as we anticipate most
    // dates will be in this range.
    private const int FirstOptimizedYear = 1900;
    private const int LastOptimizedYear = 2100;
    private const int FirstOptimizedDay = -25567;
    private const int LastOptimizedDay = 47846;

    private const int DaysFrom0000To1970 = 719527;
    private const int AverageDaysPer10Years = 3652; // Ideally 365.2425 per year...

    private static readonly int[] NonLeapDaysPerMonth = { 0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
    private static readonly int[] LeapDaysPerMonth = { 0, 31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

    // Note: these fields must be declared after NonLeapDaysPerMonth and LeapDaysPerMonth so that the initialization
    // is correct. This behavior (textual order for initialization) is guaranteed by the spec. We'd normally
    // try to avoid relying on it, but that's quite hard here.
    private static readonly int[] NonLeapTotalDaysByMonth = GenerateTotalDaysByMonth(NonLeapDaysPerMonth);
    private static readonly int[] LeapTotalDaysByMonth = GenerateTotalDaysByMonth(LeapDaysPerMonth);

    internal static bool TryValidateGregorianYearMonthDay(int year, int month, int day)
    {
        // Perform quick validation without calling Preconditions, then do it properly if we're going to throw
        // an exception. Avoiding the method call is pretty extreme, but it does help.
        if (year < MinGregorianYear || year > MaxGregorianYear || month < 1 || month > 12)
        {
            if (year < MinGregorianYear || year > MaxGregorianYear)
            {
                return false;
            }

            if (month < 1 || month > 12)
            {
                return false;
            }
        }
        // If we've been asked for day 1-28, we're definitely okay regardless of month.
        if (day >= 1 && day <= 28)
        {
            return true;
        }

        int daysInMonth = month == 2 && IsGregorianLeapYear(year) ? LeapDaysPerMonth[month] : NonLeapDaysPerMonth[month];
        if (day < 1 || day > daysInMonth)
        {
            if (day < 1 || day > daysInMonth)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Produces an array with "the sum of the elements of <paramref name="monthLengths"/> before the corresponding index".
    /// So for an input of [0, 1, 2, 3, 4, 5] this would produce [0, 0, 1, 3, 6, 10].
    /// </summary>
    private static int[] GenerateTotalDaysByMonth(int[] monthLengths)
    {
        int[] ret = new int[monthLengths.Length];
        for (int i = 0; i < ret.Length - 1; i++)
        {
            ret[i + 1] = ret[i] + monthLengths[i];
        }
        return ret;
    }

    private static bool IsGregorianLeapYear(int year) => ((year & 3) == 0) && ((year % 100) != 0 || (year % 400) == 0);
}