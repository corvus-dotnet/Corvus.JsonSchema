﻿// <copyright file="YearMonthDayCalculator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable

// Copyright 2013 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using NodaTime.Annotations;
using NodaTime.Utility;
using System.Collections.Generic;

namespace NodaTime.Calendars
{
    /// <summary>
    /// The core of date calculations in Noda Time. This class *only* cares about absolute years, and only
    /// dates - it has no time aspects at all, nor era-related aspects.
    /// </summary>
    internal abstract class YearMonthDayCalculator : IComparer<YearMonthDay>
    {
        /// <summary>
        /// Cache to speed up working out when a particular year starts.
        /// See the <see cref="YearStartCacheEntry"/> documentation and <see cref="GetStartOfYearInDays"/>
        /// for more details.
        /// </summary>
        private readonly YearStartCacheEntry[] yearCache = YearStartCacheEntry.CreateCache();

        internal int MinYear { get; }

        internal int MaxYear { get; }

        internal int DaysAtStartOfYear1 { get; }

        private readonly int averageDaysPer10Years;

        protected YearMonthDayCalculator(int minYear, int maxYear,
            int averageDaysPer10Years, int daysAtStartOfYear1)
        {
            this.MinYear = minYear;
            this.MaxYear = maxYear;
            // We add an extra day to make sure that
            // approximations using days-since-epoch are conservative, to avoid going out of bounds.
            this.averageDaysPer10Years = averageDaysPer10Years + 1;
            this.DaysAtStartOfYear1 = daysAtStartOfYear1;
        }

        #region Abstract methods
        /// <summary>
        /// Returns the number of days from the start of the given year to the start of the given month.
        /// </summary>
        protected abstract int GetDaysFromStartOfYearToStartOfMonth(int year, int month);

        /// <summary>
        /// Compute the start of the given year in days since 1970-01-01 ISO. The year may be outside
        /// the bounds advertised by the calendar, but only by a single year. This method is only
        /// called by <see cref="GetStartOfYearInDays"/> (unless the calendar chooses to call it itself),
        /// so calendars which override that method and don't call the original implementation may leave
        /// this unimplemented (e.g. by throwing an exception if it's ever called).
        /// </summary>
        // TODO(misc): Either hard-code a check that this *is* only called by GetStartOfYearInDays
        // via a Roslyn test, or work out an attribute to indicate that, and write a more general test.
        protected abstract int CalculateStartOfYearDays(int year);
        internal abstract int GetMonthsInYear(int year);
        internal abstract int GetDaysInMonth(int year, int month);
        internal abstract bool IsLeapYear(int year);
        internal abstract YearMonthDay AddMonths(YearMonthDay yearMonthDay, int months);

        internal abstract YearMonthDay GetYearMonthDay(int year, int dayOfYear);

        /// <summary>
        /// Returns the number of days in the given year, which will always be within 1 year of
        /// the valid range for the calculator.
        /// </summary>
        internal abstract int GetDaysInYear(int year);

        /// <summary>
        /// Find the months between <paramref name="start"/> and <paramref name="end"/>.
        /// (If start is earlier than end, the result will be non-negative.)
        /// </summary>
        internal abstract int MonthsBetween(YearMonthDay start, YearMonthDay end);

        /// <summary>
        /// Adjusts the given YearMonthDay to the specified year, potentially adjusting
        /// other fields as required.
        /// </summary>
        internal abstract YearMonthDay SetYear(YearMonthDay yearMonthDay, int year);
        #endregion

        #region Virtual methods (subclasses should check to see whether they could override for performance, or should override for correctness)
        /// <summary>
        /// Computes the days since the Unix epoch at the start of the given year/month/day.
        /// This is the opposite of <see cref="GetYearMonthDay(int)"/>.
        /// This assumes the parameter have been validated previously.
        /// </summary>
        internal virtual int GetDaysSinceEpoch(YearMonthDay yearMonthDay)
        {
            int year = yearMonthDay.Year;
            int startOfYear = GetStartOfYearInDays(year);
            int startOfMonth = startOfYear + GetDaysFromStartOfYearToStartOfMonth(year, yearMonthDay.Month);
            return startOfMonth + yearMonthDay.Day - 1;
        }

