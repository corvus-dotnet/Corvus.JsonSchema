// <copyright file="ComplexValueBuilderCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using System.Text;
using Corvus.Numerics;
using Corvus.Text.Json.Internal;
using NodaTime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests targeting the thin wrapper overloads of AddProperty on
/// <see cref="ComplexValueBuilder"/> that delegate with default escapeName/nameRequiresUnescaping.
/// Also covers AddPropertyFormattedNumber and AddPropertyRawString string-name variants,
/// and AddProperty with char-span property names.
/// </summary>
[TestClass]
public class ComplexValueBuilderCoverageTests
{
    #region Helpers

    private static string BuildObjectViaObjectBuilder(JsonElement.ObjectBuilder.Build buildAction)
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        JsonElement.ObjectBuilder.BuildValue(buildAction, ref cvb);
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        return documentBuilder.RootElement.ToString();
    }

    #endregion

    #region AddProperty — byte

    [TestMethod]
    public void AddProperty_Byte_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), (byte)255);
            });

        Assert.AreEqual("""{"val":255}""", json);
    }

    #endregion

    #region AddProperty — sbyte

    [TestMethod]
    public void AddProperty_Sbyte_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), (sbyte)-42);
            });

        Assert.AreEqual("""{"val":-42}""", json);
    }

    #endregion

    #region AddProperty — short

    [TestMethod]
    public void AddProperty_Short_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), (short)-1000);
            });

        Assert.AreEqual("""{"val":-1000}""", json);
    }

    #endregion

    #region AddProperty — ushort

    [TestMethod]
    public void AddProperty_Ushort_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), (ushort)60000);
            });

        Assert.AreEqual("""{"val":60000}""", json);
    }

    #endregion

    #region AddProperty — uint

    [TestMethod]
    public void AddProperty_Uint_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), 3000000000U);
            });

        Assert.AreEqual("""{"val":3000000000}""", json);
    }

    #endregion

    #region AddProperty — ulong

    [TestMethod]
    public void AddProperty_Ulong_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), 9999999999999UL);
            });

        Assert.AreEqual("""{"val":9999999999999}""", json);
    }

    #endregion

    #region AddProperty — float

    [TestMethod]
    public void AddProperty_Float_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), 1.5f);
            });

        Assert.AreEqual("""{"val":1.5}""", json);
    }

    #endregion

    #region AddProperty — double

    [TestMethod]
    public void AddProperty_Double_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), 3.14);
            });

        Assert.AreEqual("""{"val":3.14}""", json);
    }

    #endregion

    #region AddProperty — decimal

    [TestMethod]
    public void AddProperty_Decimal_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), 123.456m);
            });

        Assert.AreEqual("""{"val":123.456}""", json);
    }

    #endregion

    #region AddProperty — BigInteger

    [TestMethod]
    public void AddProperty_BigInteger_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), new BigInteger(999999999999999));
            });

        Assert.AreEqual("""{"val":999999999999999}""", json);
    }

    #endregion

    #region AddProperty — BigNumber

    [TestMethod]
    public void AddProperty_BigNumber_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                BigNumber.TryParse(Encoding.UTF8.GetBytes("12345.6789"), out BigNumber bn);
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), bn);
            });

        Assert.AreEqual("""{"val":123456789E-4}""", json);
    }

    #endregion

    #region AddProperty — Guid

    [TestMethod]
    public void AddProperty_Guid_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), new Guid("12345678-1234-1234-1234-123456789abc"));
            });

        Assert.AreEqual("""{"val":"12345678-1234-1234-1234-123456789abc"}""", json);
    }

    #endregion

    #region AddProperty — DateTime

    [TestMethod]
    public void AddProperty_DateTime_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc));
            });

        Assert.AreEqual("""{"val":"2024-01-15T10:30:00Z"}""", json);
    }

    #endregion

    #region AddProperty — DateTimeOffset

    [TestMethod]
    public void AddProperty_DateTimeOffset_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.FromHours(5)));
            });

        Assert.AreEqual("""{"val":"2024-06-15T12:00:00\u002B05:00"}""", json);
    }

    #endregion

    #region AddProperty — bool

    [TestMethod]
    public void AddProperty_Bool_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), true);
            });

        Assert.AreEqual("""{"val":true}""", json);
    }

    #endregion

#if NET

    #region AddProperty — Half

    [TestMethod]
    public void AddProperty_Half_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), (Half)2.5);
            });

        Assert.AreEqual("""{"val":2.5}""", json);
    }

    #endregion

