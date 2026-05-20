// <copyright file="CodeEmitHelpersTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

[TestClass]
public class CodeEmitHelpersTests
{
    [TestMethod]
    public void SanitizeIdentifier_SimpleAlpha_ReturnsPascalCase()
    {
        Assert.AreEqual("MyName", CodeEmitHelpers.SanitizeIdentifier("my_name"));
    }

    [TestMethod]
    public void SanitizeIdentifier_SpecialChars_StripsNonAlphanumeric()
    {
        // e.g. "X-Rate@Limit!" → "XRateLimit"
        Assert.AreEqual("XRateLimit", CodeEmitHelpers.SanitizeIdentifier("X-Rate@Limit!"));
    }

    [TestMethod]
    public void SanitizeIdentifier_Hyphens_PascalCases()
    {
        Assert.AreEqual("PetId", CodeEmitHelpers.SanitizeIdentifier("pet-id"));
    }

    [TestMethod]
    public void SanitizeIdentifier_Dots_PascalCases()
    {
        Assert.AreEqual("FooBar", CodeEmitHelpers.SanitizeIdentifier("foo.bar"));
    }

    [TestMethod]
    public void SanitizeParameterName_NeedsSanitizing_ReturnsCamelCase()
    {
        Assert.AreEqual("petId", CodeEmitHelpers.SanitizeParameterName("pet-id"));
    }

    [TestMethod]
    public void SanitizeParameterName_UnderscoreSeparated_ReturnsAsIs()
    {
        Assert.AreEqual("pet_id", CodeEmitHelpers.SanitizeParameterName("pet_id"));
    }

    [TestMethod]
    public void SanitizeParameterName_AlreadyValid_ReturnsSame()
    {
        Assert.AreEqual("limit", CodeEmitHelpers.SanitizeParameterName("limit"));
    }

    [TestMethod]
    public void SanitizeParameterName_StartsWithDigit_SanitizesToCamelCase()
    {
        // Starts with non-letter → needs sanitizing
        string result = CodeEmitHelpers.SanitizeParameterName("1foo");
        Assert.AreEqual("1foo", result);
    }

    [TestMethod]
    public void SanitizeParameterName_AllSpecialChars_ReturnsFallback()
    {
        // All characters stripped → empty → returns "_param"
        Assert.AreEqual("_param", CodeEmitHelpers.SanitizeParameterName("@#$"));
    }

    [TestMethod]
    public void SanitizeParameterName_Empty_ReturnsFallback()
    {
        Assert.AreEqual("_param", CodeEmitHelpers.SanitizeParameterName(""));
    }

    [TestMethod]
    public void SanitizeParameterName_StartsWithUnderscore_ReturnsSame()
    {
        // Starts with underscore — valid, does not need sanitizing
        Assert.AreEqual("_id", CodeEmitHelpers.SanitizeParameterName("_id"));
    }

    [TestMethod]
    public void SanitizeParameterName_ContainsInvalidChar_NeedsSanitizing()
    {
        // Contains hyphen → needs sanitizing
        Assert.AreEqual("myParam", CodeEmitHelpers.SanitizeParameterName("my-param"));
    }

    [TestMethod]
    public void EscapeCSharpKeyword_ReservedWord_PrefixesAt()
    {
        Assert.AreEqual("@class", CodeEmitHelpers.EscapeCSharpKeyword("class"));
    }

    [TestMethod]
    public void EscapeCSharpKeyword_NotReserved_ReturnsSame()
    {
        Assert.AreEqual("petId", CodeEmitHelpers.EscapeCSharpKeyword("petId"));
    }

