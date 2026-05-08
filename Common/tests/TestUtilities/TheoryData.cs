// <copyright file="TheoryData.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Shim for xUnit's TheoryData to support MSTest DynamicData.
/// </summary>
public class TheoryData<T1> : IEnumerable<object[]>
{
    private readonly List<object[]> _data = new();
    public void Add(T1 v1) => _data.Add([v1!]);
    public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Shim for xUnit's TheoryData to support MSTest DynamicData.
/// </summary>
public class TheoryData<T1, T2> : IEnumerable<object[]>
{
    private readonly List<object[]> _data = new();
    public void Add(T1 v1, T2 v2) => _data.Add([v1!, v2!]);
    public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Shim for xUnit's TheoryData to support MSTest DynamicData.
/// </summary>
public class TheoryData<T1, T2, T3> : IEnumerable<object[]>
{
    private readonly List<object[]> _data = new();
    public void Add(T1 v1, T2 v2, T3 v3) => _data.Add([v1!, v2!, v3!]);
    public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Shim for xUnit's TheoryData to support MSTest DynamicData.
/// </summary>
public class TheoryData<T1, T2, T3, T4> : IEnumerable<object[]>
{
    private readonly List<object[]> _data = new();
    public void Add(T1 v1, T2 v2, T3 v3, T4 v4) => _data.Add([v1!, v2!, v3!, v4!]);
    public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Shim for xUnit's TheoryData to support MSTest DynamicData.
/// </summary>
public class TheoryData<T1, T2, T3, T4, T5> : IEnumerable<object[]>
{
    private readonly List<object[]> _data = new();
    public void Add(T1 v1, T2 v2, T3 v3, T4 v4, T5 v5) => _data.Add([v1!, v2!, v3!, v4!, v5!]);
    public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Shim for xUnit's TheoryData to support MSTest DynamicData.
/// </summary>
public class TheoryData<T1, T2, T3, T4, T5, T6> : IEnumerable<object[]>
{
    private readonly List<object[]> _data = new();
    public void Add(T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6) => _data.Add([v1!, v2!, v3!, v4!, v5!, v6!]);
    public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Shim for xUnit's TheoryData to support MSTest DynamicData.
/// </summary>
public class TheoryData<T1, T2, T3, T4, T5, T6, T7> : IEnumerable<object[]>
{
    private readonly List<object[]> _data = new();
    public void Add(T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7) => _data.Add([v1!, v2!, v3!, v4!, v5!, v6!, v7!]);
    public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
