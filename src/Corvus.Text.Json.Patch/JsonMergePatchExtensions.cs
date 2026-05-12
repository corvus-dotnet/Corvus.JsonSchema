// <copyright file="JsonMergePatchExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Patch;

/// <summary>
/// Provides extension methods for applying RFC 7396 JSON Merge Patch operations.
/// </summary>
public static class JsonMergePatchExtensions
{
    /// <summary>
    /// Applies a JSON Merge Patch (RFC 7396) to a mutable JSON element.
    /// </summary>
    /// <param name="target">The mutable root element to patch. This value is modified in place;
    /// if the patch is not an object, the target is replaced entirely.</param>
    /// <param name="patch">The merge patch to apply.</param>
    /// <remarks>
    /// <para>
    /// The merge patch algorithm (RFC 7396 Section 2):
    /// </para>
    /// <list type="bullet">
    /// <item><description>If the patch is an object, each property is merged recursively into the target.
    /// Properties with <see langword="null"/> values are removed from the target.</description></item>
    /// <item><description>If the patch is not an object, the target is replaced entirely by the patch value.</description></item>
    /// <item><description>Arrays are replaced wholesale — they are not merged element-by-element.</description></item>
    /// </list>
    /// </remarks>
    public static void ApplyMergePatch(ref JsonElement.Mutable target, in JsonElement patch)
    {
        if (patch.ValueKind != JsonValueKind.Object)
        {
            JsonElement.Source source = patch;
            ReplaceRoot(ref target, in source);
            return;
        }

        if (target.ValueKind != JsonValueKind.Object)
        {
            ReplaceRootWithEmptyObject(ref target);
        }

        foreach (JsonProperty<JsonElement> patchProperty in patch.EnumerateObject())
        {
            JsonElement patchValue = patchProperty.Value;

            if (patchValue.ValueKind == JsonValueKind.Null)
            {
                using UnescapedUtf8JsonString nameUtf8 = patchProperty.Utf8NameSpan;
                target.RemoveProperty(nameUtf8.Span);
            }
            else if (patchValue.ValueKind == JsonValueKind.Object)
            {
                using UnescapedUtf8JsonString nameUtf8 = patchProperty.Utf8NameSpan;
                ReadOnlySpan<byte> nameSpan = nameUtf8.Span;

                if (!target.TryGetProperty(nameSpan, out JsonElement.Mutable child)
                    || child.ValueKind != JsonValueKind.Object)
                {
                    // Property doesn't exist or isn't an object — set it to empty object
                    target.SetProperty(nameSpan, static (ref JsonElement.ObjectBuilder _) => { }, estimatedMemberCount: 0);
                    target.TryGetProperty(nameSpan, out child);
                }

                ApplyMergePatch(ref child, in patchValue);
            }
            else
            {
                using UnescapedUtf8JsonString nameUtf8 = patchProperty.Utf8NameSpan;
                target.SetProperty(nameUtf8.Span, patchValue);
            }
        }
    }

    private static void ReplaceRoot(ref JsonElement.Mutable target, in JsonElement.Source value)
    {
        IMutableJsonDocument mutableDoc = GetMutableDoc(in target);
        ComplexValueBuilder cvb = ComplexValueBuilder.Create(mutableDoc, 30);
        value.AddAsItem(ref cvb);
        mutableDoc.ReplaceRootAndDispose(ref cvb);
        target = ((JsonDocumentBuilder<JsonElement.Mutable>)mutableDoc).RootElement;
    }

    private static void ReplaceRootWithEmptyObject(ref JsonElement.Mutable target)
    {
        IMutableJsonDocument mutableDoc = GetMutableDoc(in target);
        ComplexValueBuilder cvb = ComplexValueBuilder.Create(mutableDoc, 0);
        JsonElement.ObjectBuilder.BuildValue(static (ref JsonElement.ObjectBuilder _) => { }, ref cvb);
        mutableDoc.ReplaceRootAndDispose(ref cvb);
        target = ((JsonDocumentBuilder<JsonElement.Mutable>)mutableDoc).RootElement;
    }

    private static IMutableJsonDocument GetMutableDoc<T>(in T element)
        where T : struct, IJsonElement<T>
    {
        return (IMutableJsonDocument)element.ParentDocument;
    }
}