    [TestMethod]
    [DataRow(OperationMethod.Get, "OperationMethod.Get")]
    [DataRow(OperationMethod.Post, "OperationMethod.Post")]
    [DataRow(OperationMethod.Put, "OperationMethod.Put")]
    [DataRow(OperationMethod.Delete, "OperationMethod.Delete")]
    [DataRow(OperationMethod.Patch, "OperationMethod.Patch")]
    [DataRow(OperationMethod.Head, "OperationMethod.Head")]
    [DataRow(OperationMethod.Options, "OperationMethod.Options")]
    [DataRow(OperationMethod.Trace, "OperationMethod.Trace")]
    public void OperationMethodExpression_KnownMethod_ReturnsExpected(
        OperationMethod method, string expected)
    {
        Assert.AreEqual(expected, CodeEmitHelpers.OperationMethodExpression(method));
    }

    [TestMethod]
    public void OperationMethodExpression_UnknownMethod_ReturnsCast()
    {
        Assert.AreEqual("(OperationMethod)99", CodeEmitHelpers.OperationMethodExpression((OperationMethod)99));
    }

    [TestMethod]
    [DataRow("200", "Ok")]
    [DataRow("201", "Created")]
    [DataRow("202", "Accepted")]
    [DataRow("204", "NoContent")]
    [DataRow("301", "MovedPermanently")]
    [DataRow("304", "NotModified")]
    [DataRow("400", "BadRequest")]
    [DataRow("401", "Unauthorized")]
    [DataRow("403", "Forbidden")]
    [DataRow("404", "NotFound")]
    [DataRow("405", "MethodNotAllowed")]
    [DataRow("409", "Conflict")]
    [DataRow("422", "UnprocessableEntity")]
    [DataRow("429", "TooManyRequests")]
    [DataRow("500", "InternalServerError")]
    [DataRow("502", "BadGateway")]
    [DataRow("503", "ServiceUnavailable")]
    [DataRow("default", "Default")]
    public void StatusCodeToName_KnownCode_ReturnsExpected(string code, string expected)
    {
        Assert.AreEqual(expected, CodeEmitHelpers.StatusCodeToName(code));
    }

    [TestMethod]
    public void StatusCodeToName_WildcardCode_ReturnsStatusNxx()
    {
        Assert.AreEqual("Status2xx", CodeEmitHelpers.StatusCodeToName("2XX"));
    }

    [TestMethod]
    public void StatusCodeToName_UnknownCode_ReturnsStatusNNN()
    {
        Assert.AreEqual("Status418", CodeEmitHelpers.StatusCodeToName("418"));
    }

    [TestMethod]
    public void ToCamelCase_Empty_ReturnsEmpty()
    {
        Assert.AreEqual("", CodeEmitHelpers.ToCamelCase(""));
    }

    [TestMethod]
    public void ToCamelCase_PascalCase_ReturnsCamelCase()
    {
        Assert.AreEqual("listPets", CodeEmitHelpers.ToCamelCase("ListPets"));
    }

    [TestMethod]
    public void EscapeXml_AmpersandAndAngles_Escapes()
    {
        Assert.AreEqual("a &amp; b &lt;c&gt;", CodeEmitHelpers.EscapeXml("a & b <c>"));
    }

    [TestMethod]
    public void EscapeXml_NoSpecialChars_ReturnsSame()
    {
        Assert.AreEqual("hello world", CodeEmitHelpers.EscapeXml("hello world"));
    }

    [TestMethod]
    public void HeaderNameToPropertyName_HyphenatedHeader_ReturnsPascalCase()
    {
        Assert.AreEqual("XRateLimit", CodeEmitHelpers.HeaderNameToPropertyName("X-Rate-Limit"));
    }

    [TestMethod]
    public void HeaderNameToPropertyName_SimpleHeader_ReturnsPascalCase()
    {
        Assert.AreEqual("ETag", CodeEmitHelpers.HeaderNameToPropertyName("eTag"));
    }

    [TestMethod]
    public void TryFormatBufferSize_BoundedKind_ReturnsSize()
    {
        Assert.AreEqual(11, CodeEmitHelpers.TryFormatBufferSize(ParameterSerializationKind.Int32));
    }

    [TestMethod]
    public void TryFormatBufferSize_UnboundedKind_ReturnsMinusOne()
    {
        Assert.AreEqual(-1, CodeEmitHelpers.TryFormatBufferSize(ParameterSerializationKind.String));
    }

