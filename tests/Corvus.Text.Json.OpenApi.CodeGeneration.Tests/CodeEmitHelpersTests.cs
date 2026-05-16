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
        CodeEmitHelpers.EmitPathParamWrite(w, "value", "p0", kind);
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
}