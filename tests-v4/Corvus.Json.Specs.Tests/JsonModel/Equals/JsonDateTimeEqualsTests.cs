// <copyright file="JsonDateTimeEqualsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.Equals;

/// <summary>
/// Tests for JsonDateTimeEquals.
/// </summary>
public class JsonDateTimeEqualsTests
{
    [Fact]
    public void Equals_for_json_element_backed_value_as_a_dateTime__2018_11_13T20_20_39_00_00__2018_11_13T20_20_39_00_00_true()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        var other = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"");
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
    public void Equals_for_json_element_backed_value_as_a_dateTime_Garbage__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonDateTime.ParseValue("\"Garbage\"".AsSpan());
        var other = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"");
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
    public void Equals_for_json_element_backed_value_as_a_dateTime__2018_11_13T20_20_39_00_00_Goodbye_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        var other = JsonDateTime.Parse("\"Goodbye\"");
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
    public void Equals_for_json_element_backed_value_as_a_dateTime_null_null_true()
    {
        var sut = JsonDateTime.ParseValue("null".AsSpan());
        var other = JsonDateTime.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_a_dateTime_null__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonDateTime.ParseValue("null".AsSpan());
        var other = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"");
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
    public void Equals_for_dotnet_backed_value_as_a_dateTime_Garbage__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonDateTime.Parse("\"Garbage\"").AsDotnetBackedValue();
        var other = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"");
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
    public void Equals_for_dotnet_backed_value_as_a_dateTime__2018_11_13T20_20_39_00_00__2018_11_13T20_20_39_00_00_true()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        var other = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"");
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
    public void Equals_for_dotnet_backed_value_as_a_dateTime__2018_11_13T20_20_39_1234567_00_00__2018_11_13T20_20_39_1234567_00_00_true()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39.1234567+00:00\"").AsDotnetBackedValue();
        var other = JsonDateTime.Parse("\"2018-11-13T20:20:39.1234567+00:00\"");
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
    public void Equals_for_dotnet_backed_value_as_a_dateTime__2018_11_13T20_20_39_00_00_Goodbye_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        var other = JsonDateTime.Parse("\"Goodbye\"");
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
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_Hello_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_Goodbye_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00__1_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00__1_1_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00__1_2_3_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_first_1_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_true_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_false_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00__2018_11_13T20_20_39_00_00_true()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00__2018_11_13_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue_Garbage__2018_11_13_false()
    {
        var sut = JsonDateTime.ParseValue("\"Garbage\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_hello_endjin_com_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_www_example_com_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_first_1_false_2()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00__192_168_0_1_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_Hello_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_Goodbye_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00__1_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00__1_1_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00__1_2_3_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_first_1_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_true_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_false_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00__2018_11_13T20_20_39_00_00_true()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00__2018_11_13_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue_Garbage__2018_11_13_false()
    {
        var sut = JsonDateTime.Parse("\"Garbage\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_hello_endjin_com_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_www_example_com_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00_first_1_false_2()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00__192_168_0_1_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_IJsonValue__2018_11_13T20_20_39_00_00__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00_Hello_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00_Goodbye_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"Goodbye\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00__1_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00__1_1_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00__1_2_3_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00_first_1_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00_true_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00_false_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00__2018_11_13T20_20_39_00_00_true()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"2018-11-13T20:20:39+00:00\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"P3Y6M4DT12H30M5S\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00__2018_11_13_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"2018-11-13\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00_hello_endjin_com_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"hello@endjin.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00_www_example_com_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"www.example.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00_first_1_false_2()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00__192_168_0_1_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"192.168.0.1\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00_new_object_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = new object();
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_json_element_backed_value_as_an_object__2018_11_13T20_20_39_00_00_null_false()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00_Hello_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00_Goodbye_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"Goodbye\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00__1_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00__1_1_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00__1_2_3_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00_first_1_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00_true_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00_false_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00__2018_11_13T20_20_39_00_00_true()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"2018-11-13T20:20:39+00:00\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"P3Y6M4DT12H30M5S\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00__2018_11_13_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"2018-11-13\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00_hello_endjin_com_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"hello@endjin.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00_www_example_com_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"www.example.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00_first_1_false_2()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00__192_168_0_1_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"192.168.0.1\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00_new_object_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = new object();
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00_null_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00_null_false_2()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object__2018_11_13T20_20_39_00_00_undefined_false()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        object? obj = default(JsonDateTime);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object_null_null_true()
    {
        var sut = JsonDateTime.Null;
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object_null_null_true_2()
    {
        var sut = JsonDateTime.Null;
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_dateTime_dotnet_backed_value_as_an_object_null_undefined_false()
    {
        var sut = JsonDateTime.Null;
        object? obj = default(JsonDateTime);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }
}