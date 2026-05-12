// <copyright file="TokenType.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// The type of a JSONPath lexer token.
/// </summary>
internal enum TokenType : byte
{
    /// <summary>The <c>$</c> root identifier.</summary>
    Root,

    /// <summary>The <c>@</c> current node identifier.</summary>
    Current,

    /// <summary>The <c>.</c> child segment separator (dot notation).</summary>
    Dot,

    /// <summary>The <c>..</c> descendant segment.</summary>
    DotDot,

    /// <summary>The <c>[</c> bracket open.</summary>
    LeftBracket,

    /// <summary>The <c>]</c> bracket close.</summary>
    RightBracket,

    /// <summary>The <c>(</c> parenthesis open.</summary>
    LeftParen,

    /// <summary>The <c>)</c> parenthesis close.</summary>
    RightParen,

    /// <summary>The <c>*</c> wildcard selector.</summary>
    Wildcard,

    /// <summary>The <c>,</c> selector separator.</summary>
    Comma,

    /// <summary>The <c>:</c> slice separator.</summary>
    Colon,

    /// <summary>The <c>?</c> filter prefix.</summary>
    Question,

    /// <summary>An unquoted member name (dot-notation shorthand).</summary>
    Name,

    /// <summary>A single-quoted string selector (<c>'name'</c>).</summary>
    SingleQuotedString,

    /// <summary>A double-quoted string (used in filter comparisons).</summary>
    DoubleQuotedString,

    /// <summary>An integer literal (index or slice component).</summary>
    Integer,

    /// <summary>A numeric literal with a fractional part or exponent (filter comparisons).</summary>
    Number,

    /// <summary>The <c>==</c> comparison operator.</summary>
    Equal,

    /// <summary>The <c>!=</c> comparison operator.</summary>
    NotEqual,

    /// <summary>The <c>&lt;</c> comparison operator.</summary>
    LessThan,

    /// <summary>The <c>&lt;=</c> comparison operator.</summary>
    LessThanOrEqual,

    /// <summary>The <c>&gt;</c> comparison operator.</summary>
    GreaterThan,

    /// <summary>The <c>&gt;=</c> comparison operator.</summary>
    GreaterThanOrEqual,

    /// <summary>The <c>&amp;&amp;</c> logical AND operator.</summary>
    And,

    /// <summary>The <c>||</c> logical OR operator.</summary>
    Or,

    /// <summary>The <c>!</c> logical NOT operator.</summary>
    Not,

    /// <summary>The <c>true</c> literal.</summary>
    True,

    /// <summary>The <c>false</c> literal.</summary>
    False,

    /// <summary>The <c>null</c> literal.</summary>
    Null,

    /// <summary>A function name followed by <c>(</c>.</summary>
    Function,

    /// <summary>End of expression.</summary>
    Eof,
}
