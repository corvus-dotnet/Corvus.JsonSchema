// <copyright file="SchemaClassifierTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

[TestClass]
public class SchemaClassifierTests
{
    private static JsonElement ParseSchema(string json)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        return doc.RootElement.Clone();
    }

    [TestMethod]
    public void Classify_NoTypeProperty_ReturnsString()
    {
        JsonElement schema = ParseSchema("""{ "description": "no type" }""");
        Assert.AreEqual(
            ParameterSerializationKind.String,
            SchemaClassifier.Classify(schema));
    }

    [TestMethod]
    public void Classify_TypeNotString_ReturnsString()
    {
        JsonElement schema = ParseSchema("""{ "type": ["string", "null"] }""");
        Assert.AreEqual(
            ParameterSerializationKind.String,
            SchemaClassifier.Classify(schema));
    }

    [TestMethod]
    public void Classify_StringType_ReturnsString()
    {
        JsonElement schema = ParseSchema("""{ "type": "string" }""");
        Assert.AreEqual(
            ParameterSerializationKind.String,
            SchemaClassifier.Classify(schema));
    }

    [TestMethod]
    public void Classify_BooleanType_ReturnsBoolean()
    {
        JsonElement schema = ParseSchema("""{ "type": "boolean" }""");
        Assert.AreEqual(
            ParameterSerializationKind.Boolean,
            SchemaClassifier.Classify(schema));
    }

    [TestMethod]
    public void Classify_ObjectType_ReturnsObject()
    {
        JsonElement schema = ParseSchema("""{ "type": "object" }""");
        Assert.AreEqual(
            ParameterSerializationKind.Object,
            SchemaClassifier.Classify(schema));
    }

    [TestMethod]
    public void Classify_ArrayType_ReturnsArray()
    {
        JsonElement schema = ParseSchema("""{ "type": "array" }""");
        Assert.AreEqual(
            ParameterSerializationKind.Array,
            SchemaClassifier.Classify(schema));
    }

    [TestMethod]
    public void Classify_UnknownType_ReturnsString()
    {
        JsonElement schema = ParseSchema("""{ "type": "null" }""");
        Assert.AreEqual(
            ParameterSerializationKind.String,
            SchemaClassifier.Classify(schema));
    }

    [TestMethod]
    [DataRow("byte", ParameterSerializationKind.Byte)]
    [DataRow("uint16", ParameterSerializationKind.UInt16)]
    [DataRow("uint32", ParameterSerializationKind.UInt32)]
    [DataRow("uint64", ParameterSerializationKind.UInt64)]
    [DataRow("uint128", ParameterSerializationKind.UInt128)]
    [DataRow("sbyte", ParameterSerializationKind.SByte)]
    [DataRow("int16", ParameterSerializationKind.Int16)]
    [DataRow("int32", ParameterSerializationKind.Int32)]
    [DataRow("int64", ParameterSerializationKind.Int64)]
    [DataRow("int128", ParameterSerializationKind.Int128)]
    public void Classify_IntegerFormat_ReturnsExpected(string format, ParameterSerializationKind expected)
    {
        JsonElement schema = ParseSchema($$"""{ "type": "integer", "format": "{{format}}" }""");
        Assert.AreEqual(expected, SchemaClassifier.Classify(schema));
    }

    [TestMethod]
    public void Classify_IntegerNoFormat_ReturnsUnboundedNumber()
    {
        JsonElement schema = ParseSchema("""{ "type": "integer" }""");
        Assert.AreEqual(
            ParameterSerializationKind.UnboundedNumber,
            SchemaClassifier.Classify(schema));
    }

    [TestMethod]
    public void Classify_IntegerUnknownFormat_ReturnsUnboundedNumber()
    {
        JsonElement schema = ParseSchema("""{ "type": "integer", "format": "bigint" }""");
        Assert.AreEqual(
            ParameterSerializationKind.UnboundedNumber,
            SchemaClassifier.Classify(schema));
    }

    [TestMethod]
    [DataRow("half", ParameterSerializationKind.Half)]
    [DataRow("single", ParameterSerializationKind.Single)]
    [DataRow("float", ParameterSerializationKind.Single)]
    [DataRow("double", ParameterSerializationKind.Double)]
    [DataRow("decimal", ParameterSerializationKind.Decimal)]
    public void Classify_NumberFormat_ReturnsExpected(string format, ParameterSerializationKind expected)
    {
        JsonElement schema = ParseSchema($$"""{ "type": "number", "format": "{{format}}" }""");
        Assert.AreEqual(expected, SchemaClassifier.Classify(schema));
    }

    [TestMethod]
    public void Classify_NumberNoFormat_ReturnsUnboundedNumber()
    {
        JsonElement schema = ParseSchema("""{ "type": "number" }""");
        Assert.AreEqual(
            ParameterSerializationKind.UnboundedNumber,
            SchemaClassifier.Classify(schema));
    }

    [TestMethod]
    public void Classify_NumberUnknownFormat_ReturnsUnboundedNumber()
    {
        JsonElement schema = ParseSchema("""{ "type": "number", "format": "currency" }""");
        Assert.AreEqual(
            ParameterSerializationKind.UnboundedNumber,
            SchemaClassifier.Classify(schema));
    }

    [TestMethod]
    [DataRow(ParameterSerializationKind.Boolean, 5)]
    [DataRow(ParameterSerializationKind.Byte, 3)]
    [DataRow(ParameterSerializationKind.UInt16, 5)]
    [DataRow(ParameterSerializationKind.UInt32, 10)]
    [DataRow(ParameterSerializationKind.UInt64, 20)]
    [DataRow(ParameterSerializationKind.UInt128, 39)]
    [DataRow(ParameterSerializationKind.SByte, 4)]
    [DataRow(ParameterSerializationKind.Int16, 6)]
    [DataRow(ParameterSerializationKind.Int32, 11)]
    [DataRow(ParameterSerializationKind.Int64, 20)]
    [DataRow(ParameterSerializationKind.Int128, 40)]
    [DataRow(ParameterSerializationKind.Half, 16)]
    [DataRow(ParameterSerializationKind.Single, 32)]
    [DataRow(ParameterSerializationKind.Double, 32)]
    [DataRow(ParameterSerializationKind.Decimal, 32)]
    public void GetMaxFormattedSize_BoundedKind_ReturnsExpected(
        ParameterSerializationKind kind, int expected)
    {
        Assert.AreEqual(expected, SchemaClassifier.GetMaxFormattedSize(kind));
    }

    [TestMethod]
    [DataRow(ParameterSerializationKind.String)]
    [DataRow(ParameterSerializationKind.UnboundedNumber)]
    [DataRow(ParameterSerializationKind.Object)]
    [DataRow(ParameterSerializationKind.Array)]
    public void GetMaxFormattedSize_UnboundedKind_ReturnsZero(ParameterSerializationKind kind)
    {
        Assert.AreEqual(0, SchemaClassifier.GetMaxFormattedSize(kind));
    }

    [TestMethod]
    [DataRow(ParameterSerializationKind.Boolean)]
    [DataRow(ParameterSerializationKind.Byte)]
    [DataRow(ParameterSerializationKind.UInt16)]
    [DataRow(ParameterSerializationKind.UInt32)]
    [DataRow(ParameterSerializationKind.UInt64)]
    [DataRow(ParameterSerializationKind.UInt128)]
    [DataRow(ParameterSerializationKind.SByte)]
    [DataRow(ParameterSerializationKind.Int16)]
    [DataRow(ParameterSerializationKind.Int32)]
    [DataRow(ParameterSerializationKind.Int64)]
    [DataRow(ParameterSerializationKind.Int128)]
    [DataRow(ParameterSerializationKind.Half)]
    [DataRow(ParameterSerializationKind.Single)]
    [DataRow(ParameterSerializationKind.Double)]
    [DataRow(ParameterSerializationKind.Decimal)]
    public void IsFormattable_NumericOrBoolean_ReturnsTrue(ParameterSerializationKind kind)
    {
        Assert.IsTrue(SchemaClassifier.IsFormattable(kind));
    }

    [TestMethod]
    [DataRow(ParameterSerializationKind.String)]
    [DataRow(ParameterSerializationKind.UnboundedNumber)]
    [DataRow(ParameterSerializationKind.Object)]
    [DataRow(ParameterSerializationKind.Array)]
    public void IsFormattable_NonNumeric_ReturnsFalse(ParameterSerializationKind kind)
    {
        Assert.IsFalse(SchemaClassifier.IsFormattable(kind));
    }

    [TestMethod]
    public void ClassifyArrayElement_HasItems_ReturnsItemKind()
    {
        JsonElement schema = ParseSchema("""{ "type": "array", "items": { "type": "integer", "format": "int32" } }""");
        Assert.AreEqual(
            ParameterSerializationKind.Int32,
            SchemaClassifier.ClassifyArrayElement(schema));
    }

    [TestMethod]
    public void ClassifyArrayElement_NoItems_ReturnsString()
    {
        JsonElement schema = ParseSchema("""{ "type": "array" }""");
        Assert.AreEqual(
            ParameterSerializationKind.String,
            SchemaClassifier.ClassifyArrayElement(schema));
    }

    [TestMethod]
    public void ClassifyArrayElement_ItemsNotObject_ReturnsString()
    {
        JsonElement schema = ParseSchema("""{ "type": "array", "items": true }""");
        Assert.AreEqual(
            ParameterSerializationKind.String,
            SchemaClassifier.ClassifyArrayElement(schema));
    }

    [TestMethod]
    public void ClassifyObjectValue_HasAdditionalProperties_ReturnsValueKind()
    {
        JsonElement schema = ParseSchema("""{ "type": "object", "additionalProperties": { "type": "number", "format": "double" } }""");
        Assert.AreEqual(
            ParameterSerializationKind.Double,
            SchemaClassifier.ClassifyObjectValue(schema));
    }

    [TestMethod]
    public void ClassifyObjectValue_NoAdditionalProperties_ReturnsString()
    {
        JsonElement schema = ParseSchema("""{ "type": "object" }""");
        Assert.AreEqual(
            ParameterSerializationKind.String,
            SchemaClassifier.ClassifyObjectValue(schema));
    }

    [TestMethod]
    public void ClassifyObjectValue_AdditionalPropertiesNotObject_ReturnsString()
    {
        JsonElement schema = ParseSchema("""{ "type": "object", "additionalProperties": true }""");
        Assert.AreEqual(
            ParameterSerializationKind.String,
            SchemaClassifier.ClassifyObjectValue(schema));
    }

    [TestMethod]
    public void Classify_IntegerFormatNotString_ReturnsUnboundedNumber()
    {
        JsonElement schema = ParseSchema("""{ "type": "integer", "format": 42 }""");
        Assert.AreEqual(
            ParameterSerializationKind.UnboundedNumber,
            SchemaClassifier.Classify(schema));
    }

    [TestMethod]
    public void Classify_NumberFormatNotString_ReturnsUnboundedNumber()
    {
        JsonElement schema = ParseSchema("""{ "type": "number", "format": true }""");
        Assert.AreEqual(
            ParameterSerializationKind.UnboundedNumber,
            SchemaClassifier.Classify(schema));
    }
}