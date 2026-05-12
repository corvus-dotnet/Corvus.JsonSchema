// <copyright file="TokenType.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JMESPath;

/// <summary>
/// The type of a JMESPath lexer token.
/// </summary>
internal enum TokenType : byte
{
    /// <summary>An unquoted identifier (property name).</summary>
    Identifier,

    /// <summary>A quoted identifier (double-quoted property name).</summary>
    QuotedIdentifier,

    /// <summary>An integer literal (for array indices and slices).</summary>
    Number,

    /// <summary>A raw string literal (single-quoted, no escape processing).</summary>
    RawString,

    /// <summary>A JSON literal (backtick-delimited embedded JSON).</summary>
    Literal,

    /// <summary>The <c>.</c> operator (sub-expression / child).</summary>
    Dot,

    /// <summary>The <c>[</c> token.</summary>
    LeftBracket,

    /// <summary>The <c>]</c> token.</summary>
    RightBracket,

    /// <summary>The <c>(</c> token.</summary>
    LeftParen,

    /// <summary>The <c>)</c> token.</summary>
    RightParen,

    /// <summary>The <c>{</c> token.</summary>
    LeftBrace,

    /// <summary>The <c>}</c> token.</summary>
    RightBrace,

    /// <summary>The <c>,</c> separator.</summary>
    Comma,

    /// <summary>The <c>:</c> token (used in slices and multi-select hash).</summary>
    Colon,

    /// <summary>The <c>*</c> wildcard.</summary>
    Star,

    /// <summary>The <c>@</c> current node reference.</summary>
    At,

    /// <summary>The <c>&amp;</c> expression reference.</summary>
    Ampersand,

    /// <summary>The <c>|</c> pipe operator.</summary>
    Pipe,

    /// <summary>The <c>||</c> logical OR operator.</summary>
    Or,

    /// <summary>The <c>&amp;&amp;</c> logical AND operator.</summary>
    And,

    /// <summary>The <c>!</c> logical NOT operator.</summary>
    Not,

    /// <summary>The <c>[?</c> filter start token.</summary>
    Filter,

    /// <summary>The <c>[]</c> flatten operator.</summary>
    Flatten,

    /// <summary>The <c>&lt;</c> comparator.</summary>
    LessThan,

    /// <summary>The <c>&lt;=</c> comparator.</summary>
    LessThanOrEqual,

    /// <summary>The <c>==</c> comparator.</summary>
    Equal,

    /// <summary>The <c>&gt;=</c> comparator.</summary>
    GreaterThanOrEqual,

    /// <summary>The <c>&gt;</c> comparator.</summary>
    GreaterThan,

    /// <summary>The <c>!=</c> comparator.</summary>
    NotEqual,

    /// <summary>End of expression.</summary>
    Eof,
}
