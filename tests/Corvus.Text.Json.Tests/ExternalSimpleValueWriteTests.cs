// <copyright file="ExternalSimpleValueWriteTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests that serializing a <see cref="JsonDocumentBuilder{T}"/> correctly handles simple values
/// (strings, numbers) that originate from external <see cref="FixedJsonValueDocument{T}"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// When a builder references an element from an external document, the write loop must call
/// <c>GetRawSimpleValue(index, includeQuotes: false)</c> to strip the surrounding JSON quotes
/// from string values. Using the 1-parameter overload on <see cref="FixedJsonValueDocument{T}"/>
/// returns the raw bytes <em>with</em> quotes, producing double-quoted output like
/// <c>"\u0022HELLO\u0022"</c> instead of <c>"HELLO"</c>.
/// </para>
/// <para>
/// This scenario is exercised in practice by the JSONata evaluator, which creates
/// <see cref="FixedJsonValueDocument{T}"/> instances for intermediate string/number results
/// and adds them to builder arrays and objects.
/// </para>
/// </remarks>
public class ExternalSimpleValueWriteTests
{
    /// <summary>
    /// Verifies that a string from an external FixedJsonValueDocument is serialized without
    /// extra quote characters when it appears as an array item in a builder.
    /// </summary>
    [Fact]
    public void WriteArrayItem_StringFromExternalDocument_NoExtraQuotes()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // Create an external string document (raw bytes include quotes, like Jsonata produces)
        FixedJsonValueDocument<JsonElement> stringDoc =
            FixedJsonValueDocument<JsonElement>.ForString(Encoding.UTF8.GetBytes("\"HELLO\""));
        workspace.RegisterDocument(stringDoc);

