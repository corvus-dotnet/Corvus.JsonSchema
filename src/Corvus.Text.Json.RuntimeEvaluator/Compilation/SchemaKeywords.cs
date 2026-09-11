// <copyright file="SchemaKeywords.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.RuntimeEvaluator.Compilation;

/// <summary>
/// How a keyword holds subschemas.
/// </summary>
internal enum SubschemaKeywordKind
{
    None,
    Single,
    SingleOrArray,
    Array,
    Map,
}

/// <summary>
/// Dialect-aware keyword classification used for schema identification traversal.
/// </summary>
internal static class SchemaKeywords
{
    public static SubschemaKeywordKind GetSubschemaKind(ReadOnlySpan<byte> keyword, JsonSchemaDialect dialect, bool legacyRefOverridesSiblings)
    {
        // Definitions containers are always traversable: they are reachable by pointer references
        // even when sibling keywords are ignored.
        if (keyword.SequenceEqual("definitions"u8) || keyword.SequenceEqual("$defs"u8))
        {
            return SubschemaKeywordKind.Map;
        }

        if (legacyRefOverridesSiblings)
        {
            return SubschemaKeywordKind.None;
        }

        if (keyword.SequenceEqual("properties"u8) || keyword.SequenceEqual("patternProperties"u8))
        {
            return SubschemaKeywordKind.Map;
        }

        if (keyword.SequenceEqual("additionalProperties"u8) || keyword.SequenceEqual("not"u8))
        {
            return SubschemaKeywordKind.Single;
        }

        if (keyword.SequenceEqual("allOf"u8) || keyword.SequenceEqual("anyOf"u8) || keyword.SequenceEqual("oneOf"u8))
        {
            return SubschemaKeywordKind.Array;
        }

        if (keyword.SequenceEqual("items"u8))
        {
            return dialect >= JsonSchemaDialect.Draft202012 ? SubschemaKeywordKind.Single : SubschemaKeywordKind.SingleOrArray;
        }

        if (keyword.SequenceEqual("additionalItems"u8))
        {
            return dialect <= JsonSchemaDialect.Draft201909 ? SubschemaKeywordKind.Single : SubschemaKeywordKind.None;
        }

        if (keyword.SequenceEqual("dependencies"u8))
        {
            return SubschemaKeywordKind.Map;
        }

        if (dialect >= JsonSchemaDialect.Draft6)
        {
            if (keyword.SequenceEqual("contains"u8) || keyword.SequenceEqual("propertyNames"u8))
            {
                return SubschemaKeywordKind.Single;
            }
        }

        if (dialect >= JsonSchemaDialect.Draft7)
        {
            if (keyword.SequenceEqual("if"u8) || keyword.SequenceEqual("then"u8) || keyword.SequenceEqual("else"u8))
            {
                return SubschemaKeywordKind.Single;
            }
        }

        if (dialect >= JsonSchemaDialect.Draft201909)
        {
            if (keyword.SequenceEqual("unevaluatedProperties"u8) || keyword.SequenceEqual("unevaluatedItems"u8) || keyword.SequenceEqual("contentSchema"u8))
            {
                return SubschemaKeywordKind.Single;
            }

            if (keyword.SequenceEqual("dependentSchemas"u8))
            {
                return SubschemaKeywordKind.Map;
            }
        }

        if (dialect >= JsonSchemaDialect.Draft202012)
        {
            if (keyword.SequenceEqual("prefixItems"u8))
            {
                return SubschemaKeywordKind.Array;
            }
        }

        return SubschemaKeywordKind.None;
    }
}