    [TestMethod]
    [DataRow(ParameterSerializationKind.String)]
    [DataRow(ParameterSerializationKind.Boolean)]
    [DataRow(ParameterSerializationKind.Int32)]
    [DataRow(ParameterSerializationKind.Double)]
    [DataRow(ParameterSerializationKind.UnboundedNumber)]
    [DataRow(ParameterSerializationKind.Object)]
    public void EmitPathParamWrite_AllKindCategories_ProducesOutput(ParameterSerializationKind kind)
    {
        IndentedWriter w = new();
        CodeEmitHelpers.EmitPathParamWrite(w, "color", "value", "p0", kind, ParameterStyle.Simple, false);
        Assert.IsTrue(w.ToString().Length > 0, $"EmitPathParamWrite should produce output for {kind}");
    }

    [TestMethod]
    [DataRow(ParameterSerializationKind.String)]
    [DataRow(ParameterSerializationKind.Boolean)]
    [DataRow(ParameterSerializationKind.Int32)]
    [DataRow(ParameterSerializationKind.Double)]
    [DataRow(ParameterSerializationKind.UnboundedNumber)]
    [DataRow(ParameterSerializationKind.Object)]
    public void EmitQueryScalarWrite_AllKindCategories_ProducesOutput(ParameterSerializationKind kind)
    {
        IndentedWriter w = new();
        CodeEmitHelpers.EmitQueryScalarWrite(w, "value", "q0", kind);
        Assert.IsTrue(w.ToString().Length > 0, $"EmitQueryScalarWrite should produce output for {kind}");
    }

    [TestMethod]
    [DataRow(ParameterSerializationKind.String)]
    [DataRow(ParameterSerializationKind.Boolean)]
    [DataRow(ParameterSerializationKind.Int32)]
    [DataRow(ParameterSerializationKind.Double)]
    [DataRow(ParameterSerializationKind.UnboundedNumber)]
    [DataRow(ParameterSerializationKind.Object)]
    public void EmitHeaderScalarWrite_AllKindCategories_ProducesOutput(ParameterSerializationKind kind)
    {
        IndentedWriter w = new();
        CodeEmitHelpers.EmitHeaderScalarWrite(w, "value", "h0", kind);
        Assert.IsTrue(w.ToString().Length > 0, $"EmitHeaderScalarWrite should produce output for {kind}");
    }

    [TestMethod]
    [DataRow(ParameterSerializationKind.String)]
    [DataRow(ParameterSerializationKind.Boolean)]
    [DataRow(ParameterSerializationKind.Int32)]
    [DataRow(ParameterSerializationKind.Double)]
    [DataRow(ParameterSerializationKind.UnboundedNumber)]
    [DataRow(ParameterSerializationKind.Object)]
    public void EmitCookieScalarWrite_AllKindCategories_ProducesOutput(ParameterSerializationKind kind)
    {
        IndentedWriter w = new();
        CodeEmitHelpers.EmitCookieScalarWrite(w, "value", "c0", kind);
        Assert.IsTrue(w.ToString().Length > 0, $"EmitCookieScalarWrite should produce output for {kind}");
    }

