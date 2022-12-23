﻿// <copyright file="RegularYearMonthDayCalculator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable

// Copyright 2013 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using NodaTime.Annotations;
using System;

namespace NodaTime.Calendars
{
    /// <summary>
    /// Subclass of YearMonthDayCalculator for calendars with the following attributes:
    /// <list type="bullet">
    /// <item>A fixed number of months</item>
    /// <item>Occasional leap years which are always 1 day longer than non-leap years</item>
    /// <item>The year starting with month 1, day 1 (i.e. naive YearMonthDay comparisons work)</item>
    /// </list>
    /// </summary>
    internal abstract class RegularYearMonthDayCalculator : YearMonthDayCalculator
    {
        private readonly int monthsInYear;

        protected RegularYearMonthDayCalculator(int minYear, int maxYear, int monthsInYear,
            int averageDaysPer10Years, int daysAtStartOfYear1)
            : base(minYear, maxYear, averageDaysPer10Years, daysAtStartOfYear1)
        {
            this.monthsInYear = monthsInYear;
        }

        internal override int GetMonthsInYear(int year) => monthsInYear;

        /// <summary>
        /// Implements a simple year-setting policy, truncating the day
        /// if necessary.
        /// </summary>
        internal override YearMonthDay SetYear(YearMonthDay yearMonthDay, int year)
        {
            // If this ever becomes a bottleneck due to GetDaysInMonth, it can be overridden
            // in subclasses.
            int currentMonth = yearMonthDay.Month;
            int currentDay = yearMonthDay.Day;
            int newDay = GetDaysInMonth(year, currentMonth);
            return new YearMonthDay(year, currentMonth, Math.Min(currentDay, newDay));
        }

        internal override YearMonthDay AddMonths(YearMonthDay yearMonthDay, int months)
        {
            if (months == 0)
            {
                return yearMonthDay;
            }
            // Get the year and month
            int thisYear = yearMonthDay.Year;
            int thisMonth = yearMonthDay.Month;

            // Do not refactor without careful consideration.
            // Order of calculation is important.

            int yearToUse;
            // Initially, monthToUse is zero-based
            int monthToUse = thisMonth - 1 + months;
            if (monthToUse >= 0)
            {
                yearToUse = thisYear + (monthToUse / monthsInYear);
                monthToUse = (monthToUse % monthsInYear) + 1;
            }
            else
            {
                yearToUse = thisYear + (monthToUse / monthsInYear) - 1;
                monthToUse = Math.Abs(monthToUse);
                int remMonthToUse = monthToUse % monthsInYear;
                // Take care of the boundary condition
                if (remMonthToUse == 0)
                {
                    remMonthToUse = monthsInYear;
                }
                monthToUse = monthsInYear - remMonthToUse + 1;
                // Take care of the boundary condition
                if (monthToUse == 1)
                {
                    yearToUse++;
                }
            }
            // End of do not refactor.

            // Quietly force DOM to nearest sane value.
            int dayToUse = yearMonthDay.Day;
            int maxDay = GetDaysInMonth(yearToUse, monthToUse);
            dayToUse = Math.Min(dayToUse, maxDay);
            if (yearToUse < MinYear || yearToUse > MaxYear)
            {
                throw new OverflowException("Date computation would overflow calendar bounds.");
            }
            return new YearMonthDay(yearToUse, monthToUse, dayToUse);
        }

        internal override int MonthsBetween(YearMonthDay start, YearMonthDay end)
        {
            int startMonth = start.Month;
            int startYear = start.Year;
            int endMonth = end.Month;
            int endYear = end.Year;

            int diff = (endYear - startYear) * monthsInYear + endMonth - startMonth;

            // If we just add the difference in months to start, what do we get?
            YearMonthDay simpleAddition = AddMonths(start, diff);

            // Note: this relies on naive comparison of year/month/date values.
            if (start <= end)
            {
                // Moving forward: if the result of the simple addition is before or equal to the end,
                // we're done. Otherwise, rewind a month because we've overshot.
                return simpleAddition <= end ? diff : diff - 1;
            }
            else
            {
                // Moving backward: if the result of the simple addition (of a non-positive number)
                // is after or equal to the end, we're done. Otherwise, increment by a month because
                // we've overshot backwards.
                return simpleAddition >= end ? diff : diff + 1;
            }
        }
    }
}