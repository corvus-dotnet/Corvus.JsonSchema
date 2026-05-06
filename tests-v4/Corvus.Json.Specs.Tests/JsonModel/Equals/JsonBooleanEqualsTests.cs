// <copyright file="JsonBooleanEqualsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.Equals;

/// <summary>
/// Tests for JsonBooleanEquals.
/// </summary>
public class JsonBooleanEqualsTests
{
    [Fact]
    public void Equals_for_json_element_backed_value_as_a_boolean_true_true_true()
    {
        var sut = JsonBoolean.ParseValue("true".AsSpan());
        var other = JsonBoolean.Parse("true");
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
    public void Equals_for_json_element_backed_value_as_a_boolean_false_false_true()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        var other = JsonBoolean.Parse("false");
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
    public void Equals_for_json_element_backed_value_as_a_boolean_true_false_false()
    {
        var sut = JsonBoolean.ParseValue("true".AsSpan());
        var other = JsonBoolean.Parse("false");
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
    public void Equals_for_json_element_backed_value_as_a_boolean_false_true_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        var other = JsonBoolean.Parse("true");
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
    public void Equals_for_json_element_backed_value_as_a_boolean_true_null_false()
    {
        var sut = JsonBoolean.ParseValue("true".AsSpan());
        var other = JsonBoolean.ParseValue("null".AsSpan());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_boolean_null_true_false()
    {
        var sut = JsonBoolean.ParseValue("null".AsSpan());
        var other = JsonBoolean.Parse("true");
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
    public void Equals_for_json_element_backed_value_as_a_boolean_null_null_true()
    {
        var sut = JsonBoolean.ParseValue("null".AsSpan());
        var other = JsonBoolean.ParseValue("null".AsSpan());
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
    public void Equals_for_dotnet_backed_value_as_a_boolean_false_true_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        var other = JsonBoolean.Parse("true");
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
    public void Equals_for_dotnet_backed_value_as_a_boolean_false_false_true()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        var other = JsonBoolean.Parse("false");
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
    public void Equals_for_dotnet_backed_value_as_a_boolean_true_true_true()
    {
        var sut = JsonBoolean.Parse("true").AsDotnetBackedValue();
        var other = JsonBoolean.Parse("true");
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
    public void Equals_for_dotnet_backed_value_as_a_boolean_true_false_false()
    {
        var sut = JsonBoolean.Parse("true").AsDotnetBackedValue();
        var other = JsonBoolean.Parse("false");
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
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_false_Hello_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_false__1_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_false__1_1_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_false__1_2_3_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_false_first_1_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_true_true_true()
    {
        var sut = JsonBoolean.ParseValue("true".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_false_false_true()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_true_false_false()
    {
        var sut = JsonBoolean.ParseValue("true".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_false_true_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_false__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_false_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_false__2018_11_13_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_false_hello_endjin_com_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_false_www_example_com_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_false_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_false_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_false_first_1_false_2()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_false__192_168_0_1_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_IJsonValue_false__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_false_Hello_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_false__1_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_false__1_1_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_false__1_2_3_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_false_first_1_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_true_true_true()
    {
        var sut = JsonBoolean.Parse("true").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_false_false_true()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_true_false_false()
    {
        var sut = JsonBoolean.Parse("true").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_false_true_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_false__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_false_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_false__2018_11_13_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_false_hello_endjin_com_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_false_www_example_com_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_false_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_false_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_false_first_1_false_2()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_false__192_168_0_1_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_IJsonValue_false__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false_Hello_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false__1_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false__1_1_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false__1_2_3_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false_first_1_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_true_true_true()
    {
        var sut = JsonBoolean.ParseValue("true".AsSpan());
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false_false_true()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_true_false_false()
    {
        var sut = JsonBoolean.ParseValue("true".AsSpan());
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false_true_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("\"2018-11-13T20:20:39+00:00\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("\"P3Y6M4DT12H30M5S\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false__2018_11_13_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("\"2018-11-13\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false_hello_endjin_com_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("\"hello@endjin.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false_www_example_com_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("\"www.example.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false_first_1_false_2()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false__192_168_0_1_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("\"192.168.0.1\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false_new_object_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = new object();
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_json_element_backed_value_as_an_object_false_null_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false_Hello_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false__1_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false__1_1_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false__1_2_3_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false_first_1_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_true_true_true()
    {
        var sut = JsonBoolean.Parse("true").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false_false_true()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_true_false_false()
    {
        var sut = JsonBoolean.Parse("true").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false_true_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"2018-11-13T20:20:39+00:00\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"P3Y6M4DT12H30M5S\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false__2018_11_13_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"2018-11-13\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false_hello_endjin_com_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"hello@endjin.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false_www_example_com_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"www.example.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false_first_1_false_2()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false__192_168_0_1_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"192.168.0.1\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false_new_object_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = new object();
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false_null_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false_null_false_2()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_false_undefined_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        object? obj = default(JsonBoolean);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_null_null_true()
    {
        var sut = JsonBoolean.Null;
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_null_null_true_2()
    {
        var sut = JsonBoolean.Null;
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_boolean_dotnet_backed_value_as_an_object_null_undefined_false()
    {
        var sut = JsonBoolean.Null;
        object? obj = default(JsonBoolean);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }
}