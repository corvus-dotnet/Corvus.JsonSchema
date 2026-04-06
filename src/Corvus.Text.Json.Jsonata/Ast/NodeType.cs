// <copyright file="NodeType.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// Discriminator for the concrete <see cref="JsonataNode"/> subtypes.
/// </summary>
internal enum NodeType : byte
{
    /// <summary>A path expression with an array of navigation steps.</summary>
    Path,

    /// <summary>A binary operator expression (<c>lhs op rhs</c>).</summary>
    Binary,

    /// <summary>A unary operator expression (<c>op expr</c>).</summary>
    Unary,

    /// <summary>An array constructor (<c>[ expr, ... ]</c>).</summary>
    ArrayConstructor,

    /// <summary>An object constructor (<c>{ key: value, ... }</c>).</summary>
    ObjectConstructor,

    /// <summary>A function call (<c>fn(args)</c>).</summary>
    FunctionCall,

    /// <summary>Partial function application (<c>fn(?, arg)</c>).</summary>
    Partial,

    /// <summary>A lambda expression (<c>function($x){ body }</c>).</summary>
    Lambda,

    /// <summary>A ternary condition (<c>cond ? then : else</c>).</summary>
    Condition,

    /// <summary>A block expression (<c>( expr; expr )</c>).</summary>
    Block,

    /// <summary>A variable binding (<c>$var := expr</c>).</summary>
    Bind,

    /// <summary>A transform expression (<c>|source|update,delete|</c>).</summary>
    Transform,

    /// <summary>A sort step (<c>^(&lt;field, &gt;field)</c>).</summary>
    Sort,

    /// <summary>A filter predicate in a path step.</summary>
    Filter,

    /// <summary>A field name.</summary>
    Name,

    /// <summary>A string literal.</summary>
    String,

    /// <summary>A numeric literal.</summary>
    Number,

    /// <summary>A literal value (<c>true</c>, <c>false</c>, or <c>null</c>).</summary>
    Value,

    /// <summary>A variable reference (<c>$name</c>).</summary>
    Variable,

    /// <summary>A wildcard step (<c>*</c>).</summary>
    Wildcard,

    /// <summary>A descendant wildcard step (<c>**</c>).</summary>
    Descendant,

    /// <summary>A regex literal (<c>/pattern/flags</c>).</summary>
    Regex,

    /// <summary>The parent operator (<c>%</c>).</summary>
    Parent,

    /// <summary>The chain/apply operator (<c>~&gt;</c>).</summary>
    Apply,

    /// <summary>A partial application placeholder (<c>?</c>).</summary>
    Placeholder,
}