    [TestMethod]
    [DataRow("abstract")]
    [DataRow("as")]
    [DataRow("base")]
    [DataRow("bool")]
    [DataRow("break")]
    [DataRow("byte")]
    [DataRow("case")]
    [DataRow("catch")]
    [DataRow("char")]
    [DataRow("checked")]
    [DataRow("class")]
    [DataRow("const")]
    [DataRow("continue")]
    [DataRow("decimal")]
    [DataRow("default")]
    [DataRow("delegate")]
    [DataRow("do")]
    [DataRow("double")]
    [DataRow("else")]
    [DataRow("enum")]
    [DataRow("event")]
    [DataRow("explicit")]
    [DataRow("extern")]
    [DataRow("false")]
    [DataRow("finally")]
    [DataRow("fixed")]
    [DataRow("float")]
    [DataRow("for")]
    [DataRow("foreach")]
    [DataRow("goto")]
    [DataRow("if")]
    [DataRow("implicit")]
    [DataRow("in")]
    [DataRow("int")]
    [DataRow("interface")]
    [DataRow("internal")]
    [DataRow("is")]
    [DataRow("lock")]
    [DataRow("long")]
    [DataRow("namespace")]
    [DataRow("new")]
    [DataRow("null")]
    [DataRow("object")]
    [DataRow("operator")]
    [DataRow("out")]
    [DataRow("override")]
    [DataRow("params")]
    [DataRow("private")]
    [DataRow("protected")]
    [DataRow("public")]
    [DataRow("readonly")]
    [DataRow("ref")]
    [DataRow("return")]
    [DataRow("sbyte")]
    [DataRow("sealed")]
    [DataRow("short")]
    [DataRow("sizeof")]
    [DataRow("stackalloc")]
    [DataRow("static")]
    [DataRow("string")]
    [DataRow("struct")]
    [DataRow("switch")]
    [DataRow("this")]
    [DataRow("throw")]
    [DataRow("true")]
    [DataRow("try")]
    [DataRow("typeof")]
    [DataRow("uint")]
    [DataRow("ulong")]
    [DataRow("unchecked")]
    [DataRow("unsafe")]
    [DataRow("ushort")]
    [DataRow("using")]
    [DataRow("virtual")]
    [DataRow("void")]
    [DataRow("volatile")]
    [DataRow("while")]
    public void EscapeCSharpKeyword_ReservedWords_AllPrefixAt(string keyword)
    {
        Assert.AreEqual($"@{keyword}", CodeEmitHelpers.EscapeCSharpKeyword(keyword));
    }

    [TestMethod]
    [DataRow(JsonValueKind.Object)]
    [DataRow(JsonValueKind.Array)]
    public void FormatDefaultValueExpression_ObjectOrArray_ReturnsDefault(JsonValueKind kind)
    {
        string result = CodeEmitHelpers.FormatDefaultValueExpression(
            "MyType", """{"key":"value"}""", kind);
        Assert.AreEqual("default", result);
    }

    [TestMethod]
    public void FormatDefaultValueExpression_NullJson_ReturnsDefault()
    {
        string result = CodeEmitHelpers.FormatDefaultValueExpression(
            "MyType", null, JsonValueKind.String);
        Assert.AreEqual("default", result);
    }

    [TestMethod]
    public void FormatDefaultValueExpression_StringKind_ReturnsStringConstant()
    {
        string result = CodeEmitHelpers.FormatDefaultValueExpression(
            "MyType", "hello", JsonValueKind.String);
        Assert.IsTrue(
            result.Contains("StringConstant", StringComparison.Ordinal),
            "String kind should produce StringConstant expression");
    }

    [TestMethod]
    public void FormatDefaultValueExpression_NumberKind_ReturnsNumberConstant()
    {
        string result = CodeEmitHelpers.FormatDefaultValueExpression(
            "MyType", "42", JsonValueKind.Number);
        Assert.IsTrue(
            result.Contains("NumberConstant", StringComparison.Ordinal),
            "Number kind should produce NumberConstant expression");
    }

    [TestMethod]
    public void FormatDefaultValueExpression_TrueKind_ReturnsTrue()
    {
        string result = CodeEmitHelpers.FormatDefaultValueExpression(
            "MyType", "true", JsonValueKind.True);
        Assert.IsTrue(
            result.Contains(".True", StringComparison.Ordinal),
            "True kind should produce .True expression");
    }

    [TestMethod]
    public void FormatDefaultValueExpression_FalseKind_ReturnsFalse()
    {
        string result = CodeEmitHelpers.FormatDefaultValueExpression(
            "MyType", "false", JsonValueKind.False);
        Assert.IsTrue(
            result.Contains(".False", StringComparison.Ordinal),
            "False kind should produce .False expression");
    }

