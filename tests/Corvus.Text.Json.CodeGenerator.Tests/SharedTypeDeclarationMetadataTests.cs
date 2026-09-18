// <copyright file="SharedTypeDeclarationMetadataTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// The process-wide well-known type declarations are reached by concurrent generations. Their metadata is
/// copy-on-write: writers publish a new dictionary, readers take no lock, and a reader always sees a complete
/// dictionary (never a torn or partially built one).
/// </summary>
[TestClass]
public class SharedTypeDeclarationMetadataTests
{
    [TestMethod]
    public void SharedDeclaration_ConcurrentReadersAndWriters_AlwaysSeeCompleteMetadata()
    {
        TypeDeclaration shared = WellKnownTypeDeclarations.JsonAny;
        string prefix = "m4-shared-guard-" + Guid.NewGuid().ToString("N") + "-";
        string constantKey = prefix + "constant";
        shared.SetMetadata(constantKey, 42);
        try
        {
            const int Writers = 4;
            const int Iterations = 2000;
            List<Task> tasks = [];
            for (int w = 0; w < Writers; w++)
            {
                string key = prefix + w;
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < Iterations; i++)
                    {
                        shared.SetMetadata(key, i);
                        Assert.IsTrue(shared.TryGetMetadata(key, out int value), "a writer reads its own last write");
                        Assert.AreEqual(i, value);
                        Assert.IsTrue(shared.TryGetMetadata(constantKey, out int constant), "the constant survives every publish");
                        Assert.AreEqual(42, constant);
                    }
                }));
            }

            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < Iterations * Writers; i++)
                {
                    Assert.IsTrue(shared.TryGetMetadata(constantKey, out int constant));
                    Assert.AreEqual(42, constant);
                    Assert.IsTrue(shared.TryGetMetadata("dotnetTypeName", out string name) || name is null, "unrelated keys keep working");
                }
            }));

            Task.WaitAll([.. tasks]);

            for (int w = 0; w < Writers; w++)
            {
                Assert.IsTrue(shared.TryGetMetadata(prefix + w, out int value));
                Assert.AreEqual(Iterations - 1, value);
                shared.RemoveMetadata(prefix + w);
                Assert.IsFalse(shared.TryGetMetadata(prefix + w, out int _));
            }
        }
        finally
        {
            shared.RemoveMetadata(constantKey);
        }

        Assert.IsFalse(shared.TryGetMetadata(constantKey, out int _));
    }
}