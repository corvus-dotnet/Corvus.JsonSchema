// <copyright file="RefTupleTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET9_0_OR_GREATER

using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for <see cref="RefTuple{T1, T2}"/> and its higher arities — the span-capable companion to
/// <see cref="System.ValueTuple"/> used to thread context through generated <c>Build&lt;TContext&gt;</c> /
/// <c>Ok&lt;TContext&gt;</c> calls without a bespoke context struct. The type is .NET 9.0+ only (it relies on the
/// <c>allows ref struct</c> constraint), so the whole fixture is compiled only there.
/// </summary>
[TestClass]
public class RefTupleTests
{
    [TestMethod]
    public void TwoElements_ItemAccessAndDeconstruct()
    {
        var tuple = new RefTuple<int, string>(7, "x");

        Assert.AreEqual(7, tuple.Item1);
        Assert.AreEqual("x", tuple.Item2);

        var (first, second) = tuple;
        Assert.AreEqual(7, first);
        Assert.AreEqual("x", second);
    }

    [TestMethod]
    public void ThreeElements_ItemAccessAndDeconstruct()
    {
        var tuple = new RefTuple<int, int, int>(1, 2, 3);

        Assert.AreEqual(1, tuple.Item1);
        Assert.AreEqual(2, tuple.Item2);
        Assert.AreEqual(3, tuple.Item3);

        var (a, b, c) = tuple;
        Assert.AreEqual(6, a + b + c);
    }

    [TestMethod]
    public void FourElements_ItemAccessAndDeconstruct()
    {
        var tuple = new RefTuple<int, int, int, int>(1, 2, 3, 4);

        Assert.AreEqual(1, tuple.Item1);
        Assert.AreEqual(2, tuple.Item2);
        Assert.AreEqual(3, tuple.Item3);
        Assert.AreEqual(4, tuple.Item4);

        var (a, b, c, d) = tuple;
        Assert.AreEqual(10, a + b + c + d);
    }

    [TestMethod]
    public void CarriesSpanElements_WhichValueTupleCannot()
    {
        ReadOnlySpan<byte> value = "hello"u8;
        ReadOnlySpan<byte> label = "world"u8;

        var tuple = new RefTuple<ReadOnlySpan<byte>, ReadOnlySpan<byte>, int>(value, label, 3);

        Assert.IsTrue(tuple.Item1.SequenceEqual("hello"u8));
        Assert.IsTrue(tuple.Item2.SequenceEqual("world"u8));
        Assert.AreEqual(3, tuple.Item3);

        var (v, l, n) = tuple;
        Assert.IsTrue(v.SequenceEqual("hello"u8));
        Assert.IsTrue(l.SequenceEqual("world"u8));
        Assert.AreEqual(3, n);
    }
}

#endif