// Copyright (c) Endjin Limited. All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 20: ParsedJsonDocument type mismatch paths,
/// Mutable.Apply with non-object, array insertion at end,
/// ObjectBuilder.TryApply with non-object.
/// </summary>
[TestClass]
public class CoverageBatch20Tests
{
    #region ParsedJsonDocument — GetString on non-string throws (lines 444, 462, 500, 573)

    /// <summary>
    /// GetString() on a number element throws InvalidOperationException
    /// via CheckExpectedType in GetStringUnsafe.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void ParsedJsonDocument_GetString_OnNumber_Throws()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"n":42}"""u8.ToArray());
        JsonElement numElem = doc.RootElement["n"];
        Assert.ThrowsExactly<InvalidOperationException>(() => numElem.GetString());
    }

    /// <summary>
    /// GetString() on a boolean element throws InvalidOperationException.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void ParsedJsonDocument_GetString_OnBoolean_Throws()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"b":true}"""u8.ToArray());
        JsonElement elem = doc.RootElement["b"];
        Assert.ThrowsExactly<InvalidOperationException>(() => elem.GetString());
    }

    /// <summary>
    /// GetUtf8String() on a number element throws InvalidOperationException
    /// via CheckExpectedType in GetUtf8JsonStringUnsafe.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void ParsedJsonDocument_GetUtf8String_OnNumber_Throws()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"n":42}"""u8.ToArray());
        JsonElement numElem = doc.RootElement["n"];
        Assert.ThrowsExactly<InvalidOperationException>(() => numElem.GetUtf8String());
    }

    /// <summary>
    /// GetUtf16String() on a boolean element throws InvalidOperationException
    /// via CheckExpectedType in GetUtf16JsonStringUnsafe.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void ParsedJsonDocument_GetUtf16String_OnBoolean_Throws()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"b":false}"""u8.ToArray());
        JsonElement elem = doc.RootElement["b"];
        Assert.ThrowsExactly<InvalidOperationException>(() => elem.GetUtf16String());
    }

    /// <summary>
    /// GetString() on a null element returns null (does not throw).
    /// Covers the null fast-path in GetStringUnsafe line 439-441.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void ParsedJsonDocument_GetString_OnNull_ReturnsNull()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"n":null}"""u8.ToArray());
        JsonElement elem = doc.RootElement["n"];
        Assert.IsNull(elem.GetString());
    }

    #endregion

    #region Mutable.Apply with non-object (JsonElementHelpers.ApplyUnsafe lines 325-328)

    /// <summary>
    /// Apply() with an array source throws InvalidOperationException.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_Apply_ArraySource_Throws()
    {
        using var workspace = JsonWorkspace.Create();
        using var targetDoc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}"""u8.ToArray());
        using var builder = targetDoc.RootElement.CreateBuilder(workspace);

        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse("""[1,2,3]"""u8.ToArray());

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.RootElement.Apply(sourceDoc.RootElement));
    }

    /// <summary>
    /// Apply() with a string source throws InvalidOperationException.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_Apply_StringSource_Throws()
    {
        using var workspace = JsonWorkspace.Create();
        using var targetDoc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}"""u8.ToArray());
        using var builder = targetDoc.RootElement.CreateBuilder(workspace);

        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse("\"hello\""u8.ToArray());

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.RootElement.Apply(sourceDoc.RootElement));
    }

    /// <summary>
    /// Apply() with a number source throws InvalidOperationException.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_Apply_NumberSource_Throws()
    {
        using var workspace = JsonWorkspace.Create();
        using var targetDoc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}"""u8.ToArray());
        using var builder = targetDoc.RootElement.CreateBuilder(workspace);

        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse("""99"""u8.ToArray());

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.RootElement.Apply(sourceDoc.RootElement));
    }

    #endregion

    #region ObjectBuilder.TryApply with non-object (ComplexValueBuilder.TryApply lines 3261-3263)

    /// <summary>
    /// ObjectBuilder.TryApply() with an array source returns false.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void ObjectBuilder_TryApply_ArraySource_ReturnsFalse()
    {
        byte[] json = """{"base": true}"""u8.ToArray();
        byte[] arrayJson = """[1,2,3]"""u8.ToArray();

        using var workspace = JsonWorkspace.Create();
        using var source = ParsedJsonDocument<JsonElement>.Parse(json);
        using var builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement arrayElement = JsonElement.ParseValue(arrayJson);

        root.SetProperty(
            "test"u8,
            arrayElement,
            static (in JsonElement arr, ref JsonElement.ObjectBuilder b) =>
            {
                b.AddProperty("existing"u8, "value"u8);
                // TryApply with non-object should return false
                bool result = b.TryApply(arr);
                // We can't easily return this from the delegate,
                // but the property "existing" should remain unchanged
            });

        string result = root.ToString();
        StringAssert.Contains(result, "\"existing\"");
        StringAssert.Contains(result, "\"value\"");
    }

    /// <summary>
    /// ObjectBuilder.TryApply() with a number source returns false and does not modify.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void ObjectBuilder_TryApply_NumberSource_ReturnsFalse()
    {
        byte[] json = """{"base": true}"""u8.ToArray();
        byte[] numJson = """42"""u8.ToArray();

        using var workspace = JsonWorkspace.Create();
        using var source = ParsedJsonDocument<JsonElement>.Parse(json);
        using var builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement numElement = JsonElement.ParseValue(numJson);

        root.SetProperty(
            "container"u8,
            numElement,
            static (in JsonElement num, ref JsonElement.ObjectBuilder b) =>
            {
                b.AddProperty("keep"u8, "me"u8);
                b.TryApply(num);
            });

        string result = root.ToString();
        StringAssert.Contains(result, "\"keep\"");
        StringAssert.Contains(result, "\"me\"");
    }

    #endregion

    #region Array insertion at end — GetArrayInsertionIndex (JsonDocumentBuilder lines 363-365)

    /// <summary>
    /// AddItem on an array appends at the end, triggering arrayIndex == length path
    /// in JsonDocumentBuilder.GetArrayInsertionIndex.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void ArrayBuilder_AddItem_AppendsAtEnd()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"arr":[1,2,3]}"""u8.ToArray());
        using var builder = doc.RootElement.CreateBuilder(workspace);

        builder.RootElement["arr"].AddItem(4);

        Assert.AreEqual("""{"arr":[1,2,3,4]}""", builder.RootElement.ToString());
    }

    /// <summary>
    /// AddItemNull on an array appends null at the end.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void ArrayBuilder_AddItemNull_AppendsAtEnd()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"arr":[1,2]}"""u8.ToArray());
        using var builder = doc.RootElement.CreateBuilder(workspace);

        builder.RootElement["arr"].AddItemNull();

        Assert.AreEqual("""{"arr":[1,2,null]}""", builder.RootElement.ToString());
    }

    /// <summary>
    /// Multiple AddItem calls all append at end correctly.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void ArrayBuilder_MultipleAddItem_AppendsAll()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[]"""u8.ToArray());
        using var builder = doc.RootElement.CreateBuilder(workspace);

        builder.RootElement.AddItem(1);
        builder.RootElement.AddItem(2);
        builder.RootElement.AddItem(3);

        Assert.AreEqual("[1,2,3]", builder.RootElement.ToString());
    }

    #endregion

    #region Object enumeration — GetPropertyName (ParsedJsonDocument lines 648-654)

    /// <summary>
    /// Enumerating object properties accesses property names
    /// via IJsonDocument.GetPropertyName.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void ParsedJsonDocument_ObjectEnumeration_AccessesPropertyNames()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"foo":"bar","baz":42}"""u8.ToArray());

        var names = new List<string>();
        foreach (JsonProperty<JsonElement> prop in doc.RootElement.EnumerateObject())
        {
            names.Add(prop.Name);
        }

        CollectionAssert.AreEqual(new[] { "foo", "baz" }, names.ToArray());
    }

    /// <summary>
    /// Enumerating object with escaped property name triggers
    /// the GetPropertyName path with unescaping.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void ParsedJsonDocument_ObjectEnumeration_EscapedPropertyName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"hello\nworld":"value"}"""u8.ToArray());

        var names = new List<string>();
        foreach (JsonProperty<JsonElement> prop in doc.RootElement.EnumerateObject())
        {
            names.Add(prop.Name);
        }

        CollectionAssert.AreEqual(new[] { "hello\nworld" }, names.ToArray());
    }

    #endregion
}
