// <copyright file="UniqueItemsHashSetSizeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Internal;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests.UniqueItemsHashSetSize;

/// <summary>
/// <see cref="UniqueItemsHashSet"/> works with the stack buffers its callers pass, whatever the item count. Its table
/// has a prime number of buckets at least the item count, and for 240 to 256 items that prime (293) is larger than
/// the <see cref="UniqueItemsHashSet.StackAllocBucketSize"/> buckets of the stack buffer, which the set sliced past
/// its end (issue #1068).
/// </summary>
[TestClass]
public class UniqueItemsHashSetStackBuffers
{
    [TestMethod]
    public void EveryItemCountWorksWithTheStackBuffers()
    {
        for (int count = 1; count <= 600; count++)
        {
            string json = DistinctStrings(count);
            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
            AssertAddsEachItemOnce(doc.RootElement, count);
        }
    }

    private static void AssertAddsEachItemOnce(JsonElement array, int count)
    {
        IJsonDocument parentDocument = ((IJsonElement)array).ParentDocument;
        using UniqueItemsHashSet hashSet = new(
            parentDocument,
            count,
            stackalloc int[UniqueItemsHashSet.StackAllocBucketSize],
            stackalloc byte[UniqueItemsHashSet.StackAllocEntrySize]);

        int firstIndex = -1;
        foreach (JsonElement item in array.EnumerateArray())
        {
            int index = ((IJsonElement)item).ParentDocumentIndex;
            if (firstIndex < 0)
            {
                firstIndex = index;
            }

            Assert.IsTrue(hashSet.AddItemIfNotExists(index), $"An item of {count} was seen as a duplicate.");
        }

        Assert.IsFalse(hashSet.AddItemIfNotExists(firstIndex), $"The first of {count} items was not seen again.");
    }

    internal static string DistinctStrings(int count)
    {
        return "[" + string.Join(",", Enumerable.Range(0, count).Select(i => $"\"item{i}\"")) + "]";
    }
}

/// <summary>
/// A generated type validates <c>uniqueItems</c> for arrays of 240 to 256 items, where V5.5 threw
/// <see cref="System.ArgumentOutOfRangeException"/> from <see cref="UniqueItemsHashSet"/> (issue #1068). Generated
/// types have used the runtime evaluator's own set since V5.6.0, so this guards the reported case.
/// </summary>
[TestCategory("CodeGen")]
[TestClass]
public class UniqueItemsHashSetSizeValues
{
    private static Fixture? s_fixture;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        s_fixture = new Fixture();
        await s_fixture.InitializeAsync();
    }

    [ClassCleanup]
    public static void ClassCleanupMethod()
    {
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    [DataRow(239)]
    [DataRow(240)]
    [DataRow(250)]
    [DataRow(256)]
    [DataRow(257)]
    public void DistinctItemsAreValid(int count)
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance(UniqueItemsHashSetStackBuffers.DistinctStrings(count));
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow(240)]
    [DataRow(256)]
    public void ARepeatedItemIsInvalid(int count)
    {
        string json = UniqueItemsHashSetStackBuffers.DistinctStrings(count - 1)[..^1] + ",\"item0\"]";
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\unique-items-hash-set-size.json",
                """
                {
                    "$schema": "https://json-schema.org/draft/2020-12/schema",
                    "type": "array",
                    "uniqueItems": true,
                    "items": { "type": "string" }
                }
                """,
                "Corvus.Text.Json.Tests.UniqueItemsHashSetSize",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                optionalAsNullable: true,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}