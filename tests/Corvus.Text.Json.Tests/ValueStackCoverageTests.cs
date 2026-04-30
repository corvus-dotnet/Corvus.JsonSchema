// <copyright file="ValueStackCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for <see cref="ValueStack{T}"/> targeting uncovered code paths
/// identified from Cobertura coverage reports.
/// </summary>
/// <remarks>
/// <para>Covered paths and their line references:</para>
/// <list type="bullet">
/// <item><c>Append(T)</c> resize path via <c>AddWithResize</c> (lines 62-64, 138-145)</item>
/// <item><c>Append(ReadOnlySpan&lt;T&gt;)</c> multi-element path (lines 68-81, 84-93)</item>
/// <item><c>Insert</c> at index 0 (lines 95-107)</item>
/// <item><c>AppendSpan</c> with growth path (lines 110-134)</item>
/// <item><c>AsSpan</c> (lines 147-150)</item>
/// <item><c>TryCopyTo</c> success and failure paths (lines 152-162)</item>
/// <item><c>Sort</c> (lines 244-254)</item>
/// <item><c>Grow</c> resize with old array return (line 223+)</item>
/// </list>
/// <para>
/// Not covered: <c>Grow</c> overflow path (lines 213-215) requires &gt;2 GB capacity,
/// impractical in tests. <c>#if SYSTEM_PRIVATE_CORELIB</c> branches (lines 224-232)
/// are unreachable on this compilation target.
/// </para>
/// </remarks>
public class ValueStackCoverageTests
{
    [Fact]
    public void AppendSingleItem_TriggersResize_WhenCapacityExceeded()
    {
        // ArrayPool minimum bucket is 16 for int, so we must exceed that.
        var stack = new ValueStack<int>(4);
        try
        {
            for (int i = 0; i < 16; i++)
            {
                stack.Append(i);
            }

            // This append exceeds the rented capacity, triggering AddWithResize (lines 62-64, 138-145).
            stack.Append(99);

            Assert.Equal(17, stack.Length);
            Assert.Equal(99, stack[16]);
        }
        finally
        {
            stack.Dispose();
        }
    }

    [Fact]
    public void AppendSpan_SingleElement_UsesInlinePath()
    {
        var stack = new ValueStack<int>(8);
        try
        {
            ReadOnlySpan<int> single = stackalloc int[] { 42 };
            stack.Append(single);

            Assert.Equal(1, stack.Length);
            Assert.Equal(42, stack[0]);
        }
        finally
        {
            stack.Dispose();
        }
    }

    [Fact]
    public void AppendSpan_MultiElement_UsesAppendMultiChar()
    {
        var stack = new ValueStack<int>(8);
        try
        {
            // Multi-element span triggers AppendMultiChar (lines 78-79, 84-93).
            ReadOnlySpan<int> items = stackalloc int[] { 10, 20, 30 };
            stack.Append(items);

            Assert.Equal(3, stack.Length);
            Assert.Equal(10, stack[0]);
            Assert.Equal(20, stack[1]);
            Assert.Equal(30, stack[2]);
        }
        finally
        {
            stack.Dispose();
        }
    }

    [Fact]
    public void AppendSpan_MultiElement_TriggersGrow()
    {
        var stack = new ValueStack<int>(4);
        try
        {
            // Fill the rented buffer (ArrayPool minimum = 16 for int).
            for (int i = 0; i < 15; i++)
            {
                stack.Append(i);
            }

            // Appending 3 elements exceeds remaining capacity (1 slot left),
            // triggering the grow path in AppendMultiChar (lines 87-88).
            ReadOnlySpan<int> overflow = stackalloc int[] { 100, 200, 300 };
            stack.Append(overflow);

            Assert.Equal(18, stack.Length);
            Assert.Equal(300, stack[17]);
        }
        finally
        {
            stack.Dispose();
        }
    }

    [Fact]
    public void Insert_AtIndexZero_ShiftsExistingElements()
    {
        var stack = new ValueStack<int>(8);
        try
        {
            stack.Append(3);
            stack.Append(4);

            // Insert at index 0 (lines 95-107).
            ReadOnlySpan<int> prefix = stackalloc int[] { 1, 2 };
            stack.Insert(0, prefix);

            Assert.Equal(4, stack.Length);
            Assert.Equal(1, stack[0]);
            Assert.Equal(2, stack[1]);
            Assert.Equal(3, stack[2]);
            Assert.Equal(4, stack[3]);
        }
        finally
        {
            stack.Dispose();
        }
    }

