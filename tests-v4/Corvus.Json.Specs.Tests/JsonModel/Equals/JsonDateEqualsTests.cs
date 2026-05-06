// <copyright file="JsonDateEqualsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.Equals;

/// <summary>
/// Tests for JsonDateEquals.
/// </summary>
public class JsonDateEqualsTests
{
    [Fact]
    public void Equals_for_json_element_backed_value_as_a_date__2018_11_13__2018_11_13_true()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        var other = JsonDate.Parse("\"2018-11-13\"");
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_date_Garbage__2018_11_13_false()
    {
        var sut = JsonDate.ParseValue("\"Garbage\"".AsSpan());
        var other = JsonDate.Parse("\"2018-11-13\"");
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_date__2018_11_13_Goodbye_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        var other = JsonDate.Parse("\"Goodbye\"");
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_date_null_null_true()
    {
        var sut = JsonDate.ParseValue("null".AsSpan());
        var other = JsonDate.ParseValue("null".AsSpan());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_date_null__2018_11_13_false()
    {
        var sut = JsonDate.ParseValue("null".AsSpan());
        var other = JsonDate.Parse("\"2018-11-13\"");
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_date_Garbage__2018_11_13_false()
    {
        var sut = JsonDate.Parse("\"Garbage\"").AsDotnetBackedValue();
        var other = JsonDate.Parse("\"2018-11-13\"");
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_date__2018_11_13__2018_11_13_true()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        var other = JsonDate.Parse("\"2018-11-13\"");
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_date__2018_11_13_Goodbye_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        var other = JsonDate.Parse("\"Goodbye\"");
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13_Hello_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13_Goodbye_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13__1_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13__1_1_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13__1_2_3_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13_first_1_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13_true_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13_false_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13__2018_11_13_true()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue_Garbage__2018_11_13_false()
    {
        var sut = JsonDate.ParseValue("\"Garbage\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13_hello_endjin_com_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13_www_example_com_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13_first_1_false_2()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13__192_168_0_1_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_IJsonValue__2018_11_13__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13_Hello_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13_Goodbye_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13__1_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13__1_1_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13__1_2_3_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13_first_1_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13_true_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13_false_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13__2018_11_13_true()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue_Garbage__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonDate.Parse("\"Garbage\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13_hello_endjin_com_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13_www_example_com_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13_first_1_false_2()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13__192_168_0_1_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_IJsonValue__2018_11_13__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13_Hello_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13_Goodbye_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"Goodbye\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13__1_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13__1_1_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13__1_2_3_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13_first_1_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13_true_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13_false_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"2018-11-13T20:20:39+00:00\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"P3Y6M4DT12H30M5S\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13__2018_11_13_true()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"2018-11-13\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13_hello_endjin_com_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"hello@endjin.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13_www_example_com_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"www.example.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13_first_1_false_2()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13__192_168_0_1_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"192.168.0.1\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13_new_object_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = new object();
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_json_element_backed_value_as_an_object__2018_11_13_null_false()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13_Hello_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13_Goodbye_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"Goodbye\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13__1_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13__1_1_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13__1_2_3_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13_first_1_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13_true_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13_false_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"2018-11-13T20:20:39+00:00\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"P3Y6M4DT12H30M5S\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13__2018_11_13_true()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"2018-11-13\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13_hello_endjin_com_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"hello@endjin.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13_www_example_com_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"www.example.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13_first_1_false_2()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13__192_168_0_1_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"192.168.0.1\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13_new_object_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = new object();
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13_null_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13_null_false_2()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object__2018_11_13_undefined_false()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        object? obj = default(JsonDate);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object_null_null_true()
    {
        var sut = JsonDate.Null;
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object_null_null_true_2()
    {
        var sut = JsonDate.Null;
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_date_dotnet_backed_value_as_an_object_null_undefined_false()
    {
        var sut = JsonDate.Null;
        object? obj = default(JsonDate);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }
}