    [TestMethod]
    public void FormatDefaultValueExpression_NullKind_ReturnsNull()
    {
        string result = CodeEmitHelpers.FormatDefaultValueExpression(
            "MyType", "null", JsonValueKind.Null);
        Assert.IsTrue(
            result.Contains(".Null", StringComparison.Ordinal),
            "Null kind should produce .Null expression");
    }

    [TestMethod]
    public void ContentTypeCondition_Json_ReturnsJsonCondition()
    {
        string result = CodeEmitHelpers.ContentTypeCondition(ContentCategory.Json);
        Assert.IsTrue(
            result.Contains("application/json", StringComparison.Ordinal),
            "Json category should check for application/json");
    }

    [TestMethod]
    public void ContentTypeCondition_TextPlain_ReturnsTextCondition()
    {
        string result = CodeEmitHelpers.ContentTypeCondition(ContentCategory.TextPlain);
        Assert.IsTrue(
            result.Contains("text/", StringComparison.Ordinal),
            "TextPlain category should check for text/");
    }

    [TestMethod]
    public void ContentTypeCondition_OctetStream_ReturnsBinaryCondition()
    {
        string result = CodeEmitHelpers.ContentTypeCondition(ContentCategory.OctetStream);
        Assert.IsTrue(
            result.Contains("text/", StringComparison.Ordinal),
            "OctetStream category should reference text/ (negated)");
    }

