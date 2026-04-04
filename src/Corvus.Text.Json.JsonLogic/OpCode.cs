// <copyright file="OpCode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// Bytecode opcodes for the JsonLogic VM.
/// </summary>
public enum OpCode : byte
{
    // Stack ops

    /// <summary>
    /// Push a literal value from the constant pool onto the stack.
    /// Operand: constant pool index.
    /// </summary>
    PushLiteral,

    /// <summary>
    /// Push the current data context onto the stack.
    /// </summary>
    PushData,

    /// <summary>
    /// Lookup a variable by dot-path from the constant pool.
    /// Operand: constant pool index of the path string.
    /// </summary>
    Var,

    /// <summary>
    /// Lookup a variable with a default value.
    /// The default is on the stack; operand is the path constant pool index.
    /// </summary>
    VarWithDefault,

    // Logic

    /// <summary>
    /// Coercing equality (==). JS-style type coercion.
    /// </summary>
    Equals,

    /// <summary>
    /// Strict equality (===). No type coercion.
    /// </summary>
    StrictEquals,

    /// <summary>
    /// Coercing inequality (!=).
    /// </summary>
    NotEquals,

    /// <summary>
    /// Strict inequality (!==).
    /// </summary>
    StrictNotEquals,

    /// <summary>
    /// Logical NOT (!). Returns truthy/falsy inverse.
    /// </summary>
    Not,

    /// <summary>
    /// Double NOT (!!). Converts to boolean.
    /// </summary>
    Truthy,

    // Short-circuit control flow

    /// <summary>
    /// Jump forward if the top of stack is falsy (for <c>and</c> / <c>if</c>).
    /// Operand: jump offset in bytes.
    /// </summary>
    JumpIfFalsy,

    /// <summary>
    /// Jump forward if the top of stack is truthy (for <c>or</c>).
    /// Operand: jump offset in bytes.
    /// </summary>
    JumpIfTruthy,

    /// <summary>
    /// Unconditional jump.
    /// Operand: jump offset in bytes.
    /// </summary>
    Jump,

    // Numeric — comparison uses CompareNormalizedJsonNumbers (UTF-8, zero-alloc)

    /// <summary>
    /// Greater than (&gt;).
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Greater than or equal (&gt;=).
    /// </summary>
    GreaterThanOrEqual,

    /// <summary>
    /// Less than (&lt;).
    /// </summary>
    LessThan,

    /// <summary>
    /// Less than or equal (&lt;=).
    /// </summary>
    LessThanOrEqual,

    /// <summary>
    /// Minimum of N values. Operand: count.
    /// </summary>
    Min,

    /// <summary>
    /// Maximum of N values. Operand: count.
    /// </summary>
    Max,

    // Numeric — arithmetic converts to BigNumber, computes, writes back

    /// <summary>
    /// Addition (+). N-ary; operand: count.
    /// </summary>
    Add,

    /// <summary>
    /// Subtraction (-). Binary or unary negation.
    /// Operand: arg count (1 = negate, 2 = subtract).
    /// </summary>
    Sub,

    /// <summary>
    /// Multiplication (*). N-ary; operand: count.
    /// </summary>
    Mul,

    /// <summary>
    /// Division (/). Binary.
    /// </summary>
    Div,

    /// <summary>
    /// Modulo (%). Uses UTF-8 modular arithmetic where possible.
    /// </summary>
    Mod,

    // Explicit numeric conversion

    /// <summary>
    /// Convert to double-precision float. User opt-in to lossy conversion.
    /// </summary>
    AsDouble,

    /// <summary>
    /// Convert to 64-bit integer. User opt-in to truncation.
    /// </summary>
    AsLong,

    /// <summary>
    /// Convert to BigNumber. Arbitrary-precision decimal round-trip.
    /// </summary>
    AsBigNumber,

    /// <summary>
    /// Convert to BigInteger. Truncate fractional part, arbitrary precision.
    /// </summary>
    AsBigInteger,

    // String

    /// <summary>
    /// String concatenation. Operand: count.
    /// </summary>
    Cat,

    /// <summary>
    /// Substring extraction.
    /// </summary>
    Substr,

    /// <summary>
    /// String contains check (in for strings).
    /// </summary>
    InString,

    // Array

    /// <summary>
    /// Array membership check (in for arrays).
    /// </summary>
    InArray,

    /// <summary>
    /// Merge arrays. Operand: count.
    /// </summary>
    Merge,

    /// <summary>
    /// Begin a map loop. Switches data context.
    /// Operand: jump offset to loop end.
    /// </summary>
    MapBegin,

    /// <summary>
    /// Begin a filter loop. Switches data context.
    /// Operand: jump offset to loop end.
    /// </summary>
    FilterBegin,

    /// <summary>
    /// Begin a reduce loop. Switches data context.
    /// Operand: jump offset to loop end.
    /// </summary>
    ReduceBegin,

    /// <summary>
    /// Begin an all check loop. Switches data context.
    /// Operand: jump offset to loop end.
    /// </summary>
    AllBegin,

    /// <summary>
    /// Begin a none check loop. Switches data context.
    /// Operand: jump offset to loop end.
    /// </summary>
    NoneBegin,

    /// <summary>
    /// Begin a some check loop. Switches data context.
    /// Operand: jump offset to loop end.
    /// </summary>
    SomeBegin,

    /// <summary>
    /// End of a loop body.
    /// </summary>
    LoopEnd,

    // Data

    /// <summary>
    /// Check for missing variables. Operand: count.
    /// </summary>
    Missing,

    /// <summary>
    /// Check for missing_some variables.
    /// </summary>
    MissingSome,

    // Misc

    /// <summary>
    /// Log the top of stack (side-effect).
    /// </summary>
    Log,

    /// <summary>
    /// Return the top of stack as the result.
    /// </summary>
    Return,
}