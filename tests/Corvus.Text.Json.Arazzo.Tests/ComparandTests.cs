// <copyright file="ComparandTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
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
        Utf8("AB").ValueEquals(Utf8("ab")).ShouldBeTrue();   // case-insensitive
        Comparand.FromNumber(1).ValueEquals(Utf8("1")).ShouldBeFalse();  // differing kinds
    }

    [TestMethod]
    public void String_equality_works_across_literal_and_json_backing()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"OK\""u8.ToArray());
        Comparand jsonBacked = Comparand.FromJsonString(doc.RootElement);

        jsonBacked.ValueEquals(Utf8("ok")).ShouldBeTrue();          // json string vs literal, case-insensitive
        Utf8("ok").ValueEquals(jsonBacked).ShouldBeTrue();          // literal vs json string
        jsonBacked.ValueEquals(Utf8("nope")).ShouldBeFalse();
    }

    [TestMethod]
    public void TryAsNumber_coerces_numeric_strings()
    {
        Comparand.FromNumber(3).TryAsNumber(out double a).ShouldBeTrue();
        a.ShouldBe(3);
        Utf8("4.5").TryAsNumber(out double b).ShouldBeTrue();
        b.ShouldBe(4.5);
        Utf8("abc").TryAsNumber(out _).ShouldBeFalse();
        Comparand.Null.TryAsNumber(out _).ShouldBeFalse();
    }

    private static Comparand Utf8(string value) => Comparand.FromUtf8String(System.Text.Encoding.UTF8.GetBytes(value));

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