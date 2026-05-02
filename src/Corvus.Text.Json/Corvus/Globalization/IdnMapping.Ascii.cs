// <copyright file="IdnMapping.Ascii.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Diagnostics;

namespace Corvus.Globalization;

/// <summary>
/// IDN (Internationalized Domain Names) mapping class. This partial contains
/// shared constants, the <see cref="Default"/> instance, and the <see cref="IsDot(char)"/>
/// helper. The UTF-8 punycode decode implementation is in <c>IdnMapping.Unicode.UTF8.cs</c>.
/// </summary>
public sealed partial class IdnMapping
{
    public static IdnMapping Default { get; } = new IdnMapping { AllowUnassigned = true, UseStd3AsciiRules = true };

    private bool _allowUnassigned;

    private bool _useStd3AsciiRules;

    public IdnMapping()
    {
    }

    public bool AllowUnassigned
    {
        get => _allowUnassigned;
        set => _allowUnassigned = value;
    }

    public bool UseStd3AsciiRules
    {
        get => _useStd3AsciiRules;
        set => _useStd3AsciiRules = value;
    }

    // Constants shared with the UTF-8 punycode decode partial (IdnMapping.Unicode.UTF8.cs)
    private const int c_labelLimit = 63;          // Not including dots
    private const int c_defaultNameLimit = 255;   // Including dots
    private const int c_initialN = 0x80;

    private const int c_maxint = 0x7ffffff;

    private const int c_initialBias = 72;

    private const int c_punycodeBase = 36;

    private const int c_tmin = 1;

    private const int c_tmax = 26;

    private const int c_skew = 38;

    private const int c_damp = 700;

    // Is it a dot?
    // are we U+002E (., full stop), U+3002 (ideographic full stop), U+FF0E (fullwidth full stop), or
    // U+FF61 (halfwidth ideographic full stop).
    // Note: IDNA Normalization gets rid of dots now, but testing for last dot is before normalization
    internal static bool IsDot(char c) =>
        c == '.' || c == '\u3002' || c == '\uFF0E' || c == '\uFF61';

    private static int Adapt(int delta, int numpoints, bool firsttime)
    {
        uint k;

        delta = firsttime ? delta / c_damp : delta / 2;
        Debug.Assert(numpoints != 0, "[IdnMapping.adapt]Expected non-zero numpoints.");
        delta += delta / numpoints;

        for (k = 0; delta > ((c_punycodeBase - c_tmin) * c_tmax) / 2; k += c_punycodeBase)
        {
            delta /= c_punycodeBase - c_tmin;
        }

        Debug.Assert(delta + c_skew != 0, "[IdnMapping.adapt]Expected non-zero delta+skew.");
        return (int)(k + (c_punycodeBase - c_tmin + 1) * delta / (delta + c_skew));
    }
}