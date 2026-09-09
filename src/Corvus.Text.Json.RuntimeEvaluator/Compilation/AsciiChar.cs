// <copyright file="AsciiChar.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.RuntimeEvaluator.Compilation;

/// <summary>
/// ASCII character classification available on every target framework.
/// </summary>
internal static class AsciiChar
{
    public static bool IsLetter(char c) => (uint)((c | 0x20) - 'a') <= 'z' - 'a';

    public static bool IsLetterOrDigit(char c) => IsLetter(c) || (uint)(c - '0') <= 9;
}
