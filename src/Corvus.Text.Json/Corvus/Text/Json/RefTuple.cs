// <copyright file="RefTuple.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json;

#if NET9_0_OR_GREATER

/// <summary>
/// A two-element tuple that is itself a <see langword="ref"/> <see langword="struct"/> and whose elements may be ref
/// structs (<c>allows ref struct</c>) — the span-capable companion to <see cref="System.ValueTuple{T1, T2}"/>, which
/// cannot carry a <see cref="System.ReadOnlySpan{T}"/>.
/// </summary>
/// <typeparam name="T1">The type of the first element.</typeparam>
/// <typeparam name="T2">The type of the second element.</typeparam>
/// <remarks>
/// <para>
/// Available on .NET 9.0 and later only: it relies on the <c>allows ref struct</c> generic constraint (C# 13), which is
/// what lets an element be a <see cref="System.ReadOnlySpan{T}"/> or other ref struct. On earlier targets use a bespoke
/// context <see langword="struct"/> instead.
/// </para>
/// <para>
/// Use it to thread several values (including spans) through a generated <c>Build&lt;TContext&gt;</c> /
/// <c>CreateBuilder&lt;TContext&gt;</c> / <c>Ok&lt;TContext&gt;</c> as the <c>TContext</c>, instead of declaring a bespoke
/// context <see langword="struct"/> per call site. <see cref="Deconstruct"/> recovers named locals at the use site:
/// <c>var (page, access) = state;</c>.
/// </para>
/// <para>
/// This is a minimal carrier: it deliberately has no equality, hashing, or <c>ToString</c> (those are meaningless when an
/// element is a span), and no <c>(a, b)</c> literal syntax (reserved by the compiler for <see cref="System.ValueTuple"/>).
/// </para>
/// </remarks>
public readonly ref struct RefTuple<T1, T2>
    where T1 : allows ref struct
    where T2 : allows ref struct
{
    /// <summary>Initializes a new instance of the <see cref="RefTuple{T1, T2}"/> struct.</summary>
    /// <param name="item1">The first element.</param>
    /// <param name="item2">The second element.</param>
    public RefTuple(T1 item1, T2 item2)
    {
        this.Item1 = item1;
        this.Item2 = item2;
    }

    /// <summary>Gets the first element.</summary>
    public T1 Item1 { get; }

    /// <summary>Gets the second element.</summary>
    public T2 Item2 { get; }

    /// <summary>Deconstructs the tuple into named locals.</summary>
    /// <param name="item1">Receives the first element.</param>
    /// <param name="item2">Receives the second element.</param>
    public void Deconstruct(out T1 item1, out T2 item2)
    {
        item1 = this.Item1;
        item2 = this.Item2;
    }
}

/// <summary>
/// A three-element span-capable tuple (.NET 9.0+). See <see cref="RefTuple{T1, T2}"/> for the rationale.
/// </summary>
/// <typeparam name="T1">The type of the first element.</typeparam>
/// <typeparam name="T2">The type of the second element.</typeparam>
/// <typeparam name="T3">The type of the third element.</typeparam>
public readonly ref struct RefTuple<T1, T2, T3>
    where T1 : allows ref struct
    where T2 : allows ref struct
    where T3 : allows ref struct
{
    /// <summary>Initializes a new instance of the <see cref="RefTuple{T1, T2, T3}"/> struct.</summary>
    /// <param name="item1">The first element.</param>
    /// <param name="item2">The second element.</param>
    /// <param name="item3">The third element.</param>
    public RefTuple(T1 item1, T2 item2, T3 item3)
    {
        this.Item1 = item1;
        this.Item2 = item2;
        this.Item3 = item3;
    }

    /// <summary>Gets the first element.</summary>
    public T1 Item1 { get; }

    /// <summary>Gets the second element.</summary>
    public T2 Item2 { get; }

    /// <summary>Gets the third element.</summary>
    public T3 Item3 { get; }

    /// <summary>Deconstructs the tuple into named locals.</summary>
    /// <param name="item1">Receives the first element.</param>
    /// <param name="item2">Receives the second element.</param>
    /// <param name="item3">Receives the third element.</param>
    public void Deconstruct(out T1 item1, out T2 item2, out T3 item3)
    {
        item1 = this.Item1;
        item2 = this.Item2;
        item3 = this.Item3;
    }
}

/// <summary>
/// A four-element span-capable tuple (.NET 9.0+). See <see cref="RefTuple{T1, T2}"/> for the rationale.
/// </summary>
/// <typeparam name="T1">The type of the first element.</typeparam>
/// <typeparam name="T2">The type of the second element.</typeparam>
/// <typeparam name="T3">The type of the third element.</typeparam>
/// <typeparam name="T4">The type of the fourth element.</typeparam>
public readonly ref struct RefTuple<T1, T2, T3, T4>
    where T1 : allows ref struct
    where T2 : allows ref struct
    where T3 : allows ref struct
    where T4 : allows ref struct
{
    /// <summary>Initializes a new instance of the <see cref="RefTuple{T1, T2, T3, T4}"/> struct.</summary>
    /// <param name="item1">The first element.</param>
    /// <param name="item2">The second element.</param>
    /// <param name="item3">The third element.</param>
    /// <param name="item4">The fourth element.</param>
    public RefTuple(T1 item1, T2 item2, T3 item3, T4 item4)
    {
        this.Item1 = item1;
        this.Item2 = item2;
        this.Item3 = item3;
        this.Item4 = item4;
    }

    /// <summary>Gets the first element.</summary>
    public T1 Item1 { get; }

    /// <summary>Gets the second element.</summary>
    public T2 Item2 { get; }

    /// <summary>Gets the third element.</summary>
    public T3 Item3 { get; }

    /// <summary>Gets the fourth element.</summary>
    public T4 Item4 { get; }

    /// <summary>Deconstructs the tuple into named locals.</summary>
    /// <param name="item1">Receives the first element.</param>
    /// <param name="item2">Receives the second element.</param>
    /// <param name="item3">Receives the third element.</param>
    /// <param name="item4">Receives the fourth element.</param>
    public void Deconstruct(out T1 item1, out T2 item2, out T3 item3, out T4 item4)
    {
        item1 = this.Item1;
        item2 = this.Item2;
        item3 = this.Item3;
        item4 = this.Item4;
    }
}

#endif