    [Fact]
    public void Insert_AtIndexZero_TriggersGrow()
    {
        var stack = new ValueStack<int>(4);
        try
        {
            // Fill the rented buffer (ArrayPool minimum = 16 for int).
            for (int i = 0; i < 16; i++)
            {
                stack.Append(i + 10);
            }

            // Insert with full capacity triggers grow (lines 99-101).
            ReadOnlySpan<int> prefix = stackalloc int[] { 1, 2 };
            stack.Insert(0, prefix);

            Assert.Equal(18, stack.Length);
            Assert.Equal(1, stack[0]);
            Assert.Equal(2, stack[1]);
            Assert.Equal(10, stack[2]);
        }
        finally
        {
            stack.Dispose();
        }
    }

    [Fact]
    public void AppendSpan_ReturnsWritableSpan()
    {
        var stack = new ValueStack<int>(8);
        try
        {
            // AppendSpan returns a writable span (lines 110-119).
            Span<int> span = stack.AppendSpan(3);
            span[0] = 100;
            span[1] = 200;
            span[2] = 300;

            Assert.Equal(3, stack.Length);
            Assert.Equal(100, stack[0]);
            Assert.Equal(200, stack[1]);
            Assert.Equal(300, stack[2]);
        }
        finally
        {
            stack.Dispose();
        }
    }

    [Fact]
    public void AppendSpan_TriggersGrow_WhenInsufficient()
    {
        var stack = new ValueStack<int>(4);
        try
        {
            // Fill the rented buffer (ArrayPool minimum = 16 for int).
            for (int i = 0; i < 15; i++)
            {
                stack.Append(i);
            }

            // Request 3 more elements with only 1 slot remaining.
            // Triggers AppendSpanWithGrow (lines 122-123, 128-134).
            Span<int> span = stack.AppendSpan(3);
            span[0] = 100;
            span[1] = 200;
            span[2] = 300;

            Assert.Equal(18, stack.Length);
            Assert.Equal(100, stack[15]);
        }
        finally
        {
            stack.Dispose();
        }
    }

    [Fact]
    public void AsSpan_ReturnsCurrentContent()
    {
        var stack = new ValueStack<int>(8);
        try
        {
            stack.Append(10);
            stack.Append(20);
            stack.Append(30);

            // AsSpan (lines 147-150).
            ReadOnlySpan<int> span = stack.AsSpan();
            Assert.Equal(3, span.Length);
            Assert.Equal(10, span[0]);
            Assert.Equal(20, span[1]);
            Assert.Equal(30, span[2]);
        }
        finally
        {
            stack.Dispose();
        }
    }

    [Fact]
    public void TryCopyTo_Success_WhenDestinationLargeEnough()
    {
        var stack = new ValueStack<int>(8);
        try
        {
            stack.Append(1);
            stack.Append(2);
            stack.Append(3);

            // Success path (lines 153-157).
            Span<int> dest = stackalloc int[4];
            bool result = stack.TryCopyTo(dest, out int written);

            Assert.True(result);
            Assert.Equal(3, written);
            Assert.Equal(1, dest[0]);
            Assert.Equal(2, dest[1]);
            Assert.Equal(3, dest[2]);
        }
        finally
        {
            stack.Dispose();
        }
    }

    [Fact]
    public void TryCopyTo_Failure_WhenDestinationTooSmall()
    {
        var stack = new ValueStack<int>(8);
        try
        {
            stack.Append(1);
            stack.Append(2);
            stack.Append(3);

            // Failure path (lines 160-161).
            Span<int> dest = stackalloc int[2];
            bool result = stack.TryCopyTo(dest, out int written);

            Assert.False(result);
            Assert.Equal(0, written);
        }
        finally
        {
            stack.Dispose();
        }
    }

    [Fact]
    public void Sort_OrdersElements()
    {
        var stack = new ValueStack<int>(8);
        try
        {
            stack.Append(3);
            stack.Append(1);
            stack.Append(4);
            stack.Append(1);
            stack.Append(5);

            // Sort (lines 244-254, platform-conditional).
            stack.Sort();

            ReadOnlySpan<int> sorted = stack.AsSpan();
            Assert.Equal(1, sorted[0]);
            Assert.Equal(1, sorted[1]);
            Assert.Equal(3, sorted[2]);
            Assert.Equal(4, sorted[3]);
            Assert.Equal(5, sorted[4]);
        }
        finally
        {
            stack.Dispose();
        }
    }
}