    [TestMethod]
    public void ContentTypeCondition_UnknownCategory_ReturnsTrue()
    {
        string result = CodeEmitHelpers.ContentTypeCondition((ContentCategory)99);
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    [DataRow(ParameterSerializationKind.String, "element")]
    [DataRow(ParameterSerializationKind.Boolean, "bool.Parse(element)")]
    [DataRow(ParameterSerializationKind.Int32, "int.Parse(element)")]
    [DataRow(ParameterSerializationKind.Int64, "long.Parse(element)")]
    [DataRow(ParameterSerializationKind.Single, "float.Parse(element)")]
    [DataRow(ParameterSerializationKind.Double, "double.Parse(element)")]
    [DataRow(ParameterSerializationKind.Decimal, "decimal.Parse(element)")]
    [DataRow(ParameterSerializationKind.Int16, "short.Parse(element)")]
    [DataRow(ParameterSerializationKind.Byte, "byte.Parse(element)")]
    [DataRow(ParameterSerializationKind.SByte, "sbyte.Parse(element)")]
    [DataRow(ParameterSerializationKind.UInt16, "ushort.Parse(element)")]
    [DataRow(ParameterSerializationKind.UInt32, "uint.Parse(element)")]
    [DataRow(ParameterSerializationKind.UInt64, "ulong.Parse(element)")]
    [DataRow(ParameterSerializationKind.Half, "double.Parse(element)")]
    [DataRow(ParameterSerializationKind.UnboundedNumber, "double.Parse(element)")]
    public void GetElementSourceExpression_AllKinds_ReturnsExpected(
        ParameterSerializationKind kind, string expected)
    {
        Assert.AreEqual(expected, CodeEmitHelpers.GetElementSourceExpressionPublic(kind, "element"));
    }

    [TestMethod]
    public void GetElementSourceExpression_UnknownKind_ReturnsVarName()
    {
        // The default case returns the variable name unchanged
        Assert.AreEqual("element", CodeEmitHelpers.GetElementSourceExpressionPublic((ParameterSerializationKind)99, "element"));
    }

    [TestMethod]
    [DataRow(ParameterSerializationKind.Array)]
    [DataRow(ParameterSerializationKind.Byte)]
    [DataRow(ParameterSerializationKind.UInt16)]
    [DataRow(ParameterSerializationKind.UInt32)]
    [DataRow(ParameterSerializationKind.UInt64)]
    [DataRow(ParameterSerializationKind.SByte)]
    [DataRow(ParameterSerializationKind.Int16)]
    [DataRow(ParameterSerializationKind.Int64)]
    [DataRow(ParameterSerializationKind.Int128)]
    [DataRow(ParameterSerializationKind.UInt128)]
    [DataRow(ParameterSerializationKind.Half)]
    [DataRow(ParameterSerializationKind.Single)]
    [DataRow(ParameterSerializationKind.Decimal)]
    public void EmitQueryScalarWrite_AdditionalKinds_ProducesOutput(ParameterSerializationKind kind)
    {
        IndentedWriter w = new();
        CodeEmitHelpers.EmitQueryScalarWrite(w, "value", "q0", kind);
        Assert.IsTrue(w.ToString().Length > 0, $"EmitQueryScalarWrite should produce output for {kind}");
    }

    [TestMethod]
    [DataRow(ParameterSerializationKind.Array)]
    [DataRow(ParameterSerializationKind.Byte)]
    [DataRow(ParameterSerializationKind.UInt16)]
    [DataRow(ParameterSerializationKind.UInt32)]
    [DataRow(ParameterSerializationKind.UInt64)]
    [DataRow(ParameterSerializationKind.SByte)]
    [DataRow(ParameterSerializationKind.Int16)]
    [DataRow(ParameterSerializationKind.Int64)]
    [DataRow(ParameterSerializationKind.Int128)]
    [DataRow(ParameterSerializationKind.UInt128)]
    [DataRow(ParameterSerializationKind.Half)]
    [DataRow(ParameterSerializationKind.Single)]
    [DataRow(ParameterSerializationKind.Decimal)]
    public void EmitHeaderScalarWrite_AdditionalKinds_ProducesOutput(ParameterSerializationKind kind)
    {
        IndentedWriter w = new();
        CodeEmitHelpers.EmitHeaderScalarWrite(w, "value", "h0", kind);
        Assert.IsTrue(w.ToString().Length > 0, $"EmitHeaderScalarWrite should produce output for {kind}");
    }

    [TestMethod]
    [DataRow(ParameterSerializationKind.Array)]
    [DataRow(ParameterSerializationKind.Byte)]
    [DataRow(ParameterSerializationKind.UInt16)]
    [DataRow(ParameterSerializationKind.UInt32)]
    [DataRow(ParameterSerializationKind.UInt64)]
    [DataRow(ParameterSerializationKind.SByte)]
    [DataRow(ParameterSerializationKind.Int16)]
    [DataRow(ParameterSerializationKind.Int64)]
    [DataRow(ParameterSerializationKind.Int128)]
    [DataRow(ParameterSerializationKind.UInt128)]
    [DataRow(ParameterSerializationKind.Half)]
    [DataRow(ParameterSerializationKind.Single)]
    [DataRow(ParameterSerializationKind.Decimal)]
    public void EmitCookieScalarWrite_AdditionalKinds_ProducesOutput(ParameterSerializationKind kind)
    {
        IndentedWriter w = new();
        CodeEmitHelpers.EmitCookieScalarWrite(w, "value", "c0", kind);
        Assert.IsTrue(w.ToString().Length > 0, $"EmitCookieScalarWrite should produce output for {kind}");
    }

    [TestMethod]
    [DataRow(ParameterSerializationKind.Array)]
    [DataRow(ParameterSerializationKind.Byte)]
    [DataRow(ParameterSerializationKind.UInt16)]
    [DataRow(ParameterSerializationKind.UInt32)]
    [DataRow(ParameterSerializationKind.UInt64)]
    [DataRow(ParameterSerializationKind.SByte)]
    [DataRow(ParameterSerializationKind.Int16)]
    [DataRow(ParameterSerializationKind.Int64)]
    [DataRow(ParameterSerializationKind.Int128)]
    [DataRow(ParameterSerializationKind.UInt128)]
    [DataRow(ParameterSerializationKind.Half)]
    [DataRow(ParameterSerializationKind.Single)]
    [DataRow(ParameterSerializationKind.Decimal)]
    public void EmitPathParamWrite_AdditionalKinds_ProducesOutput(ParameterSerializationKind kind)
    {
        IndentedWriter w = new();
        CodeEmitHelpers.EmitPathParamWrite(w, "color", "value", "p0", kind, ParameterStyle.Simple, false);
        Assert.IsTrue(w.ToString().Length > 0, $"EmitPathParamWrite should produce output for {kind}");
    }

    [TestMethod]
    [DataRow(ParameterSerializationKind.String)]
    [DataRow(ParameterSerializationKind.Boolean)]
    [DataRow(ParameterSerializationKind.Int32)]
    [DataRow(ParameterSerializationKind.Double)]
    [DataRow(ParameterSerializationKind.UnboundedNumber)]
    [DataRow(ParameterSerializationKind.Byte)]
    [DataRow(ParameterSerializationKind.Decimal)]
    [DataRow(ParameterSerializationKind.Half)]
    public void EmitPathScalarValue_AllKindCategories_ProducesOutput(ParameterSerializationKind kind)
    {
        IndentedWriter w = new();
        CodeEmitHelpers.EmitPathScalarValue(w, "value", "p0", kind, allowReserved: false);
        Assert.IsTrue(w.ToString().Length > 0, $"EmitPathScalarValue should produce output for {kind}");
    }

    [TestMethod]
    public void EmitPathScalarValue_String_AllowReserved_WritesDirectly()
    {
        IndentedWriter w = new();
        CodeEmitHelpers.EmitPathScalarValue(w, "value", "p0", ParameterSerializationKind.String, allowReserved: true);
        string output = w.ToString();
        Assert.IsTrue(output.Contains("writer.Write(utf8p0.Span)"), "AllowReserved=true should write directly");
    }

    [TestMethod]
    public void EmitQueryScalarWrite_UnrecognizedKind_ProducesNoOutput()
    {
        // Exercises the implicit default (fall-through) branch
        IndentedWriter w = new();
        CodeEmitHelpers.EmitQueryScalarWrite(w, "value", "q0", (ParameterSerializationKind)99);
        Assert.AreEqual(0, w.ToString().Length);
    }

    [TestMethod]
    public void EmitHeaderScalarWrite_UnrecognizedKind_ProducesNoOutput()
    {
        IndentedWriter w = new();
        CodeEmitHelpers.EmitHeaderScalarWrite(w, "value", "h0", (ParameterSerializationKind)99);
        Assert.AreEqual(0, w.ToString().Length);
    }

    [TestMethod]
    public void EmitCookieScalarWrite_UnrecognizedKind_ProducesNoOutput()
    {
        IndentedWriter w = new();
        CodeEmitHelpers.EmitCookieScalarWrite(w, "value", "c0", (ParameterSerializationKind)99);
        Assert.AreEqual(0, w.ToString().Length);
    }

    [TestMethod]
    public void EmitPathScalarValue_UnrecognizedKind_ProducesNoOutput()
    {
        IndentedWriter w = new();
        CodeEmitHelpers.EmitPathScalarValue(w, "value", "p0", (ParameterSerializationKind)99, allowReserved: false);
        Assert.AreEqual(0, w.ToString().Length);
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
    public void TryFormatBufferSize_AllBoundedKinds_ReturnsExpectedSize(
        ParameterSerializationKind kind, int expected)
    {
        Assert.AreEqual(expected, CodeEmitHelpers.TryFormatBufferSize(kind));
    }

    [TestMethod]
    [DataRow(ParameterStyle.Simple, false)]
    [DataRow(ParameterStyle.Simple, true)]
    [DataRow(ParameterStyle.Label, false)]
    [DataRow(ParameterStyle.Label, true)]
    [DataRow(ParameterStyle.Matrix, false)]
    [DataRow(ParameterStyle.Matrix, true)]
    public void EmitPathArrayWrite_AllStyleExplodeCombinations_ProducesOutput(
        ParameterStyle style, bool explode)
    {
        IndentedWriter w = new();
        CodeEmitHelpers.EmitPathArrayWrite(w, "colors", "value", "a0", style, explode);
        Assert.IsTrue(w.ToString().Length > 0, $"EmitPathArrayWrite should produce output for {style}/{explode}");
    }

    [TestMethod]
    [DataRow(ParameterStyle.Simple, false)]
    [DataRow(ParameterStyle.Simple, true)]
    [DataRow(ParameterStyle.Label, false)]
    [DataRow(ParameterStyle.Label, true)]
    [DataRow(ParameterStyle.Matrix, false)]
    [DataRow(ParameterStyle.Matrix, true)]
    public void EmitPathObjectWrite_AllStyleExplodeCombinations_ProducesOutput(
        ParameterStyle style, bool explode)
    {
        IndentedWriter w = new();
        CodeEmitHelpers.EmitPathObjectWrite(w, "color", "value", "o0", style, explode);
        Assert.IsTrue(w.ToString().Length > 0, $"EmitPathObjectWrite should produce output for {style}/{explode}");
    }

    [TestMethod]
    public void GetAcceptMediaTypes_MultipleTypes_OrdersByPriority()
    {
        var responses = new[]
        {
            ("application/octet-stream", (string?)null),
            ("text/plain", (string?)null),
            ("application/json", (string?)"#/components/schemas/Pet"),
        };

        string[] result = CodeEmitHelpers.GetAcceptMediaTypes(responses);

        // JSON should be first, text second, octet-stream third
        Assert.AreEqual(3, result.Length);
        Assert.AreEqual("application/json", result[0]);
        Assert.AreEqual("text/plain", result[1]);
        Assert.AreEqual("application/octet-stream", result[2]);
    }

    [TestMethod]
    public void GetAcceptMediaTypes_WildcardExcluded()
    {
        var responses = new[]
        {
            ("*/*", (string?)null),
            ("application/json", (string?)"#/components/schemas/Pet"),
        };

        string[] result = CodeEmitHelpers.GetAcceptMediaTypes(responses);
        Assert.AreEqual(1, result.Length);
        Assert.AreEqual("application/json", result[0]);
    }

    [TestMethod]
    public void GetMatchTypeName_JsonCategory_ReturnsTypeName()
    {
        string result = CodeEmitHelpers.GetMatchTypeName(ContentCategory.Json, "MySchema");
        Assert.AreEqual("MySchema", result);
    }

    [TestMethod]
    public void GetMatchTypeName_JsonCategory_NullTypeName_ReturnsEmpty()
    {
        string result = CodeEmitHelpers.GetMatchTypeName(ContentCategory.Json, null);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void GetMatchTypeName_OctetStream_ReturnsStreamNullable()
    {
        string result = CodeEmitHelpers.GetMatchTypeName(ContentCategory.OctetStream, "MySchema");
        Assert.AreEqual("Stream?", result);
    }

    [TestMethod]
    public void GetMatchTypeName_TextPlain_ReturnsStringNullable()
    {
        string result = CodeEmitHelpers.GetMatchTypeName(ContentCategory.TextPlain, "MySchema");
        Assert.AreEqual("string?", result);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EmitObjectParseFromSeparatedString_HasDeepNesting_UsesNextSeparator(bool hasDeepNesting)
    {
        IndentedWriter w = new();
        CodeEmitHelpers.EmitObjectParseFromSeparatedString(
            w,
            targetVar: "result",
            rawValueVar: "raw",
            workspaceExpr: "workspace",
            typeName: "MyObj",
            separator: "','",
            explode: false,
            valueKind: ParameterSerializationKind.String,
            hasDeepNesting: hasDeepNesting,
            valueTypeName: "MyVal");

        string output = w.ToString();

        if (hasDeepNesting)
        {
            Assert.IsTrue(output.Contains("NextSeparator"), "Deep nesting should use NextSeparator");
        }
        else
        {
            Assert.IsTrue(output.Contains("IndexOf"), "Non-deep nesting should use IndexOf");
        }
    }
}