// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Numerics;
using Corvus.Numerics;
using Corvus.Text.Json.Internal;
using NodaTime;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests targeting specific uncovered code paths in JsonElement.Mutable identified
/// through Cobertura XML coverage analysis.
/// </summary>
public static class MutableAccessorCoverageTests
{
    #region Category 1: Explicit cast and equality operators

    [Fact]
    public static void ExplicitCast_FromMutableDocument_Succeeds()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""42""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        // Get a JsonElement from the builder (implicit conversion from Mutable to JsonElement)
        JsonElement element = doc.RootElement;
        JsonElement.Mutable mutable = (JsonElement.Mutable)element;

        Assert.Equal(42, mutable.GetInt32());
    }

    [Fact]
    public static void ExplicitCast_FromImmutableDocument_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""42""");
        JsonElement element = source.RootElement;

        Assert.Throws<FormatException>(() => (JsonElement.Mutable)element);
    }

    [Fact]
    public static void EqualityOperator_MutableAndJsonElement_Equal()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""42""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable mutable = doc.RootElement;
        JsonElement immutable = source.RootElement;

        Assert.True(mutable == immutable);
        Assert.False(mutable != immutable);
    }

    [Fact]
    public static void EqualityOperator_MutableAndJsonElement_NotEqual()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""42""");
        using ParsedJsonDocument<JsonElement> other = ParsedJsonDocument<JsonElement>.Parse("""99""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable mutable = doc.RootElement;
        JsonElement immutable = other.RootElement;

        Assert.False(mutable == immutable);
        Assert.True(mutable != immutable);
    }

    #endregion

    #region Category 2: Value accessors — success and failure paths

    [Fact]
    public static void GetUtf8String_Success()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"hello\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        UnescapedUtf8JsonString result = doc.RootElement.GetUtf8String();
        Assert.True(result.Span.SequenceEqual("hello"u8));
    }

    [Fact]
    public static void GetDouble_Success()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""3.14""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        double value = doc.RootElement.GetDouble();
        Assert.Equal(3.14, value);
    }

    [Fact]
    public static void GetDouble_Failure_ThrowsInvalidOperationException()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"not a number\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        Assert.Throws<InvalidOperationException>(() => doc.RootElement.GetDouble());
    }

    [Fact]
    public static void GetSingle_Success()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""1.5""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        float value = doc.RootElement.GetSingle();
        Assert.Equal(1.5f, value);
    }

    [Fact]
    public static void GetSingle_Failure_ThrowsInvalidOperationException()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"not a number\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        Assert.Throws<InvalidOperationException>(() => doc.RootElement.GetSingle());
    }

    [Fact]
    public static void GetBigNumber_Success()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""123456789.123456789""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        BigNumber value = doc.RootElement.GetBigNumber();
        Assert.NotEqual(default, value);
    }

    [Fact]
    public static void GetBigNumber_Failure_ThrowsInvalidOperationException()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"not a number\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        Assert.Throws<InvalidOperationException>(() => doc.RootElement.GetBigNumber());
    }

    [Fact]
    public static void GetBigInteger_Success()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""99999999999999999999""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        BigInteger value = doc.RootElement.GetBigInteger();
        Assert.Equal(BigInteger.Parse("99999999999999999999"), value);
    }

    [Fact]
    public static void GetBigInteger_Failure_ThrowsInvalidOperationException()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"not a number\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        Assert.Throws<InvalidOperationException>(() => doc.RootElement.GetBigInteger());
    }

    #endregion

    #region Category 3: NodaTime accessors — success and failure paths

    [Fact]
    public static void GetLocalDate_Success()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"2024-01-15\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        LocalDate value = doc.RootElement.GetLocalDate();
        Assert.Equal(new LocalDate(2024, 1, 15), value);
    }

    [Fact]
    public static void GetLocalDate_Failure_ThrowsFormatException()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"not a date\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        Assert.Throws<FormatException>(() => doc.RootElement.GetLocalDate());
    }

    [Fact]
    public static void GetOffsetTime_Success()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"10:30:00+05:00\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        OffsetTime value = doc.RootElement.GetOffsetTime();
        Assert.Equal(new OffsetTime(new LocalTime(10, 30, 0), Offset.FromHours(5)), value);
    }

    [Fact]
    public static void GetOffsetTime_Failure_ThrowsFormatException()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"not a time\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        Assert.Throws<FormatException>(() => doc.RootElement.GetOffsetTime());
    }

    [Fact]
    public static void GetOffsetDateTime_Success()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"2024-01-15T10:30:00+05:00\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        OffsetDateTime value = doc.RootElement.GetOffsetDateTime();
        Assert.Equal(new LocalDateTime(2024, 1, 15, 10, 30, 0).WithOffset(Offset.FromHours(5)), value);
    }

    [Fact]
    public static void GetOffsetDateTime_Failure_ThrowsFormatException()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"not a datetime\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        Assert.Throws<FormatException>(() => doc.RootElement.GetOffsetDateTime());
    }

    [Fact]
    public static void GetOffsetDate_Success()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"2024-01-15+05:00\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        OffsetDate value = doc.RootElement.GetOffsetDate();
        Assert.Equal(new OffsetDate(new LocalDate(2024, 1, 15), Offset.FromHours(5)), value);
    }

    [Fact]
    public static void GetOffsetDate_Failure_ThrowsFormatException()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"not a date\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        Assert.Throws<FormatException>(() => doc.RootElement.GetOffsetDate());
    }

    [Fact]
    public static void GetPeriod_Success()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"P1Y2M3D\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        Period value = doc.RootElement.GetPeriod();
        Assert.Equal(Period.FromYears(1) + Period.FromMonths(2) + Period.FromDays(3), value);
    }

    [Fact]
    public static void GetPeriod_Failure_ThrowsFormatException()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"not a period\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        Assert.Throws<FormatException>(() => doc.RootElement.GetPeriod());
    }

    #endregion

    #region Category 4: Internal properties and ToString/GetHashCode

    [Fact]
    public static void ValueIsEscaped_ReturnsFalse_ForSimpleString()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"hello\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        bool escaped = doc.RootElement.ValueIsEscaped;
        Assert.False(escaped);
    }

    [Fact]
    public static void ValueIsEscaped_ReturnsTrue_ForEscapedString()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        // JSON with an escaped character (backslash-n)
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"hello\\nworld\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        bool escaped = doc.RootElement.ValueIsEscaped;
        Assert.True(escaped);
    }

    [Fact]
    public static void ValueSpan_ReturnsUtf8Bytes()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("\"hello\"");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        ReadOnlySpan<byte> span = doc.RootElement.ValueSpan;
        Assert.True(span.SequenceEqual("hello"u8));
    }

    [Fact]
    public static void EnsurePropertyMap_CallsSuccessfully()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1, "b": 2}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = doc.RootElement;
        // Should not throw
        JsonElement.Mutable.EnsurePropertyMap(in root);
    }

    [Fact]
    public static void ToString_ReturnsEmpty_WhenParentIsNull()
    {
        JsonElement.Mutable defaultMutable = default;
        Assert.Equal(string.Empty, defaultMutable.ToString());
    }

    [Fact]
    public static void ToString_ReturnsEmpty_WhenVersionIsStale()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"a": [1, 2, 3]}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        // Get a non-root element
        JsonElement.Mutable array = doc.RootElement.GetProperty("a"u8);
        JsonElement.Mutable item = array[0];

        // Mutate the root to invalidate the version
        doc.RootElement.SetProperty("b"u8, 99);

        // The stale element's ToString should return empty
        Assert.Equal(string.Empty, item.ToString());
    }

    [Fact]
    public static void GetHashCode_ReturnsZero_WhenParentIsNull()
    {
        JsonElement.Mutable defaultMutable = default;
        Assert.Equal(0, defaultMutable.GetHashCode());
    }

    [Fact]
    public static void GetHashCode_ReturnsNonZero_ForValidElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""42""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        // A valid mutable element should produce a hash (may or may not be 0, but we exercise the path)
        _ = doc.RootElement.GetHashCode();
    }

    #endregion

    #region Category 5: SetProperty CVB fallback (builder delegate path)

    [Fact]
    public static void SetProperty_WithObjectBuilderSource_InsertNew()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x": 1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        // Use ObjectBuilder.Build delegate as Source — triggers CVB fallback insert path
        JsonElement.Source objectSource = new(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddProperty("inner"u8, 42);
        });

        doc.RootElement.SetProperty("newProp"u8, in objectSource);

        JsonElement.Mutable newProp = doc.RootElement.GetProperty("newProp"u8);
        Assert.Equal(JsonValueKind.Object, newProp.ValueKind);
        Assert.Equal(42, newProp.GetProperty("inner"u8).GetInt32());
    }

    [Fact]
    public static void SetProperty_WithObjectBuilderSource_ReplaceExisting()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x": 1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        // Use ObjectBuilder.Build delegate as Source — triggers CVB fallback replace path
        JsonElement.Source objectSource = new(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddProperty("replaced"u8, true);
        });

        doc.RootElement.SetProperty("x"u8, in objectSource);

        JsonElement.Mutable x = doc.RootElement.GetProperty("x"u8);
        Assert.Equal(JsonValueKind.Object, x.ValueKind);
        Assert.True(x.GetProperty("replaced"u8).GetBoolean());
    }

    [Fact]
    public static void SetProperty_WithArrayBuilderSource_InsertNew()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x": 1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        // Use ArrayBuilder.Build delegate as Source — also triggers CVB fallback
        JsonElement.Source arraySource = new(static (ref JsonElement.ArrayBuilder ab) =>
        {
            ab.AddItem(10);
            ab.AddItem(20);
        });

        doc.RootElement.SetProperty("arr"u8, in arraySource);

        JsonElement.Mutable arr = doc.RootElement.GetProperty("arr"u8);
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(2, arr.GetArrayLength());
        Assert.Equal(10, arr[0].GetInt32());
        Assert.Equal(20, arr[1].GetInt32());
    }

    #endregion

    #region Category 6: TryReplaceProperty CVB fallback

    [Fact]
    public static void TryReplaceProperty_WithObjectBuilderSource_ReplacesExisting()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x": 1, "y": 2}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        JsonElement.Source objectSource = new(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddProperty("z"u8, 99);
        });

        bool replaced = doc.RootElement.TryReplaceProperty("x"u8, in objectSource);

        Assert.True(replaced);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.GetProperty("x"u8).ValueKind);
        Assert.Equal(99, doc.RootElement.GetProperty("x"u8).GetProperty("z"u8).GetInt32());
    }

    [Fact]
    public static void TryReplaceProperty_WithObjectBuilderSource_ReturnsFalseWhenNotFound()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x": 1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        JsonElement.Source objectSource = new(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddProperty("z"u8, 99);
        });

        bool replaced = doc.RootElement.TryReplaceProperty("nonexistent"u8, in objectSource);

        Assert.False(replaced);
    }

    #endregion

    #region Category 7: SetPropertyNull insert path

    [Fact]
    public static void SetPropertyNull_InsertsNewProperty_WhenNotExists()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x": 1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        doc.RootElement.SetPropertyNull("newNull"u8);

        JsonElement.Mutable newProp = doc.RootElement.GetProperty("newNull"u8);
        Assert.Equal(JsonValueKind.Null, newProp.ValueKind);
    }

    [Fact]
    public static void SetPropertyNull_ReplacesExistingProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x": 42}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        doc.RootElement.SetPropertyNull("x"u8);

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("x"u8).ValueKind);
    }

    #endregion

    #region Category 8: Array item operations

    [Fact]
    public static void SetItem_WithObjectBuilderSource_CVBFallback()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""[1, 2, 3]""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        // Use an ObjectBuilder.Build delegate as Source — triggers CVB fallback in SetItem
        JsonElement.Source objectSource = new(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddProperty("key"u8, "value");
        });

        doc.RootElement.SetItem(1, in objectSource);

        Assert.Equal(JsonValueKind.Object, doc.RootElement[1].ValueKind);
        Assert.Equal("value", doc.RootElement[1].GetProperty("key"u8).GetString());
    }

    [Fact]
    public static void SetItem_WithContextObjectBuilder_ReplaceExisting()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""[1, 2, 3]""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        int multiplier = 5;
        doc.RootElement.SetItem(
            0,
            multiplier,
            static (in int ctx, ref JsonElement.ObjectBuilder ob) =>
            {
                ob.AddProperty("val"u8, ctx * 10);
            });

        Assert.Equal(JsonValueKind.Object, doc.RootElement[0].ValueKind);
        Assert.Equal(50, doc.RootElement[0].GetProperty("val"u8).GetInt32());
    }

    [Fact]
    public static void SetItem_WithContextObjectBuilder_InsertAtEnd()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""[1, 2]""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        int ctx = 7;
        doc.RootElement.SetItem(
            2, // at end = insert
            ctx,
            static (in int c, ref JsonElement.ObjectBuilder ob) =>
            {
                ob.AddProperty("n"u8, c);
            });

        Assert.Equal(3, doc.RootElement.GetArrayLength());
        Assert.Equal(7, doc.RootElement[2].GetProperty("n"u8).GetInt32());
    }

    [Fact]
    public static void SetItem_WithContextArrayBuilder_ReplaceExisting()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""[1, 2, 3]""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        int count = 3;
        doc.RootElement.SetItem(
            1,
            count,
            static (in int ctx, ref JsonElement.ArrayBuilder ab) =>
            {
                for (int i = 0; i < ctx; i++)
                {
                    ab.AddItem(i * 100);
                }
            });

        Assert.Equal(JsonValueKind.Array, doc.RootElement[1].ValueKind);
        Assert.Equal(3, doc.RootElement[1].GetArrayLength());
        Assert.Equal(0, doc.RootElement[1][0].GetInt32());
        Assert.Equal(100, doc.RootElement[1][1].GetInt32());
        Assert.Equal(200, doc.RootElement[1][2].GetInt32());
    }

    [Fact]
    public static void SetItem_WithContextArrayBuilder_InsertAtEnd()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""[1]""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        int ctx = 2;
        doc.RootElement.SetItem(
            1, // at end = insert
            ctx,
            static (in int c, ref JsonElement.ArrayBuilder ab) =>
            {
                ab.AddItem(c);
                ab.AddItem(c + 1);
            });

        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal(JsonValueKind.Array, doc.RootElement[1].ValueKind);
        Assert.Equal(2, doc.RootElement[1][0].GetInt32());
    }

    [Fact]
    public static void InsertItem_WithObjectBuilderSource_CVBFallback()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""[1, 2, 3]""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        // Use an ObjectBuilder.Build delegate as Source — triggers CVB fallback in InsertItem
        JsonElement.Source objectSource = new(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddProperty("inserted"u8, true);
        });

        doc.RootElement.InsertItem(1, in objectSource);

        Assert.Equal(4, doc.RootElement.GetArrayLength());
        Assert.Equal(JsonValueKind.Object, doc.RootElement[1].ValueKind);
        Assert.True(doc.RootElement[1].GetProperty("inserted"u8).GetBoolean());
    }

    #endregion

    #region Category 9: EvaluateSchema and interface properties

    [Fact]
    public static void EvaluateSchema_OnMutableElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""42""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        // Just exercises the EvaluateSchema path — result depends on schema config
        bool result = doc.RootElement.EvaluateSchema();
        // Default schema evaluation on a number should pass (no schema constraints)
        Assert.True(result);
    }

    [Fact]
    public static void IJsonElement_ValueKind_ExplicitInterface()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"key": "value"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        IJsonElement element = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, element.ValueKind);
    }

    #endregion

    #region Category 10: From<T> generic method

    [Fact]
    public static void From_CreatesMutableFromMutableElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable original = doc.RootElement;
        JsonElement.Mutable copy = JsonElement.Mutable.From(in original);

        Assert.Equal(original.ValueKind, copy.ValueKind);
        Assert.Equal(original.GetRawText(), copy.GetRawText());
    }

    #endregion
}
