// <copyright file="JsonIntegerEqualsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.Equals;

/// <summary>
/// Tests for JsonIntegerEquals.
/// </summary>
public class JsonIntegerEqualsTests
{
    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__1_true_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        var other = JsonInteger.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__3_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        var other = JsonInteger.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer_null_null_true_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("null".AsSpan());
        var other = JsonInteger.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_a_integer_null__1_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("null".AsSpan());
        var other = JsonInteger.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__1_true_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        var other = JsonInt128.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__3_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        var other = JsonInt128.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer_null_null_true_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("null".AsSpan());
        var other = JsonInt128.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_a_integer_null__1_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("null".AsSpan());
        var other = JsonInt128.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__1_true_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        var other = JsonInt64.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__3_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        var other = JsonInt64.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer_null_null_true_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("null".AsSpan());
        var other = JsonInt64.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_a_integer_null__1_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("null".AsSpan());
        var other = JsonInt64.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__1_true_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        var other = JsonInt32.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__3_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        var other = JsonInt32.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer_null_null_true_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("null".AsSpan());
        var other = JsonInt32.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_a_integer_null__1_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("null".AsSpan());
        var other = JsonInt32.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__1_true_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        var other = JsonInt16.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__3_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        var other = JsonInt16.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer_null_null_true_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("null".AsSpan());
        var other = JsonInt16.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_a_integer_null__1_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("null".AsSpan());
        var other = JsonInt16.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__1_true_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        var other = JsonSByte.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__3_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        var other = JsonSByte.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer_null_null_true_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("null".AsSpan());
        var other = JsonSByte.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_a_integer_null__1_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("null".AsSpan());
        var other = JsonSByte.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__1_true_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        var other = JsonUInt64.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__3_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        var other = JsonUInt64.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer_null_null_true_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("null".AsSpan());
        var other = JsonUInt64.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_a_integer_null__1_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("null".AsSpan());
        var other = JsonUInt64.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__1_true_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        var other = JsonUInt32.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__3_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        var other = JsonUInt32.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer_null_null_true_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("null".AsSpan());
        var other = JsonUInt32.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_a_integer_null__1_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("null".AsSpan());
        var other = JsonUInt32.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__1_true_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        var other = JsonUInt16.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__3_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        var other = JsonUInt16.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer_null_null_true_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("null".AsSpan());
        var other = JsonUInt16.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_a_integer_null__1_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("null".AsSpan());
        var other = JsonUInt16.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__1_true_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        var other = JsonByte.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__3_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        var other = JsonByte.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer_null_null_true_JsonByte()
    {
        var sut = JsonByte.ParseValue("null".AsSpan());
        var other = JsonByte.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_a_integer_null__1_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("null".AsSpan());
        var other = JsonByte.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__1_true_JsonInt128_2()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        var other = JsonInt128.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__3_false_JsonInt128_2()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        var other = JsonInt128.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer_null_null_true_JsonInt128_2()
    {
        var sut = JsonInt128.ParseValue("null".AsSpan());
        var other = JsonInt128.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_a_integer_null__1_false_JsonInt128_2()
    {
        var sut = JsonInt128.ParseValue("null".AsSpan());
        var other = JsonInt128.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__1_true_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        var other = JsonUInt128.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer__1__3_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        var other = JsonUInt128.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_json_element_backed_value_as_a_integer_null_null_true_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("null".AsSpan());
        var other = JsonUInt128.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_a_integer_null__1_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("null".AsSpan());
        var other = JsonUInt128.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__1_true_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        var other = JsonInteger.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__3_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        var other = JsonInteger.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__1_true_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        var other = JsonInt64.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__3_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        var other = JsonInt64.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__1_true_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        var other = JsonInt32.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__3_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        var other = JsonInt32.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__1_true_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        var other = JsonInt16.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__3_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        var other = JsonInt16.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__1_true_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        var other = JsonSByte.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__3_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        var other = JsonSByte.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__1_true_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        var other = JsonUInt64.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__3_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        var other = JsonUInt64.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__1_true_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        var other = JsonUInt32.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__3_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        var other = JsonUInt32.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__1_true_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        var other = JsonUInt16.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__3_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        var other = JsonUInt16.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__1_true_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        var other = JsonByte.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__3_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        var other = JsonByte.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__1_true_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        var other = JsonInt128.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__3_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        var other = JsonInt128.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer_null_null_true_JsonInt128()
    {
        var sut = JsonInt128.Null;
        var other = JsonInt128.ParseValue("null".AsSpan());
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
    public void Equals_for_dotnet_backed_value_as_a_integer_null__1_false_JsonInt128()
    {
        var sut = JsonInt128.Null;
        var other = JsonInt128.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__1_true_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        var other = JsonUInt128.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.True(equalsResult);
        Assert.True(equalityResult);
        Assert.False(inequalityResult);
        Assert.True(hashCodeResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer__1__3_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        var other = JsonUInt128.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_integer_null_null_true_JsonUInt128()
    {
        var sut = JsonUInt128.Null;
        var other = JsonUInt128.ParseValue("null".AsSpan());
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
    public void Equals_for_dotnet_backed_value_as_a_integer_null__1_false_JsonUInt128()
    {
        var sut = JsonUInt128.Null;
        var other = JsonUInt128.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.False(equalsResult);
        Assert.False(equalityResult);
        Assert.True(inequalityResult);
        Assert.False(hashCodeResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonInteger.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInteger.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonInteger.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInteger.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonInt64.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInt64.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonInt64.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInt64.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonInt32.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInt32.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonInt32.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInt32.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonInt16.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInt16.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonInt16.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInt16.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonSByte.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonSByte.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonSByte.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonSByte.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonUInt64.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonUInt64.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonUInt64.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonUInt64.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonUInt32.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonUInt32.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonUInt32.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonUInt32.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonUInt16.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonUInt16.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonUInt16.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonUInt16.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonByte.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonByte.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonByte.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonByte.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonInt128.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInt128.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonInt128.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInt128.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonUInt128.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonUInt128.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_dotnet_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsOtherBackedResult = sut.Equals(JsonUInt128.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonUInt128.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonInteger.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInteger.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonInteger.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInteger.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonInt64.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInt64.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonInt64.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInt64.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonInt32.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInt32.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonInt32.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInt32.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonInt16.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInt16.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonInt16.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInt16.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonSByte.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonSByte.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonSByte.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonSByte.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonUInt64.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonUInt64.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonUInt64.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonUInt64.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonUInt32.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonUInt32.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonUInt32.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonUInt32.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonUInt16.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonUInt16.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonUInt16.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonUInt16.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonByte.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonByte.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonByte.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonByte.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonInt128.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInt128.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonInt128.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonInt128.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__1_true_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonUInt128.Parse("1").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonUInt128.Parse("1").AsBinaryJsonNumber);
        Assert.True(equalsResult);
        Assert.True(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_JsonElement_backed_value_as_a_BinaryJsonNumber__1__3_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsOtherBackedResult = sut.Equals(JsonUInt128.Parse("3").AsBinaryJsonNumber);
        bool equalsResult = sut.Equals(JsonUInt128.Parse("3").AsBinaryJsonNumber);
        Assert.False(equalsResult);
        Assert.False(equalsOtherBackedResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Hello_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_true_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_0_true_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_1_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_true_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_false_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonInteger_2()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonInteger_2()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Hello_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_true_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_0_true_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_1_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_true_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_false_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonInt64_2()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonInt64_2()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Hello_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_true_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_0_true_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_1_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_true_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_false_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonInt32_2()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonInt32_2()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Hello_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_true_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_0_true_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_1_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_true_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_false_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonInt16_2()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonInt16_2()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Hello_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_true_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_0_true_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_1_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_true_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_false_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonSByte_2()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonSByte_2()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Hello_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_true_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_0_true_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_1_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_true_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_false_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonUInt64_2()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonUInt64_2()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Hello_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_true_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_0_true_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_1_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_true_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_false_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonUInt32_2()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonUInt32_2()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Hello_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_true_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_0_true_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_1_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_true_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_false_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonUInt16_2()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonUInt16_2()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Hello_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_true_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_0_true_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_1_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_true_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_false_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonByte_2()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonByte_2()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonByte()
    {
        var sut = JsonByte.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Hello_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_true_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_0_true_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_1_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_true_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_false_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonInt128_2()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonInt128_2()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Hello_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_true_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_0_true_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_1_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_true_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_false_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonUInt128_2()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1_first_1_false_JsonUInt128_2()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Hello_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_true_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_0_true_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_1_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_true_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_false_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonInteger_2()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonInteger_2()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Hello_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_true_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_0_true_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_1_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_true_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_false_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonInt64_2()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonInt64_2()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonInt64()
    {
        var sut = JsonInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Hello_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_true_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_0_true_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_1_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_true_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_false_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonInt32_2()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonInt32_2()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonInt32()
    {
        var sut = JsonInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Hello_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_true_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_0_true_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_1_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_true_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_false_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonInt16_2()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonInt16_2()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonInt16()
    {
        var sut = JsonInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Hello_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_true_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_0_true_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_1_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_true_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_false_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonSByte_2()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonSByte_2()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonSByte()
    {
        var sut = JsonSByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Hello_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_true_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_0_true_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_1_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_true_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_false_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonUInt64_2()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonUInt64_2()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Hello_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_true_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_0_true_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_1_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_true_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_false_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonUInt32_2()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonUInt32_2()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Hello_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_true_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_0_true_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_1_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_true_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_false_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonUInt16_2()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonUInt16_2()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Hello_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_true_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_0_true_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_1_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_true_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_false_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonByte_2()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonByte_2()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonByte()
    {
        var sut = JsonByte.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Hello_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_true_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_0_true_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_1_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_true_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_false_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonInt128_2()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonInt128_2()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonInt128()
    {
        var sut = JsonInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Hello_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_Goodbye_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_true_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_0_true_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.0"));
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_1_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__1_2_3_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_true_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_false_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13T20_20_39_00_00_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__2018_11_13_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_P3Y6M4DT12H30M5S_false_JsonUInt128_2()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_hello_endjin_com_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_www_example_com_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_http_foo_bar_baz_qux_quux_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1_first_1_false_JsonUInt128_2()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__192_168_0_1_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_IJsonValue__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1_Hello_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1_Goodbye_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"Goodbye\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1__1_true_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1__1_0_true_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("1.0");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1__1_1_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1__1_2_3_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1_first_1_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1_true_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1_false_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1__2018_11_13T20_20_39_00_00_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"2018-11-13T20:20:39+00:00\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1_P3Y6M4DT12H30M5S_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"P3Y6M4DT12H30M5S\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1__2018_11_13_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"2018-11-13\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1_hello_endjin_com_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"hello@endjin.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1_www_example_com_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"www.example.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1_http_foo_bar_baz_qux_quux_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1_first_1_false_JsonInteger_2()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1__192_168_0_1_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"192.168.0.1\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1_new_object_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = new object();
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_json_element_backed_value_as_an_object__1_null_false_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("1".AsSpan());
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1_Hello_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1_Goodbye_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"Goodbye\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1__1_true_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1__1_0_true_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1.0");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1__1_1_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1__1_2_3_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1_first_1_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1_true_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1_false_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1__2018_11_13T20_20_39_00_00_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"2018-11-13T20:20:39+00:00\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1__2018_11_13_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"2018-11-13\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1_P3Y6M4DT12H30M5S_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"P3Y6M4DT12H30M5S\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1_hello_endjin_com_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"hello@endjin.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1_www_example_com_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"www.example.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1_http_foo_bar_baz_qux_quux_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1_eyAiaGVsbG8iOiAid29ybGQiIH0_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1_first_1_false_JsonInteger_2()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1__192_168_0_1_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"192.168.0.1\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1__0_0_0_0_0_ffff_c0a8_0001_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1_new_object_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = new object();
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1_null_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1_null_false_JsonInteger_2()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object__1_undefined_false_JsonInteger()
    {
        var sut = JsonInteger.Parse("1").AsDotnetBackedValue();
        object? obj = default(JsonInteger);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object_null_null_true_JsonInteger()
    {
        var sut = JsonInteger.Null;
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object_null_null_true_JsonInteger_2()
    {
        var sut = JsonInteger.Null;
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.True(equalsResult);
    }

    [Fact]
    public void Equals_for_integer_dotnet_backed_value_as_an_object_null_undefined_false_JsonInteger()
    {
        var sut = JsonInteger.Null;
        object? obj = default(JsonInteger);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.False(equalsResult);
    }
}