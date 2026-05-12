// <copyright file="JsonLogicRule.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// An immutable wrapper around a <see cref="JsonElement"/> representing a JsonLogic rule.
/// </summary>
/// <remarks>
/// <para>
/// A JsonLogic rule is a JSON value where objects represent operations
/// (the single key is the operator name, the value is the arguments)
/// and all other JSON values are literals.
/// </para>
/// <para>
/// This type is cheaply constructed from any <see cref="JsonElement"/>.
/// </para>
/// </remarks>
public readonly struct JsonLogicRule
{
    private readonly JsonElement rule;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonLogicRule"/> struct.
    /// </summary>
    /// <param name="rule">The JSON element representing the rule.</param>
    public JsonLogicRule(in JsonElement rule)
    {
        this.rule = rule;
    }

    /// <summary>
    /// Gets the underlying <see cref="JsonElement"/> for this rule.
    /// </summary>
    public JsonElement Rule => this.rule;
}