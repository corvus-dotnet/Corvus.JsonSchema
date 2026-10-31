// <copyright file="JsonSchemaImageRegularExpression.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.RuntimeEvaluator;

/// <summary>
/// A regular expression a program image's evaluator matches a pattern with (see
/// <see cref="JsonSchemaEvaluator.GetImageRegularExpressions"/>): the pattern's index in the image's table, and the
/// flags, edition and version of Unicode it is compiled with.
/// </summary>
[CLSCompliant(false)]
public readonly struct JsonSchemaImageRegularExpression
{
    /// <summary>Initializes a new instance of the <see cref="JsonSchemaImageRegularExpression"/> struct.</summary>
    /// <param name="index">The index of the pattern in the image's table.</param>
    /// <param name="pattern">The pattern, in ECMA-262 syntax as the schema has it.</param>
    /// <param name="flags">The flags it is compiled with: the u flag, or none for a pattern valid only without it.</param>
    /// <param name="edition">The edition of the grammar it is read with.</param>
    /// <param name="unicodeVersion">The version of Unicode it is read with.</param>
    public JsonSchemaImageRegularExpression(int index, string pattern, Corvus.Ecma262.RegularExpressions.RegexFlags flags, Corvus.Ecma262.RegularExpressions.RegexEdition edition, Corvus.Ecma262.RegularExpressions.RegexUnicodeVersion unicodeVersion)
    {
        this.Index = index;
        this.Pattern = pattern;
        this.Flags = flags;
        this.Edition = edition;
        this.UnicodeVersion = unicodeVersion;
    }

    /// <summary>Gets the index of the pattern in the image's table, as a <see cref="JsonSchemaEcmaRegexProvider"/> is given it.</summary>
    public int Index { get; }

    /// <summary>Gets the pattern, in ECMA-262 syntax as the schema has it.</summary>
    public string Pattern { get; }

    /// <summary>Gets the flags the pattern is compiled with.</summary>
    public Corvus.Ecma262.RegularExpressions.RegexFlags Flags { get; }

    /// <summary>Gets the edition of the grammar the pattern is read with.</summary>
    public Corvus.Ecma262.RegularExpressions.RegexEdition Edition { get; }

    /// <summary>Gets the version of Unicode the pattern is read with (resolved: never <c>Default</c>).</summary>
    public Corvus.Ecma262.RegularExpressions.RegexUnicodeVersion UnicodeVersion { get; }
}