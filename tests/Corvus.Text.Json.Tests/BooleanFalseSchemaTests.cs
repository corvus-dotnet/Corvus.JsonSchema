// <copyright file="BooleanFalseSchemaTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for <see cref="JsonElementForBooleanFalseSchema"/> to improve coverage.
/// </summary>
[TestClass]
public class BooleanFalseSchemaTests
{
    #region Immutable - ParseValue and basic properties

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseValue_Utf8Span_Number()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        Assert.AreEqual(JsonValueKind.Number, elem.ValueKind);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseValue_CharSpan_String()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("\"hello\"".AsSpan());
        Assert.AreEqual(JsonValueKind.String, elem.ValueKind);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseValue_String_Object()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("{\"a\":1}");
        Assert.AreEqual(JsonValueKind.Object, elem.ValueKind);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseValue_Ref_Utf8JsonReader()
    {
        byte[] utf8 = "true"u8.ToArray();
        Utf8JsonReader reader = new(utf8);
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue(ref reader);
        Assert.AreEqual(JsonValueKind.True, elem.ValueKind);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void TryParseValue_Success()
    {
        byte[] utf8 = "[1,2]"u8.ToArray();
        Utf8JsonReader reader = new(utf8);
        bool ok = JsonElementForBooleanFalseSchema.TryParseValue(ref reader, out JsonElementForBooleanFalseSchema? element);
        Assert.IsTrue(ok);
        Assert.IsNotNull(element);
        Assert.AreEqual(JsonValueKind.Array, element!.Value.ValueKind);
    }

    #endregion

    #region Immutable - implicit operator int

    [TestMethod]
    [TestCategory("coverage")]
    public void ImplicitOperatorInt_Success()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        int value = elem;
        Assert.AreEqual(42, value);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ImplicitOperatorInt_NonNumber_Throws()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("\"notint\""u8);
        Assert.ThrowsExactly<InvalidOperationException>(() => { int _ = elem; });
    }

    #endregion

    #region Immutable - Equality operators