        /// <summary>
        /// Fetches the start of the year (in days since 1970-01-01 ISO) from the cache, or calculates
        /// and caches it.
        /// </summary>
        /// <param name="year">The year to fetch the days at the start of. This must be within 1 year of the min/max
        /// range, but can exceed it to make week-year calculations simple.</param>
        internal virtual int GetStartOfYearInDays(int year)
        {
            int cacheIndex = YearStartCacheEntry.GetCacheIndex(year);
            YearStartCacheEntry cacheEntry = yearCache[cacheIndex];
            if (!cacheEntry.IsValidForYear(year))
            {
                int days = CalculateStartOfYearDays(year);
                cacheEntry = new YearStartCacheEntry(year, days);
                yearCache[cacheIndex] = cacheEntry;
            }
            return cacheEntry.StartOfYearDays;
        }

        /// <summary>
        /// Compares two YearMonthDay values according to the rules of this calendar.
        /// The default implementation simply uses a naive comparison of the values,
        /// as this is suitable for most calendars (where the first month of the year is month 1).
        /// </summary>
        /// <remarks>Although the parameters are trusted (as in, they'll be valid in this calendar),
        /// the method being public isn't a problem - this type is never exposed.</remarks>
        public virtual int Compare(YearMonthDay lhs, YearMonthDay rhs) => lhs.CompareTo(rhs);

        #endregion

        #region Concrete methods (convenience methods delegating to virtual/abstract ones primarily)

        /// <summary>
        /// Converts from a YearMonthDay representation to "day of year".
        /// This assumes the parameter have been validated previously.
        /// </summary>
        internal int GetDayOfYear(YearMonthDay yearMonthDay) => GetDaysFromStartOfYearToStartOfMonth(yearMonthDay.Year, yearMonthDay.Month) + yearMonthDay.Day;

        /// <summary>
        /// Works out the year/month/day of a given days-since-epoch by first computing the year and day of year,
        /// then getting the month and day from those two. This is how almost all calendars are naturally implemented
        /// anyway.
        /// </summary>
        internal YearMonthDay GetYearMonthDay(int daysSinceEpoch)
        {
            int year = GetYear(daysSinceEpoch, out int zeroBasedDay);
            return GetYearMonthDay(year, zeroBasedDay + 1);
        }

        /// <summary>
        /// Work out the year from the number of days since the epoch, as well as the
        /// day of that year (0-based).
        /// </summary>
        internal int GetYear(int daysSinceEpoch, out int zeroBasedDayOfYear)
        {
            // Get an initial estimate of the year, and the days-since-epoch value that
            // represents the start of that year. Then verify estimate and fix if
            // necessary. We have the average days per 100 years to avoid getting bad candidates
            // pretty quickly.
            int daysSinceYear1 = daysSinceEpoch - DaysAtStartOfYear1;
            int candidate = ((daysSinceYear1 * 10) / averageDaysPer10Years) + 1;

            // Most of the time we'll get the right year straight away, and we'll almost
            // always get it after one adjustment - but it's safer (and easier to think about)
            // if we just keep going until we know we're right.
            int candidateStart = GetStartOfYearInDays(candidate);
            int daysFromCandidateStartToTarget = daysSinceEpoch - candidateStart;
            if (daysFromCandidateStartToTarget < 0)
            {
                // Our candidate year is later than we want. Keep going backwards until we've got
                // a non-negative result, which must then be correct.
                do
                {
                    candidate--;
                    daysFromCandidateStartToTarget += GetDaysInYear(candidate);
                }
                while (daysFromCandidateStartToTarget < 0);
                zeroBasedDayOfYear = daysFromCandidateStartToTarget;
                return candidate;
            }
            // Our candidate year is correct or earlier than the right one. Find out which by
            // comparing it with the length of the candidate year.
            int candidateLength = GetDaysInYear(candidate);
            while (daysFromCandidateStartToTarget >= candidateLength)
            {
                // Our candidate year is earlier than we want, so fast forward a year,
                // removing the current candidate length from the "remaining days" and
                // working out the length of the new candidate.
                candidate++;
                daysFromCandidateStartToTarget -= candidateLength;
                candidateLength = GetDaysInYear(candidate);
            }
            zeroBasedDayOfYear = daysFromCandidateStartToTarget;
            return candidate;
        }
        #endregion
    }
}