        // Build an array containing the external string
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "[]".AsMemory());
        builder.RootElement.AddItem((JsonElement)stringDoc.RootElement);

        JsonElement result = builder.RootElement.Freeze();
        Assert.Equal("[\"HELLO\"]", result.GetRawText());
    }

    /// <summary>
    /// Verifies that multiple strings from external documents are serialized correctly
    /// when they appear as items in a builder array.
    /// </summary>
    [Fact]
    public void WriteArrayItems_MultipleStringsFromExternalDocuments_NoExtraQuotes()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        FixedJsonValueDocument<JsonElement> doc1 =
            FixedJsonValueDocument<JsonElement>.ForString(Encoding.UTF8.GetBytes("\"HELLO\""));
        workspace.RegisterDocument(doc1);

        FixedJsonValueDocument<JsonElement> doc2 =
            FixedJsonValueDocument<JsonElement>.ForString(Encoding.UTF8.GetBytes("\"WORLD\""));
        workspace.RegisterDocument(doc2);

        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "[]".AsMemory());
        builder.RootElement.AddItem((JsonElement)doc1.RootElement);
        builder.RootElement.AddItem((JsonElement)doc2.RootElement);

        JsonElement result = builder.RootElement.Freeze();
        Assert.Equal("[\"HELLO\",\"WORLD\"]", result.GetRawText());
    }

    /// <summary>
    /// Verifies that a string from an external FixedJsonValueDocument is serialized correctly
    /// when it appears as a property value in a builder object.
    /// </summary>
    [Fact]
    public void WritePropertyValue_StringFromExternalDocument_NoExtraQuotes()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        FixedJsonValueDocument<JsonElement> stringDoc =
            FixedJsonValueDocument<JsonElement>.ForString(Encoding.UTF8.GetBytes("\"hello world\""));
        workspace.RegisterDocument(stringDoc);

        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "{}".AsMemory());
        builder.RootElement.SetProperty("greeting"u8, (JsonElement)stringDoc.RootElement);

        JsonElement result = builder.RootElement.Freeze();
        Assert.Equal("\"hello world\"", result.GetProperty("greeting").GetRawText());
    }

    /// <summary>
    /// Verifies that a number from an external FixedJsonValueDocument is serialized correctly
    /// when it appears as an array item in a builder.
    /// </summary>
    [Fact]
    public void WriteArrayItem_NumberFromExternalDocument_CorrectValue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        FixedJsonValueDocument<JsonElement> numberDoc =
            FixedJsonValueDocument<JsonElement>.ForNumber(Encoding.UTF8.GetBytes("42"));
        workspace.RegisterDocument(numberDoc);

        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "[]".AsMemory());
        builder.RootElement.AddItem((JsonElement)numberDoc.RootElement);

        JsonElement result = builder.RootElement.Freeze();
        Assert.Equal("[42]", result.GetRawText());
    }

    /// <summary>
    /// Verifies that a number from an external FixedJsonValueDocument is serialized correctly
    /// when it appears as a property value in a builder object.
    /// </summary>
    [Fact]
    public void WritePropertyValue_NumberFromExternalDocument_CorrectValue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        FixedJsonValueDocument<JsonElement> numberDoc =
            FixedJsonValueDocument<JsonElement>.ForNumber(Encoding.UTF8.GetBytes("3.14"));
        workspace.RegisterDocument(numberDoc);

        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "{}".AsMemory());
        builder.RootElement.SetProperty("value"u8, (JsonElement)numberDoc.RootElement);

        JsonElement result = builder.RootElement.Freeze();
        Assert.Equal("3.14", result.GetProperty("value").GetRawText());
    }

    /// <summary>
    /// Verifies that a mix of external string and number values in a nested structure
    /// serializes correctly — the pattern used by JSONata group-by operations.
    /// </summary>
    [Fact]
    public void WriteMixedExternalValues_NestedStructure_CorrectOutput()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // Create external values (as Jsonata would)
        FixedJsonValueDocument<JsonElement> nameDoc =
            FixedJsonValueDocument<JsonElement>.ForString(Encoding.UTF8.GetBytes("\"Alice\""));
        workspace.RegisterDocument(nameDoc);

        FixedJsonValueDocument<JsonElement> ageDoc =
            FixedJsonValueDocument<JsonElement>.ForNumber(Encoding.UTF8.GetBytes("30"));
        workspace.RegisterDocument(ageDoc);

        // Build {"name":"Alice","age":30}
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "{}".AsMemory());
        builder.RootElement.SetProperty("name"u8, (JsonElement)nameDoc.RootElement);
        builder.RootElement.SetProperty("age"u8, (JsonElement)ageDoc.RootElement);

        JsonElement result = builder.RootElement.Freeze();
        Assert.Equal("\"Alice\"", result.GetProperty("name").GetRawText());
        Assert.Equal("30", result.GetProperty("age").GetRawText());
    }

    /// <summary>
    /// Verifies that external string values survive correctly when nested inside
    /// a builder array that is itself set as a property of an object builder —
    /// the exact pattern used by JSONata group-by results.
    /// </summary>
    [Fact]
    public void WriteExternalStringsInArrayProperty_JsonataPattern()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // Create external string values (as Jsonata $uppercase would produce)
        FixedJsonValueDocument<JsonElement> val1 =
            FixedJsonValueDocument<JsonElement>.ForString(Encoding.UTF8.GetBytes("\"FOO\""));
        workspace.RegisterDocument(val1);

        FixedJsonValueDocument<JsonElement> val2 =
            FixedJsonValueDocument<JsonElement>.ForString(Encoding.UTF8.GetBytes("\"BAR\""));
        workspace.RegisterDocument(val2);

        // Build an array containing external strings
        using JsonDocumentBuilder<JsonElement.Mutable> arrBuilder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "[]".AsMemory());
        arrBuilder.RootElement.AddItem((JsonElement)val1.RootElement);
        arrBuilder.RootElement.AddItem((JsonElement)val2.RootElement);

        // Set the array as a property on an object builder
        using JsonDocumentBuilder<JsonElement.Mutable> objBuilder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "{}".AsMemory());
        objBuilder.RootElement.SetProperty("items"u8, (JsonElement)arrBuilder.RootElement);

        JsonElement result = objBuilder.RootElement.Freeze();
        Assert.Equal("{\"items\":[\"FOO\",\"BAR\"]}", result.GetRawText());
    }
}
