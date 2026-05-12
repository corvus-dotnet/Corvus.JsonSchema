// <copyright file="AssertEx.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Assertion helpers that bridge xUnit assertion patterns missing from MSTest.
/// </summary>
public static class AssertEx
{
    /// <summary>
    /// Verifies that all items in a collection satisfy the given predicate.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="collection">The collection to check.</param>
    /// <param name="action">The assertion action to apply to each item.</param>
    public static void All<T>(IEnumerable<T> collection, Action<T> action)
    {
        int index = 0;
        List<Exception>? failures = null;
        foreach (T item in collection)
        {
            try
            {
                action(item);
            }
            catch (Exception ex)
            {
                failures ??= new List<Exception>();
                failures.Add(new Exception($"Item at index {index} failed: {ex.Message}", ex));
            }

            index++;
        }

        if (failures is not null)
        {
            throw new AssertFailedException(
                $"Assert.All failed. {failures.Count} out of {index} items failed:\n" +
                string.Join("\n", failures.Select(f => f.Message)));
        }
    }

    /// <summary>
    /// Verifies that a collection contains at least one element matching the predicate.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="collection">The collection to search.</param>
    /// <param name="predicate">The match predicate.</param>
    public static void Contains<T>(IEnumerable<T> collection, Func<T, bool> predicate)
    {
        if (!collection.Any(predicate))
        {
            throw new AssertFailedException("Assert.Contains failed. No matching element found in the collection.");
        }
    }

    /// <summary>
    /// Verifies that an object is assignable to the given type.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="obj">The object to check.</param>
    /// <returns>The object cast to <typeparamref name="T"/>.</returns>
    public static T IsAssignableFrom<T>(object? obj)
    {
        Assert.IsNotNull(obj);
        Assert.IsInstanceOfType<T>(obj);
        return (T)obj;
    }

    /// <summary>
    /// Verifies that an object is exactly the given type (not a derived type).
    /// </summary>
    /// <typeparam name="T">The expected exact type.</typeparam>
    /// <param name="obj">The object to check.</param>
    /// <returns>The object cast to <typeparamref name="T"/>.</returns>
    public static T IsType<T>(object? obj)
    {
        Assert.IsNotNull(obj);
        Assert.AreEqual(typeof(T), obj!.GetType(), $"Expected exact type {typeof(T).FullName}, but got {obj.GetType().FullName}.");
        return (T)obj;
    }

    /// <summary>
    /// Verifies that the specified action throws the exact exception type,
    /// and that the exception's ParamName matches the expected value.
    /// </summary>
    /// <typeparam name="T">The expected exception type (must be ArgumentException or derived).</typeparam>
    /// <param name="expectedParamName">The expected parameter name.</param>
    /// <param name="action">The action to invoke.</param>
    /// <returns>The thrown exception.</returns>
    public static T ThrowsExactly<T>(string expectedParamName, Action action)
        where T : ArgumentException
    {
        T ex = Assert.ThrowsExactly<T>(action);
        Assert.AreEqual(expectedParamName, ex.ParamName, $"Expected ParamName '{expectedParamName}', but got '{ex.ParamName}'.");
        return ex;
    }

    /// <summary>
    /// Async version of ThrowsExactly with paramName.
    /// </summary>
    /// <typeparam name="T">The expected exception type (must be ArgumentException or derived).</typeparam>
    /// <param name="expectedParamName">The expected parameter name.</param>
    /// <param name="action">The async action to invoke.</param>
    /// <returns>The thrown exception.</returns>
    public static async System.Threading.Tasks.Task<T> ThrowsExactlyAsync<T>(string expectedParamName, Func<System.Threading.Tasks.Task> action)
        where T : ArgumentException
    {
        T ex = await Assert.ThrowsExactlyAsync<T>(action);
        Assert.AreEqual(expectedParamName, ex.ParamName, $"Expected ParamName '{expectedParamName}', but got '{ex.ParamName}'.");
        return ex;
    }
}
