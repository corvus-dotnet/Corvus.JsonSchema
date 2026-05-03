// <copyright file="BooleanFalseSchemaTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for <see cref="JsonElementForBooleanFalseSchema"/> to improve coverage.
/// </summary>
public static class BooleanFalseSchemaTests
{
    #region Immutable - ParseValue and basic properties

    [Fact]
    [Trait("category", "coverage")]
    public static void ParseValue_Utf8Span_Number()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        Assert.Equal(JsonValueKind.Number, elem.ValueKind);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ParseValue_CharSpan_String()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("\"hello\"".AsSpan());
        Assert.Equal(JsonValueKind.String, elem.ValueKind);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ParseValue_String_Object()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("{\"a\":1}");
        Assert.Equal(JsonValueKind.Object, elem.ValueKind);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ParseValue_Ref_Utf8JsonReader()
    {
        byte[] utf8 = "true"u8.ToArray();
        Utf8JsonReader reader = new(utf8);
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue(ref reader);
        Assert.Equal(JsonValueKind.True, elem.ValueKind);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void TryParseValue_Success()
    {
        byte[] utf8 = "[1,2]"u8.ToArray();
        Utf8JsonReader reader = new(utf8);
        bool ok = JsonElementForBooleanFalseSchema.TryParseValue(ref reader, out JsonElementForBooleanFalseSchema? element);
        Assert.True(ok);
        Assert.NotNull(element);
        Assert.Equal(JsonValueKind.Array, element!.Value.ValueKind);
    }

    #endregion

    #region Immutable - implicit operator int

    [Fact]
    [Trait("category", "coverage")]
    public static void ImplicitOperatorInt_Success()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        int value = elem;
        Assert.Equal(42, value);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ImplicitOperatorInt_NonNumber_Throws()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("\"notint\""u8);
        Assert.Throws<InvalidOperationException>(() => { int _ = elem; });
    }

    #endregion

    #region Immutable - Equality operators

