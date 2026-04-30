// <copyright file="ComplexValueBuilderCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using System.Text;
using Corvus.Numerics;
using Corvus.Text.Json.Internal;
using NodaTime;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests targeting the thin wrapper overloads of AddProperty on
/// <see cref="ComplexValueBuilder"/> that delegate with default escapeName/nameRequiresUnescaping.
/// Also covers AddPropertyFormattedNumber and AddPropertyRawString string-name variants,
/// and AddProperty with char-span property names.
/// </summary>
public static class ComplexValueBuilderCoverageTests
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

    [Fact]
    public static void AddProperty_Byte_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), (byte)255);
            });

        Assert.Contains("255", json);
    }

    #endregion

    #region AddProperty — sbyte

    [Fact]
    public static void AddProperty_Sbyte_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), (sbyte)-42);
            });

        Assert.Contains("-42", json);
    }

    #endregion

    #region AddProperty — short

    [Fact]
    public static void AddProperty_Short_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), (short)-1000);
            });

        Assert.Contains("-1000", json);
    }

    #endregion

    #region AddProperty — ushort

    [Fact]
    public static void AddProperty_Ushort_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), (ushort)60000);
            });

        Assert.Contains("60000", json);
    }

    #endregion

    #region AddProperty — uint

    [Fact]
    public static void AddProperty_Uint_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), 3000000000U);
            });

        Assert.Contains("3000000000", json);
    }

    #endregion

    #region AddProperty — ulong

    [Fact]
    public static void AddProperty_Ulong_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), 9999999999999UL);
            });

        Assert.Contains("9999999999999", json);
    }

    #endregion

    #region AddProperty — float

    [Fact]
    public static void AddProperty_Float_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), 1.5f);
            });

        Assert.Contains("1.5", json);
    }

    #endregion

    #region AddProperty — double

    [Fact]
    public static void AddProperty_Double_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), 3.14);
            });

        Assert.Contains("3.14", json);
    }

    #endregion

    #region AddProperty — decimal

    [Fact]
    public static void AddProperty_Decimal_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), 123.456m);
            });

        Assert.Contains("123.456", json);
    }

    #endregion

    #region AddProperty — BigInteger

    [Fact]
    public static void AddProperty_BigInteger_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), new BigInteger(999999999999999));
            });

        Assert.Contains("999999999999999", json);
    }

    #endregion

    #region AddProperty — BigNumber

    [Fact]
    public static void AddProperty_BigNumber_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                BigNumber.TryParse(Encoding.UTF8.GetBytes("12345.6789"), out BigNumber bn);
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), bn);
            });

        Assert.Contains("12345", json);
    }

    #endregion

    #region AddProperty — Guid

    [Fact]
    public static void AddProperty_Guid_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), new Guid("12345678-1234-1234-1234-123456789abc"));
            });

        Assert.Contains("12345678-1234-1234-1234-123456789abc", json);
    }

    #endregion

    #region AddProperty — DateTime

    [Fact]
    public static void AddProperty_DateTime_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc));
            });

        Assert.Contains("2024", json);
    }

    #endregion

    #region AddProperty — DateTimeOffset

    [Fact]
    public static void AddProperty_DateTimeOffset_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.FromHours(5)));
            });

        Assert.Contains("2024", json);
    }

    #endregion

    #region AddProperty — bool

    [Fact]
    public static void AddProperty_Bool_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), true);
            });

        Assert.Contains("true", json);
    }

    #endregion

#if NET

    #region AddProperty — Half

    [Fact]
    public static void AddProperty_Half_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), (Half)2.5);
            });

        Assert.Contains("2.5", json);
    }

    #endregion

