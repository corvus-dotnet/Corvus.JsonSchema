// <copyright file="CompiledRule.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// A compiled JsonLogic rule, ready for evaluation by the VM.
/// </summary>
/// <remarks>
/// <para>
/// This is allocated once per unique rule (identified by its UTF-8 hash),
/// then cached by the <see cref="JsonLogicEvaluator"/>.
/// </para>
/// <para>
/// The bytecode is a compact representation of the rule that can be
/// interpreted by a stack-based VM without allocations on the hot path.
/// </para>
/// </remarks>
internal readonly struct CompiledRule
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompiledRule"/> struct.
    /// </summary>
    /// <param name="bytecode">The compiled bytecode program.</param>
    /// <param name="constants">The constant pool of literal values referenced by the bytecode.</param>
    /// <param name="maxStackDepth">The maximum evaluation stack depth required.</param>
    public CompiledRule(byte[] bytecode, JsonElement[] constants, int maxStackDepth)
    {
        this.Bytecode = bytecode;
        this.Constants = constants;
        this.MaxStackDepth = maxStackDepth;
    }

    /// <summary>
    /// Gets the compiled bytecode program.
    /// </summary>
    public byte[] Bytecode { get; }

    /// <summary>
    /// Gets the constant pool. Literal values (strings, numbers, etc.)
    /// referenced by <see cref="OpCode.PushLiteral"/> instructions.
    /// </summary>
    public JsonElement[] Constants { get; }

    /// <summary>
    /// Gets the maximum evaluation stack depth required to execute this rule.
    /// Used to decide between stackalloc and ArrayPool allocation.
    /// </summary>
    public int MaxStackDepth { get; }
}