    [Fact]
    [Trait("category", "coverage")]
    public static void OperatorEquals_SameValue_ReturnsTrue()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        JsonElementForBooleanFalseSchema b = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        Assert.True(a == b);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void OperatorNotEquals_DifferentValue_ReturnsTrue()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        JsonElementForBooleanFalseSchema b = JsonElementForBooleanFalseSchema.ParseValue("99"u8);
        Assert.True(a != b);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void OperatorEquals_WithJsonElement_SameValue()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        JsonElement b = JsonElement.ParseValue("42"u8);
        Assert.True(a == b);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void OperatorNotEquals_WithJsonElement_DifferentValue()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        JsonElement b = JsonElement.ParseValue("99"u8);
        Assert.True(a != b);
    }

    #endregion

    #region Immutable - Equals, GetHashCode, ToString

    [Fact]
    [Trait("category", "coverage")]
    public static void Equals_Object_IJsonElement()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        JsonElement b = JsonElement.ParseValue("42"u8);
        // Box to force calling override Equals(object?) instead of Equals<T>(T)
        object aBoxed = a;
        Assert.True(aBoxed.Equals(b));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Equals_Object_Null_WhenNotNull()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        object aBoxed = a;
        Assert.False(aBoxed.Equals(null));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Equals_Object_Null_WhenNull()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("null"u8);
        object aBoxed = a;
        Assert.True(aBoxed.Equals(null));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Equals_Generic_SameValue()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("\"test\""u8);
        JsonElement b = JsonElement.ParseValue("\"test\""u8);
        Assert.True(a.Equals(b));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void From_JsonElement()
    {
        JsonElement source = JsonElement.ParseValue("\"test\""u8);
        JsonElementForBooleanFalseSchema result = JsonElementForBooleanFalseSchema.From(source);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ToString_ReturnsValue()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        Assert.Equal("42", elem.ToString());
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ToString_Default_ReturnsEmpty()
    {
        JsonElementForBooleanFalseSchema elem = default;
        Assert.Equal(string.Empty, elem.ToString());
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void GetHashCode_ReturnsConsistentValue()
    {
        JsonElementForBooleanFalseSchema a = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        JsonElementForBooleanFalseSchema b = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void GetHashCode_Default_ReturnsZero()
    {
        JsonElementForBooleanFalseSchema elem = default;
        Assert.Equal(0, elem.GetHashCode());
    }

    #endregion

    #region Immutable - WriteTo

    [Fact]
    [Trait("category", "coverage")]
    public static void WriteTo_WritesJson()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("{\"a\":1}"u8);
        using var ms = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            elem.WriteTo(writer);
        }

        string result = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal("{\"a\":1}", result);
    }

    #endregion

    #region Immutable - CheckValidInstance

    [Fact]
    [Trait("category", "coverage")]
    public static void WriteTo_Default_ThrowsInvalidOperationException()
    {
        JsonElementForBooleanFalseSchema elem = default;
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var ms = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(ms);
            elem.WriteTo(writer);
        });
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ImplicitOperatorInt_Default_ThrowsInvalidOperationException()
    {
        JsonElementForBooleanFalseSchema elem = default;
        Assert.Throws<InvalidOperationException>(() => { int _ = elem; });
    }

    #endregion

    #region Immutable - CreateDocument

    [Fact]
    [Trait("category", "coverage")]
    public static void CreateDocument_Static_CreatesIntDocument()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        string result = builder.RootElement.ToString();
        Assert.Equal("42", result);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void CreateDocument_Instance_CreatesDocument()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("{\"x\":1}"u8);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder = elem.CreateDocument(workspace);
        string result = builder.RootElement.ToString();
        Assert.Equal("{\"x\":1}", result);
    }

    #endregion

    #region Immutable - EvaluateSchema

    [Fact]
    [Trait("category", "coverage")]
    public static void EvaluateSchema_AlwaysReturnsFalse()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        Assert.False(elem.EvaluateSchema());
    }

    #endregion

    #region Immutable - IJsonElement interface members

    [Fact]
    [Trait("category", "coverage")]
    public static void IJsonElement_ParentDocument_ReturnsDocument()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        IJsonElement ielem = elem;
        Assert.NotNull(ielem.ParentDocument);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void IJsonElement_ParentDocumentIndex_ReturnsIndex()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        IJsonElement ielem = elem;
        Assert.Equal(0, ielem.ParentDocumentIndex);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void IJsonElement_TokenType_ReturnsTokenType()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        IJsonElement ielem = elem;
        Assert.Equal(JsonTokenType.Number, ielem.TokenType);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void IJsonElement_ValueKind_ReturnsValueKind()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        IJsonElement ielem = elem;
        Assert.Equal(JsonValueKind.Number, ielem.ValueKind);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void IJsonElement_CheckValidInstance_Default_Throws()
    {
        JsonElementForBooleanFalseSchema elem = default;
        IJsonElement ielem = elem;
        Assert.Throws<InvalidOperationException>(() => ielem.CheckValidInstance());
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Default_ValueKind_ReturnsUndefined()
    {
        JsonElementForBooleanFalseSchema elem = default;
        Assert.Equal(JsonValueKind.Undefined, elem.ValueKind);
    }

    #endregion

    #region Mutable - Basic operations

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_ValueKind()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        Assert.Equal(JsonValueKind.Number, builder.RootElement.ValueKind);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_ToString()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        Assert.Equal("42", builder.RootElement.ToString());
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_ToString_Default_ReturnsEmpty()
    {
        JsonElementForBooleanFalseSchema.Mutable elem = default;
        Assert.Equal(string.Empty, elem.ToString());
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_GetHashCode_ReturnsValue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        int hash = builder.RootElement.GetHashCode();
        // Hash of the number 42 should be consistent
        Assert.NotEqual(0, hash);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_GetHashCode_Default_ReturnsZero()
    {
        JsonElementForBooleanFalseSchema.Mutable elem = default;
        Assert.Equal(0, elem.GetHashCode());
    }

    #endregion

    #region Mutable - Equality operators

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_OperatorEquals_SameBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        JsonElementForBooleanFalseSchema.Mutable a = builder.RootElement;
        JsonElementForBooleanFalseSchema.Mutable b = builder.RootElement;
        Assert.True(a == b);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_OperatorNotEquals_DifferentValues()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> b1 =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> b2 =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 99);
        Assert.True(b1.RootElement != b2.RootElement);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_OperatorEquals_WithJsonElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        JsonElement je = JsonElement.ParseValue("42"u8);
        Assert.True(builder.RootElement == je);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_OperatorNotEquals_WithJsonElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        JsonElement je = JsonElement.ParseValue("99"u8);
        Assert.True(builder.RootElement != je);
    }

    #endregion

    #region Mutable - Equals

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_Equals_Object_IJsonElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        JsonElement je = JsonElement.ParseValue("42"u8);
        // Box to force calling override Equals(object?)
        object rootBoxed = builder.RootElement;
        Assert.True(rootBoxed.Equals(je));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_Equals_Object_Null_WhenNotNull()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        object rootBoxed = builder.RootElement;
        Assert.False(rootBoxed.Equals(null));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_Equals_Generic()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        JsonElement je = JsonElement.ParseValue("42"u8);
        Assert.True(builder.RootElement.Equals(je));
    }

    #endregion

    #region Mutable - Conversions

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_ImplicitToImmutable()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        JsonElementForBooleanFalseSchema immutable = builder.RootElement;
        Assert.Equal(JsonValueKind.Number, immutable.ValueKind);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_ExplicitFromImmutable_MutableDocument()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder = elem.CreateDocument(workspace);
        // The root element IS in a mutable document, so conversion should succeed
        JsonElementForBooleanFalseSchema fromBuilder = builder.RootElement;
        JsonElementForBooleanFalseSchema.Mutable mutable = (JsonElementForBooleanFalseSchema.Mutable)fromBuilder;
        Assert.Equal(JsonValueKind.Number, mutable.ValueKind);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_ExplicitFromImmutable_ImmutableDocument_Throws()
    {
        // An immutable ParseValue result is not mutable
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        Assert.Throws<FormatException>(() =>
        {
            JsonElementForBooleanFalseSchema.Mutable _ = (JsonElementForBooleanFalseSchema.Mutable)elem;
        });
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_From()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            workspace.CreateBuilder<JsonElement, JsonElement.Mutable>(JsonElement.ParseValue("42"u8));
        JsonElementForBooleanFalseSchema.Mutable result =
            JsonElementForBooleanFalseSchema.Mutable.From(builder.RootElement);
        Assert.Equal(JsonValueKind.Number, result.ValueKind);
    }

    #endregion

    #region Mutable - WriteTo

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_WriteTo()
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
        Assert.Equal("42", result);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_WriteTo_NullWriter_Throws()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);

        Assert.Throws<ArgumentNullException>(() => builder.RootElement.WriteTo(null!));
    }

    #endregion

    #region Mutable - CheckValidInstance

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_Default_WriteTo_Throws()
    {
        JsonElementForBooleanFalseSchema.Mutable elem = default;
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var ms = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(ms);
            elem.WriteTo(writer);
        });
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_IJsonElement_CheckValidInstance_Default_Throws()
    {
        JsonElementForBooleanFalseSchema.Mutable elem = default;
        IJsonElement ielem = elem;
        Assert.Throws<InvalidOperationException>(() => ielem.CheckValidInstance());
    }

    #endregion

    #region Mutable - Clone

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_Clone()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        JsonElement cloned = builder.RootElement.Clone();
        Assert.Equal(JsonValueKind.Number, cloned.ValueKind);
        Assert.Equal("42", cloned.ToString());
    }

    #endregion

    #region Mutable - EvaluateSchema

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_EvaluateSchema_AlwaysReturnsFalse()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        Assert.False(builder.RootElement.EvaluateSchema());
    }

    #endregion

    #region Mutable - CreateBuilder

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_CreateBuilder()
    {
        // To create a builder from a mutable element, the source must be frozen first
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder = elem.CreateDocument(workspace);
        JsonElementForBooleanFalseSchema frozen = JsonElementForBooleanFalseSchema.From(builder.RootElement.Clone());
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder2 = frozen.CreateDocument(workspace);
        Assert.Equal("42", builder2.RootElement.ToString());
    }

    #endregion

    #region Mutable - Indexer

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_Indexer_Array()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("[10,20,30]"u8);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder = elem.CreateDocument(workspace);
        JsonElementForBooleanFalseSchema.Mutable root = builder.RootElement;
        JsonElementForBooleanFalseSchema.Mutable second = root[1];
        Assert.Equal("20", second.ToString());
    }

    #endregion

    #region Mutable - IJsonElement interface members

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_IJsonElement_ParentDocument()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        IJsonElement ielem = builder.RootElement;
        Assert.NotNull(ielem.ParentDocument);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_IJsonElement_ParentDocumentIndex()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        IJsonElement ielem = builder.RootElement;
        Assert.Equal(0, ielem.ParentDocumentIndex);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_IJsonElement_TokenType()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        IJsonElement ielem = builder.RootElement;
        Assert.Equal(JsonTokenType.Number, ielem.TokenType);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_IJsonElement_ValueKind()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElementForBooleanFalseSchema.Mutable> builder =
            JsonElementForBooleanFalseSchema.CreateDocument(workspace, 42);
        IJsonElement ielem = builder.RootElement;
        Assert.Equal(JsonValueKind.Number, ielem.ValueKind);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void Mutable_Default_ValueKind_ReturnsUndefined()
    {
        JsonElementForBooleanFalseSchema.Mutable elem = default;
        Assert.Equal(JsonValueKind.Undefined, elem.ValueKind);
    }

    #endregion

    #region JsonSchema nested class - Evaluate overloads

    [Fact]
    [Trait("category", "coverage")]
    public static void JsonSchema_Evaluate_WithResultsCollector()
    {
        JsonElementForBooleanFalseSchema elem = JsonElementForBooleanFalseSchema.ParseValue("42"u8);
        IJsonElement ielem = elem;
        bool result = JsonElementForBooleanFalseSchema.JsonSchema.Evaluate(ielem.ParentDocument, ielem.ParentDocumentIndex, (IJsonSchemaResultsCollector?)null);
        Assert.False(result);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void JsonSchema_Evaluate_WithContext()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("42"u8.ToArray());
        JsonSchemaContext context = new();
        JsonElementForBooleanFalseSchema.JsonSchema.Evaluate(doc, 0, ref context);
        // The false schema always sets evaluated to false
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void JsonSchema_SchemaLocation_IsEmpty()
    {
        Assert.Equal(JsonElementForBooleanFalseSchema.JsonSchema.SchemaLocation, string.Empty);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void JsonSchema_SchemaLocationUtf8_IsEmpty()
    {
        Assert.Equal(0, JsonElementForBooleanFalseSchema.JsonSchema.SchemaLocationUtf8.Length);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void JsonSchema_SchemaLocationProvider_WritesEmpty()
    {
        Span<byte> buffer = stackalloc byte[16];
        bool result = JsonElementForBooleanFalseSchema.JsonSchema.SchemaLocationProvider(buffer, out int written);
        Assert.True(result);
        Assert.Equal(0, written);
    }

    #endregion
}
