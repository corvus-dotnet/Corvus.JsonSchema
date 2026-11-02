// <copyright file="GeneratedArrayCollectionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// A generated pure array type is a read-only collection of its items
/// (https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/1049).
/// </summary>
[TestClass]
public class GeneratedArrayCollectionTests
{
    private const string Json = """[{"id":1,"label":"a"},{"id":2},{"id":3,"label":"c"}]""";

    [TestMethod]
    public void APureArrayType_IsAReadOnlyCollectionOfItsItems()
    {
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(Json);
        IReadOnlyCollection<ArrayOfItems.RequiredId> items = doc.RootElement;

        Assert.AreEqual(3, items.Count);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, items.Select(i => (int)i.Id).ToArray());
        Assert.AreEqual("c", (string)items.Last().Label);
    }

    [TestMethod]
    public void Foreach_GivesTheItemsInOrder()
    {
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(Json);
        var ids = new List<int>();
        foreach (ArrayOfItems.RequiredId item in doc.RootElement)
        {
            ids.Add((int)item.Id);
        }

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, ids);
    }

    [TestMethod]
    public void Foreach_AllocatesNothingMoreThanEnumerateArray()
    {
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(Json);
        ArrayOfItems array = doc.RootElement;

        int OverTheValue()
        {
            int count = 0;
            foreach (ArrayOfItems.RequiredId item in array)
            {
                count++;
            }

            return count;
        }

        int OverEnumerateArray()
        {
            int count = 0;
            foreach (ArrayOfItems.RequiredId item in array.EnumerateArray())
            {
                count++;
            }

            return count;
        }

        long overTheValue = LeastAllocated(OverTheValue);

#if NET
        Assert.AreEqual(0, overTheValue);
#else
        // On .NET Framework, making each element allocates (EnumerateArray() does the same); foreach over the value
        // adds nothing to it.
        Assert.AreEqual(LeastAllocated(OverEnumerateArray), overTheValue);
#endif
        Assert.AreEqual(3, OverEnumerateArray());
    }

    // The least a hundred calls allocate in any of three windows, after a warm-up.
    private static long LeastAllocated(Func<int> run)
    {
        for (int i = 0; i < 100; i++)
        {
            Assert.AreEqual(3, run());
        }

        long least = long.MaxValue;
        for (int window = 0; window < 3; window++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                run();
            }

            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return least;
    }

    [TestMethod]
    public void ATupleType_IsNotACollection()
    {
        Assert.IsFalse(typeof(PureTuple).GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)));
        Assert.IsTrue(typeof(ArrayOfItems).GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)));
    }
}