#endif

    #region AddProperty — OffsetDateTime (NodaTime)

    [TestMethod]
    public void AddProperty_OffsetDateTime_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                var odt = new OffsetDateTime(new LocalDateTime(2024, 1, 15, 10, 30, 0), Offset.FromHours(5));
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), odt);
            });

        Assert.AreEqual("""{"val":"2024-01-15T10:30:00.0000000\u002B05:00"}""", json);
    }

    #endregion

    #region AddProperty — OffsetDate (NodaTime)

    [TestMethod]
    public void AddProperty_OffsetDate_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                var od = new OffsetDate(new LocalDate(2024, 1, 15), Offset.FromHours(5));
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), od);
            });

        Assert.AreEqual("""{"val":"2024-01-15\u002B05:00"}""", json);
    }

    #endregion

    #region AddProperty — OffsetTime (NodaTime)

    [TestMethod]
    public void AddProperty_OffsetTime_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                var ot = new OffsetTime(new LocalTime(10, 30, 0), Offset.FromHours(5));
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), ot);
            });

        Assert.AreEqual("""{"val":"10:30:00.0000000\u002B05:00"}""", json);
    }

    #endregion

    #region AddProperty — LocalDate (NodaTime)

    [TestMethod]
    public void AddProperty_LocalDate_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), new LocalDate(2024, 1, 15));
            });

        Assert.AreEqual("""{"val":"2024-01-15"}""", json);
    }

    #endregion

    #region AddProperty — Period (NodaTime)

    [TestMethod]
    public void AddProperty_Period_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), Period.FromYears(1) + Period.FromMonths(2) + Period.FromDays(3));
            });

        Assert.AreEqual("""{"val":"P1Y2M3D"}""", json);
    }

    #endregion

    #region AddPropertyFormattedNumber — ReadOnlySpan<byte> property name wrapper

    [TestMethod]
    public void AddPropertyFormattedNumber_Utf8Name()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddPropertyFormattedNumber(Encoding.UTF8.GetBytes("num"), Encoding.UTF8.GetBytes("99"));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"num":99}""", json);
    }

    #endregion

    #region AddPropertyFormattedNumber — string property name

    [TestMethod]
    public void AddPropertyFormattedNumber_StringName()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddPropertyFormattedNumber("num", Encoding.UTF8.GetBytes("77"));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"num":77}""", json);
    }

    #endregion

    #region AddPropertyRawString — string property name

    [TestMethod]
    public void AddPropertyRawString_StringName()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddPropertyRawString("prop", Encoding.UTF8.GetBytes("raw"), false);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"prop":"raw"}""", json);
    }

    #endregion

    #region AddPropertyNull — ReadOnlySpan<byte> property name wrapper

    [TestMethod]
    public void AddPropertyNull_Utf8Name()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddPropertyNull(Encoding.UTF8.GetBytes("n"));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"n":null}""", json);
    }

    #endregion

    #region AddProperty — ReadOnlySpan<byte> string value wrapper

    [TestMethod]
    public void AddProperty_Utf8StringValue_Utf8Name()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty(Encoding.UTF8.GetBytes("s"), Encoding.UTF8.GetBytes("hello"));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"s":"hello"}""", json);
    }

    #endregion

    #region AddProperty with char-span property names (non-UTF8 overloads)

    [TestMethod]
    public void AddProperty_CharSpan_Guid()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("myGuid".AsSpan(), new Guid("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"myGuid":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_DateTime()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("dt".AsSpan(), new DateTime(2025, 3, 20, 8, 0, 0, DateTimeKind.Utc));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"dt":"2025-03-20T08:00:00Z"}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_DateTimeOffset()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("dto".AsSpan(), new DateTimeOffset(2025, 3, 20, 8, 0, 0, TimeSpan.FromHours(2)));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"dto":"2025-03-20T08:00:00\u002B02:00"}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_Bool()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("flag".AsSpan(), false);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"flag":false}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_OffsetDateTime()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        var odt = new OffsetDateTime(new LocalDateTime(2024, 6, 1, 12, 0, 0), Offset.FromHours(3));
        cvb.AddProperty("odt".AsSpan(), odt);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"odt":"2024-06-01T12:00:00.0000000\u002B03:00"}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_OffsetDate()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        var od = new OffsetDate(new LocalDate(2024, 6, 1), Offset.FromHours(3));
        cvb.AddProperty("od".AsSpan(), od);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"od":"2024-06-01\u002B03:00"}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_OffsetTime()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        var ot = new OffsetTime(new LocalTime(14, 30, 0), Offset.FromHours(3));
        cvb.AddProperty("ot".AsSpan(), ot);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"ot":"14:30:00.0000000\u002B03:00"}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_LocalDate()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("ld".AsSpan(), new LocalDate(2024, 12, 25));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"ld":"2024-12-25"}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_Period()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("p".AsSpan(), Period.FromDays(7));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"p":"P0Y0M7D"}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_Sbyte()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("sb".AsSpan(), (sbyte)-100);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"sb":-100}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_Byte()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("b".AsSpan(), (byte)200);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"b":200}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_Short()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("sh".AsSpan(), (short)12345);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"sh":12345}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_Ushort()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("us".AsSpan(), (ushort)50000);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"us":50000}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_Uint()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("ui".AsSpan(), 4000000000U);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"ui":4000000000}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_Long()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("lg".AsSpan(), 9876543210L);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"lg":9876543210}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_Ulong()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("ul".AsSpan(), 18000000000000000000UL);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"ul":18000000000000000000}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_Float()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("fl".AsSpan(), 2.5f);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"fl":2.5}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_Double()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("db".AsSpan(), 9.99);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"db":9.99}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_Decimal()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("dec".AsSpan(), 77.77m);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"dec":77.77}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_BigInteger()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("bi".AsSpan(), new BigInteger(1234567890123456789));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"bi":1234567890123456789}""", json);
    }

    [TestMethod]
    public void AddProperty_CharSpan_BigNumber()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        BigNumber.TryParse(Encoding.UTF8.GetBytes("555.123"), out BigNumber bn);
        cvb.AddProperty("bn".AsSpan(), bn);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"bn":555123E-3}""", json);
    }

    #endregion

    #region AddProperty with char-span property name — string values

    [TestMethod]
    public void AddProperty_CharSpan_CharSpanValue()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("key".AsSpan(), "value".AsSpan());
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"key":"value"}""", json);
    }

    [TestMethod]
    public void AddPropertyNull_CharSpan()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddPropertyNull("nul".AsSpan());
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"nul":null}""", json);
    }

    #endregion

    #region AddProperty with ValueBuilderAction delegate (nested object)

    [TestMethod]
    public void AddProperty_ValueBuilderAction_NestedObject()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty(
            Encoding.UTF8.GetBytes("nested"),
            static (ref ComplexValueBuilder b) =>
            {
                b.StartObject();
                b.AddProperty(Encoding.UTF8.GetBytes("inner"), 42);
                b.EndObject();
            });
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"nested":{"inner":42}}""", json);
    }

    [TestMethod]
    public void AddProperty_ValueBuilderAction_CharSpan_NestedObject()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty(
            "nested".AsSpan(),
            static (ref ComplexValueBuilder b) =>
            {
                b.StartObject();
                b.AddProperty("x".AsSpan(), 99);
                b.EndObject();
            });
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"nested":{"x":99}}""", json);
    }

    #endregion

    #region AddProperty with ValueBuilderAction<TContext> delegate

    [TestMethod]
    public void AddProperty_ValueBuilderActionWithContext_NestedObject()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty(
            Encoding.UTF8.GetBytes("ctx"),
            77,
            static (in int ctx, ref ComplexValueBuilder b) =>
            {
                b.StartObject();
                b.AddProperty(Encoding.UTF8.GetBytes("val"), ctx);
                b.EndObject();
            });
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"ctx":{"val":77}}""", json);
    }

    [TestMethod]
    public void AddProperty_ValueBuilderActionWithContext_CharSpan_NestedObject()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty(
            "ctx".AsSpan(),
            88,
            static (in int ctx, ref ComplexValueBuilder b) =>
            {
                b.StartObject();
                b.AddProperty("val".AsSpan(), ctx);
                b.EndObject();
            });
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"ctx":{"val":88}}""", json);
    }

    #endregion

    #region AddPropertyFormattedNumber — char-span property name

    [TestMethod]
    public void AddPropertyFormattedNumber_CharSpanName()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddPropertyFormattedNumber("num".AsSpan(), Encoding.UTF8.GetBytes("1234"));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"num":1234}""", json);
    }

    #endregion

    #region AddPropertyRawString — char-span property name

    [TestMethod]
    public void AddPropertyRawString_CharSpanName()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddPropertyRawString("raw".AsSpan(), Encoding.UTF8.GetBytes("data"), false);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"raw":"data"}""", json);
    }

    #endregion

#if NET

    #region AddProperty — Int128

    [TestMethod]
    public void AddProperty_Int128_Utf8Name()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty(Encoding.UTF8.GetBytes("val"), (Int128)999888777666);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"val":999888777666}""", json);
    }

    [TestMethod]
    public void AddProperty_Int128_CharSpanName()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("val".AsSpan(), (Int128)111222333);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"val":111222333}""", json);
    }

    #endregion

    #region AddProperty — UInt128

    [TestMethod]
    public void AddProperty_UInt128_Utf8Name()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty(Encoding.UTF8.GetBytes("val"), (UInt128)888777666555);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"val":888777666555}""", json);
    }

    [TestMethod]
    public void AddProperty_UInt128_CharSpanName()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("val".AsSpan(), (UInt128)444555666);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"val":444555666}""", json);
    }

    #endregion

    #region AddProperty — Half char-span

    [TestMethod]
    public void AddProperty_Half_CharSpanName()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("h".AsSpan(), (Half)1.5);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.AreEqual("""{"h":1.5}""", json);
    }

    #endregion

#endif
}
