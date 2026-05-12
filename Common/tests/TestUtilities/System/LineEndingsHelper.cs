// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
namespace System;

public static class LineEndingsHelper
{
    public const string CompiledNewline = @"
";

    public static readonly bool s_consistentNewlines = StringComparer.Ordinal.Equals(CompiledNewline, Environment.NewLine);

    public static bool IsNewLineConsistent
    {
        get { return s_consistentNewlines; }
    }

    public static string Normalize(string expected)
    {
        if (s_consistentNewlines)
            return expected;

        return expected.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
    }
}