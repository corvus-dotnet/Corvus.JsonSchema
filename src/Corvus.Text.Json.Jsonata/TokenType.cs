// <copyright file="TokenType.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// The type of a JSONata token produced by the lexer.
/// </summary>
internal enum TokenType : byte
{
    /// <summary>A field name (unquoted identifier or backtick-quoted name).</summary>
    Name,

    /// <summary>A variable reference (<c>$name</c>). The value excludes the leading <c>$</c>.</summary>
    Variable,

    /// <summary>An operator or punctuation symbol.</summary>
    Operator,

    /// <summary>A string literal (single or double quoted).</summary>
    String,

    /// <summary>A numeric literal.</summary>
    Number,

    /// <summary>A literal value: <c>true</c>, <c>false</c>, or <c>null</c>.</summary>
    Value,

    /// <summary>A regular expression literal (<c>/pattern/flags</c>).</summary>
    Regex,

    /// <summary>Sentinel indicating the end of the input.</summary>
    End,
}