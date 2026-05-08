// <copyright file="TestExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Extension methods extracted from test classes for MSTest compatibility.
/// MSTest requires test classes to be non-static, but extension methods require static classes.
/// </summary>
internal static class TestExtensions
{
    public static JsonDocumentBuilder<JsonElement.Mutable> BuildDynamicDocument(this JsonElement source, JsonWorkspace workspace)
    {
        return ParsedDocumentExtensions.BuildDynamicDocument(source, workspace);
    }

    public static string PrintJson(this JsonDocumentBuilder<JsonElement.Mutable> document, int sizeHint = 0)
    {
        return JsonDocumentBuilderCreateDynamicTests.PrintJson(document, sizeHint);
    }

    public static string PrintJson(this JsonElement.Mutable element, int sizeHint = 0)
    {
        return JsonDocumentBuilderCreateDynamicTests.PrintJson(element, sizeHint);
    }

    public static ParsedJsonDocument<JsonElement> SniffDocument(this JsonElement element)
    {
        return (ParsedJsonDocument<JsonElement>)typeof(JsonElement)
            .GetField("_parent", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(element)!;
    }

    public static string NormalizeToJsonNetFormat(this string json, bool skipSpecialRules)
    {
        return WriterHelpers.NormalizeToJsonNetFormat(json, skipSpecialRules);
    }
}