#endif

    #region AddProperty — OffsetDateTime (NodaTime)

    [Fact]
    public static void AddProperty_OffsetDateTime_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                var odt = new OffsetDateTime(new LocalDateTime(2024, 1, 15, 10, 30, 0), Offset.FromHours(5));
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), odt);
            });

        Assert.Contains("2024", json);
    }

    #endregion

    #region AddProperty — OffsetDate (NodaTime)

    [Fact]
    public static void AddProperty_OffsetDate_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                var od = new OffsetDate(new LocalDate(2024, 1, 15), Offset.FromHours(5));
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), od);
            });

        Assert.Contains("2024", json);
    }

    #endregion

    #region AddProperty — OffsetTime (NodaTime)

    [Fact]
    public static void AddProperty_OffsetTime_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                var ot = new OffsetTime(new LocalTime(10, 30, 0), Offset.FromHours(5));
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), ot);
            });

        Assert.Contains("10", json);
    }

    #endregion

    #region AddProperty — LocalDate (NodaTime)

    [Fact]
    public static void AddProperty_LocalDate_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), new LocalDate(2024, 1, 15));
            });

        Assert.Contains("2024", json);
    }

    #endregion

    #region AddProperty — Period (NodaTime)

    [Fact]
    public static void AddProperty_Period_Utf8Name()
    {
        string json = BuildObjectViaObjectBuilder(
            static (ref JsonElement.ObjectBuilder builder) =>
            {
                builder.AddProperty(Encoding.UTF8.GetBytes("val"), Period.FromYears(1) + Period.FromMonths(2) + Period.FromDays(3));
            });

        Assert.Contains("P1Y2M3D", json);
    }

    #endregion

    #region AddPropertyFormattedNumber — ReadOnlySpan<byte> property name wrapper

    [Fact]
    public static void AddPropertyFormattedNumber_Utf8Name()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddPropertyFormattedNumber(Encoding.UTF8.GetBytes("num"), Encoding.UTF8.GetBytes("99"));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("99", json);
    }

    #endregion

    #region AddPropertyFormattedNumber — string property name

    [Fact]
    public static void AddPropertyFormattedNumber_StringName()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddPropertyFormattedNumber("num", Encoding.UTF8.GetBytes("77"));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("77", json);
    }

    #endregion

    #region AddPropertyRawString — string property name

    [Fact]
    public static void AddPropertyRawString_StringName()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddPropertyRawString("prop", Encoding.UTF8.GetBytes("raw"), false);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("raw", json);
    }

    #endregion

    #region AddPropertyNull — ReadOnlySpan<byte> property name wrapper

    [Fact]
    public static void AddPropertyNull_Utf8Name()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddPropertyNull(Encoding.UTF8.GetBytes("n"));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("null", json);
    }

    #endregion

    #region AddProperty — ReadOnlySpan<byte> string value wrapper

    [Fact]
    public static void AddProperty_Utf8StringValue_Utf8Name()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty(Encoding.UTF8.GetBytes("s"), Encoding.UTF8.GetBytes("hello"));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("hello", json);
    }

    #endregion

    #region AddProperty with char-span property names (non-UTF8 overloads)

    [Fact]
    public static void AddProperty_CharSpan_Guid()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("myGuid".AsSpan(), new Guid("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_DateTime()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("dt".AsSpan(), new DateTime(2025, 3, 20, 8, 0, 0, DateTimeKind.Utc));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("2025", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_DateTimeOffset()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("dto".AsSpan(), new DateTimeOffset(2025, 3, 20, 8, 0, 0, TimeSpan.FromHours(2)));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("2025", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_Bool()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("flag".AsSpan(), false);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("false", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_OffsetDateTime()
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

        Assert.Contains("2024", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_OffsetDate()
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

        Assert.Contains("2024", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_OffsetTime()
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

        Assert.Contains("14", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_LocalDate()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("ld".AsSpan(), new LocalDate(2024, 12, 25));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("2024", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_Period()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("p".AsSpan(), Period.FromDays(7));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("7D", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_Sbyte()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("sb".AsSpan(), (sbyte)-100);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("-100", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_Byte()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("b".AsSpan(), (byte)200);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("200", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_Short()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("sh".AsSpan(), (short)12345);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("12345", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_Ushort()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("us".AsSpan(), (ushort)50000);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("50000", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_Uint()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("ui".AsSpan(), 4000000000U);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("4000000000", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_Long()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("lg".AsSpan(), 9876543210L);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("9876543210", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_Ulong()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("ul".AsSpan(), 18000000000000000000UL);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("18000000000000000000", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_Float()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("fl".AsSpan(), 2.5f);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("2.5", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_Double()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("db".AsSpan(), 9.99);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("9.99", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_Decimal()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("dec".AsSpan(), 77.77m);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("77.77", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_BigInteger()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("bi".AsSpan(), new BigInteger(1234567890123456789));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("1234567890123456789", json);
    }

    [Fact]
    public static void AddProperty_CharSpan_BigNumber()
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

        Assert.Contains("555", json);
    }

    #endregion

    #region AddProperty with char-span property name — string values

    [Fact]
    public static void AddProperty_CharSpan_CharSpanValue()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("key".AsSpan(), "value".AsSpan());
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("value", json);
    }

    [Fact]
    public static void AddPropertyNull_CharSpan()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddPropertyNull("nul".AsSpan());
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("null", json);
    }

    #endregion

    #region AddProperty with ValueBuilderAction delegate (nested object)

    [Fact]
    public static void AddProperty_ValueBuilderAction_NestedObject()
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

        Assert.Contains("inner", json);
        Assert.Contains("42", json);
    }

    [Fact]
    public static void AddProperty_ValueBuilderAction_CharSpan_NestedObject()
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

        Assert.Contains("nested", json);
        Assert.Contains("99", json);
    }

    #endregion

    #region AddProperty with ValueBuilderAction<TContext> delegate

    [Fact]
    public static void AddProperty_ValueBuilderActionWithContext_NestedObject()
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

        Assert.Contains("77", json);
    }

    [Fact]
    public static void AddProperty_ValueBuilderActionWithContext_CharSpan_NestedObject()
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

        Assert.Contains("88", json);
    }

    #endregion

    #region AddPropertyFormattedNumber — char-span property name

    [Fact]
    public static void AddPropertyFormattedNumber_CharSpanName()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddPropertyFormattedNumber("num".AsSpan(), Encoding.UTF8.GetBytes("1234"));
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("1234", json);
    }

    #endregion

    #region AddPropertyRawString — char-span property name

    [Fact]
    public static void AddPropertyRawString_CharSpanName()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddPropertyRawString("raw".AsSpan(), Encoding.UTF8.GetBytes("data"), false);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("data", json);
    }

    #endregion

#if NET

    #region AddProperty — Int128

    [Fact]
    public static void AddProperty_Int128_Utf8Name()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty(Encoding.UTF8.GetBytes("val"), (Int128)999888777666);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("999888777666", json);
    }

    [Fact]
    public static void AddProperty_Int128_CharSpanName()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("val".AsSpan(), (Int128)111222333);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("111222333", json);
    }

    #endregion

    #region AddProperty — UInt128

    [Fact]
    public static void AddProperty_UInt128_Utf8Name()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty(Encoding.UTF8.GetBytes("val"), (UInt128)888777666555);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("888777666555", json);
    }

    [Fact]
    public static void AddProperty_UInt128_CharSpanName()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("val".AsSpan(), (UInt128)444555666);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("444555666", json);
    }

    #endregion

    #region AddProperty — Half char-span

    [Fact]
    public static void AddProperty_Half_CharSpanName()
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        cvb.AddProperty("h".AsSpan(), (Half)1.5);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        string json = documentBuilder.RootElement.ToString();

        Assert.Contains("1.5", json);
    }

    #endregion

#endif
}
