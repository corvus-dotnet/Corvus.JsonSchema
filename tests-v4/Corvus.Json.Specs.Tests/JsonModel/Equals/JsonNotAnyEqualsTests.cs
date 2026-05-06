// <copyright file="JsonNotAnyEqualsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.Equals;

/// <summary>
/// Tests for JsonNotAnyEquals.
/// </summary>
public class JsonNotAnyEqualsTests
{
    [Fact]
    public void Equals_for_json_element_backed_value_as_a_notAny__1_2_3__1_2_3_true()
    {
        var sut = JsonNotAny.ParseValue("[1,\"2\",3]".AsSpan());
        var other = JsonNotAny.Parse("[1,\"2\",3]");
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
    public void Equals_for_json_element_backed_value_as_a_notAny__1_2_3__3_2_1_false()
    {
        var sut = JsonNotAny.ParseValue("[1,\"2\",3]".AsSpan());
        var other = JsonNotAny.Parse("[3,\"2\",1]");
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
    public void Equals_for_json_element_backed_value_as_a_notAny__1_2_3__1_2_3_false()
    {
        var sut = JsonNotAny.ParseValue("[1,\"2\",3]".AsSpan());
        var other = JsonNotAny.Parse("[1,2,3]");
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
    public void Equals_for_json_element_backed_value_as_a_notAny___true()
    {
        var sut = JsonNotAny.ParseValue("[]".AsSpan());
        var other = JsonNotAny.Parse("[]");
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
    public void Equals_for_json_element_backed_value_as_a_notAny___3_2_1_false()
    {
        var sut = JsonNotAny.ParseValue("[]".AsSpan());
        var other = JsonNotAny.Parse("[3,2,1]");
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
    public void Equals_for_json_element_backed_value_as_a_notAny_true_true_true()
    {
        var sut = JsonNotAny.ParseValue("true".AsSpan());
        var other = JsonNotAny.Parse("true");
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
    public void Equals_for_json_element_backed_value_as_a_notAny_false_false_true()
    {
        var sut = JsonNotAny.ParseValue("false".AsSpan());
        var other = JsonNotAny.Parse("false");
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
    public void Equals_for_json_element_backed_value_as_a_notAny_true_false_false()
    {
        var sut = JsonNotAny.ParseValue("true".AsSpan());
        var other = JsonNotAny.Parse("false");
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
    public void Equals_for_json_element_backed_value_as_a_notAny_false_true_false()
    {
        var sut = JsonNotAny.ParseValue("false".AsSpan());
        var other = JsonNotAny.Parse("true");
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
    public void Equals_for_json_element_backed_value_as_a_notAny_true_null_false()
    {
        var sut = JsonNotAny.ParseValue("true".AsSpan());
        var other = JsonNotAny.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_a_notAny__1__1_true()
    {
        var sut = JsonNotAny.ParseValue("1".AsSpan());
        var other = JsonNotAny.Parse("1");
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
    public void Equals_for_json_element_backed_value_as_a_notAny__1_1__1_1_true()
    {
        var sut = JsonNotAny.ParseValue("1.1".AsSpan());
        var other = JsonNotAny.Parse("1.1");
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
    public void Equals_for_json_element_backed_value_as_a_notAny__1_1__1_false()
    {
        var sut = JsonNotAny.ParseValue("1.1".AsSpan());
        var other = JsonNotAny.Parse("1");
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
    public void Equals_for_json_element_backed_value_as_a_notAny__1_1__3_false()
    {
        var sut = JsonNotAny.ParseValue("1.1".AsSpan());
        var other = JsonNotAny.Parse("3");
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
    public void Equals_for_json_element_backed_value_as_a_notAny_first_1_first_1_true()
    {
        var sut = JsonNotAny.ParseValue("{ \"first\": \"1\" }".AsSpan());
        var other = JsonNotAny.Parse("{ \"first\": \"1\" }");
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
    public void Equals_for_json_element_backed_value_as_a_notAny_first_1_first_2_false()
    {
        var sut = JsonNotAny.ParseValue("{ \"first\": \"1\" }".AsSpan());
        var other = JsonNotAny.Parse("{ \"first\": \"2\" }");
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
    public void Equals_for_json_element_backed_value_as_a_notAny_first_1_second_1_false()
    {
        var sut = JsonNotAny.ParseValue("{ \"first\": \"1\" }".AsSpan());
        var other = JsonNotAny.Parse("{ \"second\": \"1\" }");
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
    public void Equals_for_json_element_backed_value_as_a_notAny_first_1_first_1_second_1_false()
    {
        var sut = JsonNotAny.ParseValue("{ \"first\": \"1\" }".AsSpan());
        var other = JsonNotAny.Parse("{ \"first\": \"1\", \"second\": \"1\" }");
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
    public void Equals_for_json_element_backed_value_as_a_notAny_first_1_first_1_second_1_false_2()
    {
        var sut = JsonNotAny.ParseValue("{ \"first\": \"1\" }".AsSpan());
        var other = JsonNotAny.Parse("{ \"first\": 1, \"second\": \"1\" }");
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
    public void Equals_for_json_element_backed_value_as_a_notAny_Hello_Hello_true()
    {
        var sut = JsonNotAny.ParseValue("\"Hello\"".AsSpan());
        var other = JsonNotAny.Parse("\"Hello\"");
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
    public void Equals_for_json_element_backed_value_as_a_notAny_Hello_Goodbye_false()
    {
        var sut = JsonNotAny.ParseValue("\"Hello\"".AsSpan());
        var other = JsonNotAny.Parse("\"Goodbye\"");
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
    public void Equals_for_json_element_backed_value_as_a_notAny_null_null_true()
    {
        var sut = JsonNotAny.ParseValue("null".AsSpan());
        var other = JsonNotAny.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_a_notAny_null__1_2_3_false()
    {
        var sut = JsonNotAny.ParseValue("null".AsSpan());
        var other = JsonNotAny.Parse("[1,2,3]");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny__1_2_3__1_2_3_true()
    {
        var sut = JsonNotAny.Parse("[1,\"2\",3]").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("[1,\"2\",3]");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny__1_2_3__3_2_1_false()
    {
        var sut = JsonNotAny.Parse("[1,\"2\",3]").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("[3,\"2\",1]");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny__1_2_3__1_2_3_false()
    {
        var sut = JsonNotAny.Parse("[1,\"2\",3]").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("[1,2,3]");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny___true()
    {
        var sut = JsonNotAny.Parse("[]").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("[]");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny___3_2_1_false()
    {
        var sut = JsonNotAny.Parse("[]").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("[3,2,1]");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny_false_true_false()
    {
        var sut = JsonNotAny.Parse("false").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("true");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny_false_false_true()
    {
        var sut = JsonNotAny.Parse("false").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("false");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny_true_true_true()
    {
        var sut = JsonNotAny.Parse("true").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("true");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny_true_false_false()
    {
        var sut = JsonNotAny.Parse("true").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("false");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny__1__1_true()
    {
        var sut = JsonNotAny.Parse("1").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("1");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny__1_1__1_1_true()
    {
        var sut = JsonNotAny.Parse("1.1").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("1.1");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny__1_1__1_false()
    {
        var sut = JsonNotAny.Parse("1.1").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("1");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny__1__3_false()
    {
        var sut = JsonNotAny.Parse("1").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("3");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny_first_1_first_1_true()
    {
        var sut = JsonNotAny.Parse("{ \"first\": \"1\" }").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("{ \"first\": \"1\" }");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny_first_1_second_2_third_first_1_first_1_second_2_third_first_1_true()
    {
        var sut = JsonNotAny.Parse("{ \"first\": \"1\", \"second\": 2, \"third\": { \"first\": 1 } }").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("{ \"first\": \"1\", \"second\": 2, \"third\": { \"first\": 1 } }");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny_first_1_first_2_false()
    {
        var sut = JsonNotAny.Parse("{ \"first\": \"1\" }").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("{ \"first\": \"2\" }");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny_first_1_second_1_false()
    {
        var sut = JsonNotAny.Parse("{ \"first\": \"1\" }").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("{ \"second\": \"1\" }");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny_first_1_first_1_second_1_false()
    {
        var sut = JsonNotAny.Parse("{ \"first\": \"1\" }").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("{ \"first\": \"1\", \"second\": \"1\" }");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny_first_1_first_1_second_1_false_2()
    {
        var sut = JsonNotAny.Parse("{ \"first\": \"1\" }").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("{ \"first\": 1, \"second\": \"1\" }");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny_Hello_Hello_true()
    {
        var sut = JsonNotAny.Parse("\"Hello\"").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("\"Hello\"");
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
    public void Equals_for_dotnet_backed_value_as_a_notAny_Hello_Goodbye_false()
    {
        var sut = JsonNotAny.Parse("\"Hello\"").AsDotnetBackedValue();
        var other = JsonNotAny.Parse("\"Goodbye\"");
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
    public void Equals_for_notAny_json_element_backed_value_as_an_IJsonValue__1_2_3__1_2_3_true()
    {
        var sut = JsonNotAny.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_json_element_backed_value_as_an_IJsonValue_true_true_true()
    {
        var sut = JsonNotAny.ParseValue("true".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_json_element_backed_value_as_an_IJsonValue_false_false_true()
    {
        var sut = JsonNotAny.ParseValue("false".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_json_element_backed_value_as_an_IJsonValue__1_1__1_1_true()
    {
        var sut = JsonNotAny.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_json_element_backed_value_as_an_IJsonValue_first_1_first_1_true()
    {
        var sut = JsonNotAny.ParseValue("{ \"first\": \"1\" }".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_json_element_backed_value_as_an_IJsonValue_Hello_Hello_true()
    {
        var sut = JsonNotAny.ParseValue("\"Hello\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_IJsonValue__1_2_3__1_2_3_true()
    {
        var sut = JsonNotAny.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_IJsonValue_true_true_true()
    {
        var sut = JsonNotAny.Parse("true").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_IJsonValue_false_false_true()
    {
        var sut = JsonNotAny.Parse("false").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_IJsonValue__1_1__1_1_true()
    {
        var sut = JsonNotAny.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_IJsonValue_first_1_first_1_true()
    {
        var sut = JsonNotAny.Parse("{ \"first\": \"1\" }").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_IJsonValue_Hello_Hello_true()
    {
        var sut = JsonNotAny.Parse("\"Hello\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_json_element_backed_value_as_an_object__1_2_3__1_2_3_true()
    {
        var sut = JsonNotAny.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_json_element_backed_value_as_an_object_true_true_true()
    {
        var sut = JsonNotAny.ParseValue("true".AsSpan());
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_json_element_backed_value_as_an_object_false_false_true()
    {
        var sut = JsonNotAny.ParseValue("false".AsSpan());
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_json_element_backed_value_as_an_object__1_1__1_1_true()
    {
        var sut = JsonNotAny.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_json_element_backed_value_as_an_object_first_1_first_1_true()
    {
        var sut = JsonNotAny.ParseValue("{ \"first\": \"1\" }".AsSpan());
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_json_element_backed_value_as_an_object_Hello_Hello_true()
    {
        var sut = JsonNotAny.ParseValue("\"Hello\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_object__1_2_3__1_2_3_true()
    {
        var sut = JsonNotAny.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_object_true_true_true()
    {
        var sut = JsonNotAny.Parse("true").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_object_false_false_true()
    {
        var sut = JsonNotAny.Parse("false").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_object__1_1__1_1_true()
    {
        var sut = JsonNotAny.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_object_first_1_first_1_true()
    {
        var sut = JsonNotAny.Parse("{ \"first\": \"1\" }").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_object_Hello_Hello_true()
    {
        var sut = JsonNotAny.Parse("\"Hello\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_object__1_2_3_new_object_false()
    {
        var sut = JsonNotAny.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = new object();
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_object__1_2_3_null_false()
    {
        var sut = JsonNotAny.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_object__1_2_3_null_false_2()
    {
        var sut = JsonNotAny.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_object__1_2_3_undefined_false()
    {
        var sut = JsonNotAny.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = default(JsonNotAny);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_object_undefined_Hello_false()
    {
        var sut = default(JsonNotAny);
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_object_null_null_true()
    {
        var sut = JsonNotAny.Null;
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_object_null_null_true_2()
    {
        var sut = JsonNotAny.Null;
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_notAny_dotnet_backed_value_as_an_object_null_undefined_false()
    {
        var sut = JsonNotAny.Null;
        object? obj = default(JsonNotAny);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }
}