    [TestMethod]
    [TestCategory("coverage")]
    public void OperatorEquals_SameValue_ReturnsTrue()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        JsonElementForBooleanFalseSchema b = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        Assert.IsTrue(a == b);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void OperatorNotEquals_DifferentValue_ReturnsTrue()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        JsonElementForBooleanFalseSchema b = JsonElementForBooleanFalseSchema.ParseValue("99"u8);
        Assert.IsTrue(a != b);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void OperatorEquals_WithJsonElement_SameValue()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        JsonElement b = JsonElement.ParseValue("42"u8);
        Assert.IsTrue(a == b);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void OperatorNotEquals_WithJsonElement_DifferentValue()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        JsonElement b = JsonElement.ParseValue("99"u8);
        Assert.IsTrue(a != b);
    }

    #endregion

    #region Immutable - Equals, GetHashCode, ToString

    [TestMethod]
    [TestCategory("coverage")]
    public void Equals_Object_IJsonElement()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        JsonElement b = JsonElement.ParseValue("42"u8);
        // Box to force calling override Equals(object?) instead of Equals<T>(T)
        object aBoxed = a;
        Assert.IsTrue(aBoxed.Equals(b));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Equals_Object_Null_WhenNotNull()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        object aBoxed = a;
        Assert.IsFalse(aBoxed.Equals(null));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Equals_Object_Null_WhenNull()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("null"u8);
        object aBoxed = a;
        Assert.IsTrue(aBoxed.Equals(null));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Equals_Generic_SameValue()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("\"test\""u8);
        JsonElement b = JsonElement.ParseValue("\"test\""u8);
        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void From_JsonElement()
    {
        JsonElement source = JsonElement.ParseValue("\"test\""u8);
        JsonElementForBooleanFalseSchema result = JsonElementForBooleanFalseSchema.From(source);
        Assert.AreEqual(JsonValueKind.String, result.ValueKind);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ToString_ReturnsValue()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        Assert.AreEqual("42", elem.ToString());
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ToString_Default_ReturnsEmpty()
    {
        JsonElementForBooleanFalseSchema elem = default;
        Assert.AreEqual(string.Empty, elem.ToString());
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void GetHashCode_ReturnsConsistentValue()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        JsonElementForBooleanFalseSchema b = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void GetHashCode_Default_ReturnsZero()
    {
        JsonElementForBooleanFalseSchema elem = default;
        Assert.AreEqual(0, elem.GetHashCode());
    }

    #endregion

    #region Immutable - WriteTo

    [TestMethod]
    [TestCategory("coverage")]
    public void WriteTo_WritesJson()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("{\"a\":1}"u8);
        using var ms = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            elem.WriteTo(writer);
        }

        string result = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        Assert.AreEqual("{\"a\":1}", result);
    }

    #endregion

    #region Immutable - CheckValidInstance

    [TestMethod]
    [TestCategory("coverage")]
    public void WriteTo_Default_ThrowsInvalidOperationException()
    {
        JsonElementForBooleanFalseSchema elem = default;
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            using var ms = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(ms);
            elem.WriteTo(writer);
        });
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ImplicitOperatorInt_Default_ThrowsInvalidOperationException()
    {
        JsonElementForBooleanFalseSchema elem = default;
        Assert.ThrowsExactly<InvalidOperationException>(() => { int _ = elem; });
    }

    #endregion

    #region Immutable - CreateDocument

    [TestMethod]
    [TestCategory("coverage")]
    public void CreateDocument_Static_CreatesIntDocument()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        string result = builder.RootElement.ToString();
        Assert.AreEqual("42", result);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void CreateDocument_Instance_CreatesDocument()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("{\"x\":1}"u8);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder = elem.CreateDocument(workspace);
        string result = builder.RootElement.ToString();
        Assert.AreEqual("{\"x\":1}", result);
    }

    #endregion

    #region Immutable - EvaluateSchema

    [TestMethod]
    [TestCategory("coverage")]
    public void EvaluateSchema_AlwaysReturnsFalse()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        Assert.IsFalse(elem.EvaluateSchema());
    }

    #endregion

    #region Immutable - IJsonElement interface members

    [TestMethod]
    [TestCategory("coverage")]
    public void IJsonElement_ParentDocument_ReturnsDocument()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        IJsonElement ielem = elem;
        Assert.IsNotNull(ielem.ParentDocument);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void IJsonElement_ParentDocumentIndex_ReturnsIndex()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        IJsonElement ielem = elem;
        Assert.AreEqual(0, ielem.ParentDocumentIndex);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void IJsonElement_TokenType_ReturnsTokenType()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        IJsonElement ielem = elem;
        Assert.AreEqual(JsonTokenType.Number, ielem.TokenType);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void IJsonElement_ValueKind_ReturnsValueKind()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        IJsonElement ielem = elem;
        Assert.AreEqual(JsonValueKind.Number, ielem.ValueKind);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void IJsonElement_CheckValidInstance_Default_Throws()
    {
        JsonElementForBooleanFalseSchema elem = default;
        IJsonElement ielem = elem;
        Assert.ThrowsExactly<InvalidOperationException>(() => ielem.CheckValidInstance());
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Default_ValueKind_ReturnsUndefined()
    {
        JsonElementForBooleanFalseSchema elem = default;
        Assert.AreEqual(JsonValueKind.Undefined, elem.ValueKind);
    }

    #endregion

    #region Mutable - Basic operations

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_ValueKind()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        Assert.AreEqual(JsonValueKind.Number, builder.RootElement.ValueKind);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_ToString()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        Assert.AreEqual("42", builder.RootElement.ToString());
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_ToString_Default_ReturnsEmpty()
    {
        JsonElementForBooleanFalseSchema.Mutable elem = default;
        Assert.AreEqual(string.Empty, elem.ToString());
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_GetHashCode_ReturnsValue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        int hash = builder.RootElement.GetHashCode();
        // Hash of the number 42 should be consistent
        Assert.AreNotEqual(0, hash);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_GetHashCode_Default_ReturnsZero()
    {
        JsonElementForBooleanFalseSchema.Mutable elem = default;
        Assert.AreEqual(0, elem.GetHashCode());
    }

    #endregion

    #region Mutable - Equality operators

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_OperatorEquals_SameBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        JsonElementForBooleanFalseSchema.Mutable a = builder.RootElement;
        JsonElementForBooleanFalseSchema.Mutable b = builder.RootElement;
        Assert.IsTrue(a == b);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_OperatorNotEquals_DifferentValues()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> b1 =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> b2 =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 99);
        Assert.IsTrue(b1.RootElement != b2.RootElement);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_OperatorEquals_WithJsonElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        JsonElement je = JsonElement.ParseValue("42"u8);
        Assert.IsTrue(builder.RootElement == je);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_OperatorNotEquals_WithJsonElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        JsonElement je = JsonElement.ParseValue("99"u8);
        Assert.IsTrue(builder.RootElement != je);
    }

    #endregion

    #region Mutable - Equals

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_Equals_Object_IJsonElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        JsonElement je = JsonElement.ParseValue("42"u8);
        // Box to force calling override Equals(object?)
        object rootBoxed = builder.RootElement;
        Assert.IsTrue(rootBoxed.Equals(je));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_Equals_Object_Null_WhenNotNull()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        object rootBoxed = builder.RootElement;
        Assert.IsFalse(rootBoxed.Equals(null));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_Equals_Generic()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        JsonElement je = JsonElement.ParseValue("42"u8);
        Assert.IsTrue(builder.RootElement.Equals(je));
    }

    #endregion

    #region Mutable - Conversions

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_ImplicitToImmutable()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        JsonElementForBooleanFalseSchema immutable = builder.RootElement;
        Assert.AreEqual(JsonValueKind.Number, immutable.ValueKind);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_ExplicitFromImmutable_MutableDocument()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder = elem.CreateDocument(workspace);
        // The root element IS in a mutable document, so conversion should succeed
        JsonElementForBooleanFalseSchema fromBuilder = builder.RootElement;
        JsonElementForBooleanFalseSchema.Mutable mutable = (JsonElementForBooleanFalseSchema.Mutable)fromBuilder;
        Assert.AreEqual(JsonValueKind.Number, mutable.ValueKind);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_ExplicitFromImmutable_ImmutableDocument_Throws()
    {
        // An immutable ParseValue result is not mutable
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        Assert.ThrowsExactly<FormatException>(() =>
        {
            JsonElementForBooleanFalseSchema.Mutable _ = (JsonElementForBooleanFalseSchema.Mutable)elem;
        });
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_From()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            workspace.CreateBuilder<JsonElement, JsonElement.Mutable>(JsonElement.ParseValue("42"u8));
        JsonElementForBooleanFalseSchema.Mutable result =
            JsonElementForBooleanFalseSchema.Mutable.From(builder.RootElement);
        Assert.AreEqual(JsonValueKind.Number, result.ValueKind);
    }

    #endregion

    #region Mutable - WriteTo

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_WriteTo()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);

        using var ms = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            builder.RootElement.WriteTo(writer);
        }

        string result = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        Assert.AreEqual("42", result);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_WriteTo_NullWriter_Throws()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);

        Assert.ThrowsExactly<ArgumentNullException>(() => builder.RootElement.WriteTo(null!));
    }

    #endregion

    #region Mutable - CheckValidInstance

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_Default_WriteTo_Throws()
    {
        JsonElementForBooleanFalseSchema.Mutable elem = default;
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            using var ms = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(ms);
            elem.WriteTo(writer);
        });
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_IJsonElement_CheckValidInstance_Default_Throws()
    {
        JsonElementForBooleanFalseSchema.Mutable elem = default;
        IJsonElement ielem = elem;
        Assert.ThrowsExactly<InvalidOperationException>(() => ielem.CheckValidInstance());
    }

    #endregion

    #region Mutable - Clone

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_Clone()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        JsonElement cloned = builder.RootElement.Clone();
        Assert.AreEqual(JsonValueKind.Number, cloned.ValueKind);
        Assert.AreEqual("42", cloned.ToString());
    }

    #endregion

    #region Mutable - EvaluateSchema

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_EvaluateSchema_AlwaysReturnsFalse()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        Assert.IsFalse(builder.RootElement.EvaluateSchema());
    }

    #endregion

    #region Mutable - CreateBuilder

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_CreateBuilder()
    {
        // To create a builder from a mutable element, the source must be frozen first
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder = elem.CreateDocument(workspace);
        JsonElementForBooleanFalseSchema frozen = JsonElementForBooleanFalseSchema.From(builder.RootElement.Clone());
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder2 = frozen.CreateDocument(workspace);
        Assert.AreEqual("42", builder2.RootElement.ToString());
    }

    #endregion

    #region Mutable - Indexer

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_Indexer_Array()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("[10,20,30]"u8);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder = elem.CreateDocument(workspace);
        JsonElementForBooleanFalseSchema.Mutable root = builder.RootElement;
        JsonElementForBooleanFalseSchema.Mutable second = root[1];
        Assert.AreEqual("20", second.ToString());
    }

    #endregion

    #region Mutable - IJsonElement interface members

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_IJsonElement_ParentDocument()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        IJsonElement ielem = builder.RootElement;
        Assert.IsNotNull(ielem.ParentDocument);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_IJsonElement_ParentDocumentIndex()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        IJsonElement ielem = builder.RootElement;
        Assert.AreEqual(0, ielem.ParentDocumentIndex);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_IJsonElement_TokenType()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        IJsonElement ielem = builder.RootElement;
        Assert.AreEqual(JsonTokenType.Number, ielem.TokenType);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_IJsonElement_ValueKind()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        IJsonElement ielem = builder.RootElement;
        Assert.AreEqual(JsonValueKind.Number, ielem.ValueKind);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Mutable_Default_ValueKind_ReturnsUndefined()
    {
        JsonElementForBooleanFalseSchema.Mutable elem = default;
        Assert.AreEqual(JsonValueKind.Undefined, elem.ValueKind);
    }

    #endregion

    #region JsonSchema nested class - Evaluate overloads

    [TestMethod]
    [TestCategory("coverage")]
    public void JsonSchema_Evaluate_WithResultsCollector()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        IJsonElement ielem = elem;
        bool result = JsonElementForBooleanFalseSchema.JsonSchema.Evaluate(ielem.ParentDocument, ielem.ParentDocumentIndex, (IJsonSchemaResultsCollector?)null);
        Assert.IsFalse(result);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void JsonSchema_Evaluate_WithContext()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("42"u8.ToArray());
        JsonSchemaContext context = new();
        JsonElementForBooleanFalseSchema.JsonSchema.Evaluate(doc, 0, ref context);
        // The false schema always sets evaluated to false
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void JsonSchema_SchemaLocation_IsEmpty()
    {
        Assert.AreEqual(JsonElementForBooleanFalseSchema.JsonSchema.SchemaLocation, string.Empty);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void JsonSchema_SchemaLocationUtf8_IsEmpty()
    {
        Assert.AreEqual(0, JsonElementForBooleanFalseSchema.JsonSchema.SchemaLocationUtf8.Length);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void JsonSchema_SchemaLocationProvider_WritesEmpty()
    {
        Span<byte> buffer = stackalloc byte[16];
        bool result = JsonElementForBooleanFalseSchema.JsonSchema.SchemaLocationProvider(buffer, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual(0, written);
    }

    #endregion
}
