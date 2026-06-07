// <copyright file="ComparandTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Direct unit tests for the internal <see cref="Comparand"/> value type (via InternalsVisibleTo),
/// covering branches not reachable through the simple-condition evaluator (which short-circuits
/// undefined operands before comparing).
/// </summary>
[TestClass]
public class ComparandTests
{
    [TestMethod]
    public void Undefined_operand_never_equal()
    {
        Comparand.Undefined.ValueEquals(Comparand.FromNumber(1)).ShouldBeFalse();
        Comparand.FromNumber(1).ValueEquals(Comparand.Undefined).ShouldBeFalse();
        Comparand.Undefined.ValueEquals(Comparand.Undefined).ShouldBeFalse();
    }

    [TestMethod]
    public void Equality_by_kind()
    {
        Comparand.Null.ValueEquals(Comparand.Null).ShouldBeTrue();
        Comparand.FromBoolean(true).ValueEquals(Comparand.FromBoolean(true)).ShouldBeTrue();
        Comparand.FromBoolean(true).ValueEquals(Comparand.FromBoolean(false)).ShouldBeFalse();
        Comparand.FromNumber(2).ValueEquals(Comparand.FromNumber(2)).ShouldBeTrue();
        Comparand.FromString("AB").ValueEquals(Comparand.FromString("ab")).ShouldBeTrue();   // case-insensitive
        Comparand.FromJson("""{"a":1}""").ValueEquals(Comparand.FromJson("""{"a":1}""")).ShouldBeTrue();
        Comparand.FromJson("""{"a":1}""").ValueEquals(Comparand.FromJson("""{"a":2}""")).ShouldBeFalse();
        Comparand.FromNumber(1).ValueEquals(Comparand.FromString("1")).ShouldBeFalse();        // differing kinds
    }

    [TestMethod]
    public void TryAsNumber_coerces_numeric_strings()
    {
        Comparand.FromNumber(3).TryAsNumber(out double a).ShouldBeTrue();
        a.ShouldBe(3);
        Comparand.FromString("4.5").TryAsNumber(out double b).ShouldBeTrue();
        b.ShouldBe(4.5);
        Comparand.FromString("abc").TryAsNumber(out _).ShouldBeFalse();
        Comparand.Null.TryAsNumber(out _).ShouldBeFalse();
    }

    [TestMethod]
    public void TryCompareNumeric_requires_two_numbers()
    {
        Comparand.FromNumber(1).TryCompareNumeric(Comparand.FromNumber(2), out int c).ShouldBeTrue();
        c.ShouldBeLessThan(0);
        Comparand.FromNumber(1).TryCompareNumeric(Comparand.Null, out _).ShouldBeFalse();
    }

    [TestMethod]
    public void ParseLiteral_recognizes_each_literal_form()
    {
        Comparand.ParseLiteral("true").Kind.ShouldBe(ComparandKind.Boolean);
        Comparand.ParseLiteral("false").Kind.ShouldBe(ComparandKind.Boolean);
        Comparand.ParseLiteral("null").Kind.ShouldBe(ComparandKind.Null);
        Comparand.ParseLiteral("42").Kind.ShouldBe(ComparandKind.Number);
        Comparand.ParseLiteral("'hi'").Kind.ShouldBe(ComparandKind.String);
        Comparand.ParseLiteral("\"hi\"").Kind.ShouldBe(ComparandKind.String);
        Comparand.ParseLiteral("bareword").Kind.ShouldBe(ComparandKind.Undefined);
    }
}