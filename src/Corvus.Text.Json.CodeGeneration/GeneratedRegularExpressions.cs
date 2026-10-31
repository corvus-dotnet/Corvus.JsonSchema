// <copyright file="GeneratedRegularExpressions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// The engine the regular expressions in generated code are written for.
/// </summary>
public enum GeneratedRegexEngine
{
    /// <summary>
    /// Corvus.Regex.Ecma262 (the default): each pattern is written as the library's compiled matcher, with the
    /// compiled regular expression as data, so nothing is parsed or built when it is first used, and it answers as
    /// ECMAScript does over UTF-8 with no transcoding.
    /// </summary>
    Ecma262,

    /// <summary>
    /// .NET's <c>System.Text.RegularExpressions</c>, as earlier versions wrote it: a translation of each pattern, built
    /// by .NET's regular expression generator where it runs. For code that depends on the generated fields being
    /// .NET's <c>Regex</c>.
    /// </summary>
    DotNet,
}

/// <summary>
/// The edition of ECMA-262 whose grammar the patterns of generated code are read with, when it is not the dialect's.
/// The values are those of <c>Corvus.Ecma262.RegularExpressions.RegexEdition</c>.
/// </summary>
public enum GeneratedRegexEdition
{
    /// <summary>The grammar of today's ECMAScript, which drafts 4, 6 and 7 and OpenAPI 2.0 and 3.0 read patterns with.</summary>
    Current = 0,

    /// <summary>The grammar of ECMAScript 2020, which draft 2019-09 and draft 2020-12 (and OpenAPI 3.1) cite.</summary>
    Es2020 = 1,
}

/// <summary>
/// The version of Unicode whose character database the patterns of generated code are read with, when it is not the
/// default. The values are those of <c>Corvus.Ecma262.RegularExpressions.RegexUnicodeVersion</c>.
/// </summary>
public enum GeneratedRegexUnicodeVersion
{
    /// <summary>The library's default: the version V8 uses (17).</summary>
    Default = 0,

    /// <summary>Unicode 13.</summary>
    V13 = 13,

    /// <summary>Unicode 17.</summary>
    V17 = 17,

    /// <summary>Unicode 18.</summary>
    V18 = 18,

    /// <summary>The latest version the library has.</summary>
    Latest = int.MaxValue,
}