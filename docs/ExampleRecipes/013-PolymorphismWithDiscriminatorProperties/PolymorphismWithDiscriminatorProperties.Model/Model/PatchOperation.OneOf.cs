//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
#nullable enable
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;

namespace JsonSchemaSample.Api;
/// <summary>
/// Generated from JSON Schema.
/// </summary>
/// <remarks>
/// <para>
/// A single JSON Patch operation
/// </para>
/// </remarks>
public readonly partial struct PatchOperation
{
    /// <summary>
    /// Matches the value against each of the any of values, and returns the result of calling the provided match function for the first match found.
    /// </summary>
    /// <param name = "context">The context to pass to the match function.</param>
    /// <param name = "match0">The function to call if the value matches the <see cref = "JsonSchemaSample.Api.PatchOperation.AddOperation"/> type.</param>
    /// <param name = "match1">The function to call if the value matches the <see cref = "JsonSchemaSample.Api.PatchOperation.RemoveOperation"/> type.</param>
    /// <param name = "match2">The function to call if the value matches the <see cref = "JsonSchemaSample.Api.PatchOperation.ReplaceOperation"/> type.</param>
    /// <param name = "match3">The function to call if the value matches the <see cref = "JsonSchemaSample.Api.PatchOperation.MoveOperation"/> type.</param>
    /// <param name = "match4">The function to call if the value matches the <see cref = "JsonSchemaSample.Api.PatchOperation.CopyOperation"/> type.</param>
    /// <param name = "match5">The function to call if the value matches the <see cref = "JsonSchemaSample.Api.PatchOperation.TestOperation"/> type.</param>
    /// <param name = "defaultMatch">The fallback match.</param>
    public TOut Match<TIn, TOut>(in TIn context, Matcher<JsonSchemaSample.Api.PatchOperation.AddOperation, TIn, TOut> match0, Matcher<JsonSchemaSample.Api.PatchOperation.RemoveOperation, TIn, TOut> match1, Matcher<JsonSchemaSample.Api.PatchOperation.ReplaceOperation, TIn, TOut> match2, Matcher<JsonSchemaSample.Api.PatchOperation.MoveOperation, TIn, TOut> match3, Matcher<JsonSchemaSample.Api.PatchOperation.CopyOperation, TIn, TOut> match4, Matcher<JsonSchemaSample.Api.PatchOperation.TestOperation, TIn, TOut> match5, Matcher<PatchOperation, TIn, TOut> defaultMatch)
    {
        var oneOf0 = this.As<JsonSchemaSample.Api.PatchOperation.AddOperation>();
        if (oneOf0.IsValid())
        {
            return match0(oneOf0, context);
        }

        var oneOf1 = this.As<JsonSchemaSample.Api.PatchOperation.RemoveOperation>();
        if (oneOf1.IsValid())
        {
            return match1(oneOf1, context);
        }

        var oneOf2 = this.As<JsonSchemaSample.Api.PatchOperation.ReplaceOperation>();
        if (oneOf2.IsValid())
        {
            return match2(oneOf2, context);
        }

        var oneOf3 = this.As<JsonSchemaSample.Api.PatchOperation.MoveOperation>();
        if (oneOf3.IsValid())
        {
            return match3(oneOf3, context);
        }

        var oneOf4 = this.As<JsonSchemaSample.Api.PatchOperation.CopyOperation>();
        if (oneOf4.IsValid())
        {
            return match4(oneOf4, context);
        }

        var oneOf5 = this.As<JsonSchemaSample.Api.PatchOperation.TestOperation>();
        if (oneOf5.IsValid())
        {
            return match5(oneOf5, context);
        }

        return defaultMatch(this, context);
    }

    /// <summary>
    /// Matches the value against each of the any of values, and returns the result of calling the provided match function for the first match found.
    /// </summary>
    /// <param name = "match0">The function to call if the value matches the <see cref = "JsonSchemaSample.Api.PatchOperation.AddOperation"/> type.</param>
    /// <param name = "match1">The function to call if the value matches the <see cref = "JsonSchemaSample.Api.PatchOperation.RemoveOperation"/> type.</param>
    /// <param name = "match2">The function to call if the value matches the <see cref = "JsonSchemaSample.Api.PatchOperation.ReplaceOperation"/> type.</param>
    /// <param name = "match3">The function to call if the value matches the <see cref = "JsonSchemaSample.Api.PatchOperation.MoveOperation"/> type.</param>
    /// <param name = "match4">The function to call if the value matches the <see cref = "JsonSchemaSample.Api.PatchOperation.CopyOperation"/> type.</param>
    /// <param name = "match5">The function to call if the value matches the <see cref = "JsonSchemaSample.Api.PatchOperation.TestOperation"/> type.</param>
    /// <param name = "defaultMatch">The fallback match.</param>
    public TOut Match<TOut>(Matcher<JsonSchemaSample.Api.PatchOperation.AddOperation, TOut> match0, Matcher<JsonSchemaSample.Api.PatchOperation.RemoveOperation, TOut> match1, Matcher<JsonSchemaSample.Api.PatchOperation.ReplaceOperation, TOut> match2, Matcher<JsonSchemaSample.Api.PatchOperation.MoveOperation, TOut> match3, Matcher<JsonSchemaSample.Api.PatchOperation.CopyOperation, TOut> match4, Matcher<JsonSchemaSample.Api.PatchOperation.TestOperation, TOut> match5, Matcher<PatchOperation, TOut> defaultMatch)
    {
        var oneOf0 = this.As<JsonSchemaSample.Api.PatchOperation.AddOperation>();
        if (oneOf0.IsValid())
        {
            return match0(oneOf0);
        }

        var oneOf1 = this.As<JsonSchemaSample.Api.PatchOperation.RemoveOperation>();
        if (oneOf1.IsValid())
        {
            return match1(oneOf1);
        }

        var oneOf2 = this.As<JsonSchemaSample.Api.PatchOperation.ReplaceOperation>();
        if (oneOf2.IsValid())
        {
            return match2(oneOf2);
        }

        var oneOf3 = this.As<JsonSchemaSample.Api.PatchOperation.MoveOperation>();
        if (oneOf3.IsValid())
        {
            return match3(oneOf3);
        }

        var oneOf4 = this.As<JsonSchemaSample.Api.PatchOperation.CopyOperation>();
        if (oneOf4.IsValid())
        {
            return match4(oneOf4);
        }

        var oneOf5 = this.As<JsonSchemaSample.Api.PatchOperation.TestOperation>();
        if (oneOf5.IsValid())
        {
            return match5(oneOf5);
        }

        return defaultMatch(this);
    }
}