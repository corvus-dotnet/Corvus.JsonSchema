// <copyright file="PatchCopyLargeFileBenchmark.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Corvus.Text.Json.Patch;
using V4PatchDocument = Corvus.Json.Patch.Model.JsonPatchDocument;
using V5PatchDocument = Corvus.Text.Json.Patch.JsonPatchDocument;

namespace Corvus.Text.Json.Benchmarks.PatchBenchmarks;

/// <summary>
/// Benchmark for a large JSON file with 36 copy operations across array indices.
/// </summary>
[MemoryDiagnoser]
public class PatchCopyLargeFileBenchmark : PatchBenchmarkBase
{
    private V4PatchDocument v4Patch;
    private V5PatchDocument v5Patch;
    private global::Json.Patch.JsonPatch? jePatch;

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        this.jePatch =
            new global::Json.Patch.JsonPatch(
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/11/actor/id"), global::Json.Pointer.JsonPointer.Parse("/14/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/12/actor/id"), global::Json.Pointer.JsonPointer.Parse("/15/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/13/actor/id"), global::Json.Pointer.JsonPointer.Parse("/16/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/14/actor/id"), global::Json.Pointer.JsonPointer.Parse("/17/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/15/actor/id"), global::Json.Pointer.JsonPointer.Parse("/18/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/16/actor/id"), global::Json.Pointer.JsonPointer.Parse("/19/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/17/actor/id"), global::Json.Pointer.JsonPointer.Parse("/20/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/18/actor/id"), global::Json.Pointer.JsonPointer.Parse("/21/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/19/actor/id"), global::Json.Pointer.JsonPointer.Parse("/22/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/20/actor/id"), global::Json.Pointer.JsonPointer.Parse("/23/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/21/actor/id"), global::Json.Pointer.JsonPointer.Parse("/24/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/22/actor/id"), global::Json.Pointer.JsonPointer.Parse("/25/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/23/actor/id"), global::Json.Pointer.JsonPointer.Parse("/26/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/24/actor/id"), global::Json.Pointer.JsonPointer.Parse("/27/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/25/actor/id"), global::Json.Pointer.JsonPointer.Parse("/28/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/26/actor/id"), global::Json.Pointer.JsonPointer.Parse("/29/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/27/actor/id"), global::Json.Pointer.JsonPointer.Parse("/30/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/28/actor/id"), global::Json.Pointer.JsonPointer.Parse("/31/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/29/actor/id"), global::Json.Pointer.JsonPointer.Parse("/32/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/30/actor/id"), global::Json.Pointer.JsonPointer.Parse("/33/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/31/actor/id"), global::Json.Pointer.JsonPointer.Parse("/34/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/32/actor/id"), global::Json.Pointer.JsonPointer.Parse("/35/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/33/actor/id"), global::Json.Pointer.JsonPointer.Parse("/36/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/34/actor/id"), global::Json.Pointer.JsonPointer.Parse("/37/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/35/actor/id"), global::Json.Pointer.JsonPointer.Parse("/38/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/36/actor/id"), global::Json.Pointer.JsonPointer.Parse("/39/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/37/actor/id"), global::Json.Pointer.JsonPointer.Parse("/40/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/38/actor/id"), global::Json.Pointer.JsonPointer.Parse("/41/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/39/actor/id"), global::Json.Pointer.JsonPointer.Parse("/42/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/40/actor/id"), global::Json.Pointer.JsonPointer.Parse("/43/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/41/actor/id"), global::Json.Pointer.JsonPointer.Parse("/44/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/42/actor/id"), global::Json.Pointer.JsonPointer.Parse("/45/actor/id")),
                global::Json.Patch.PatchOperation.Copy(global::Json.Pointer.JsonPointer.Parse("/43/actor/id"), global::Json.Pointer.JsonPointer.Parse("/46/actor/id")));

        string patchJson = """
            [
                {"op":"copy","from":"/11/actor/id","path":"/14/actor/id"},
                {"op":"copy","from":"/12/actor/id","path":"/15/actor/id"},
                {"op":"copy","from":"/13/actor/id","path":"/16/actor/id"},
                {"op":"copy","from":"/14/actor/id","path":"/17/actor/id"},
                {"op":"copy","from":"/15/actor/id","path":"/18/actor/id"},
                {"op":"copy","from":"/16/actor/id","path":"/19/actor/id"},
                {"op":"copy","from":"/17/actor/id","path":"/20/actor/id"},
                {"op":"copy","from":"/18/actor/id","path":"/21/actor/id"},
                {"op":"copy","from":"/19/actor/id","path":"/22/actor/id"},
                {"op":"copy","from":"/20/actor/id","path":"/23/actor/id"},
                {"op":"copy","from":"/21/actor/id","path":"/24/actor/id"},
                {"op":"copy","from":"/22/actor/id","path":"/25/actor/id"},
                {"op":"copy","from":"/23/actor/id","path":"/26/actor/id"},
                {"op":"copy","from":"/24/actor/id","path":"/27/actor/id"},
                {"op":"copy","from":"/25/actor/id","path":"/28/actor/id"},
                {"op":"copy","from":"/26/actor/id","path":"/29/actor/id"},
                {"op":"copy","from":"/27/actor/id","path":"/30/actor/id"},
                {"op":"copy","from":"/28/actor/id","path":"/31/actor/id"},
                {"op":"copy","from":"/29/actor/id","path":"/32/actor/id"},
                {"op":"copy","from":"/30/actor/id","path":"/33/actor/id"},
                {"op":"copy","from":"/31/actor/id","path":"/34/actor/id"},
                {"op":"copy","from":"/32/actor/id","path":"/35/actor/id"},
                {"op":"copy","from":"/33/actor/id","path":"/36/actor/id"},
                {"op":"copy","from":"/34/actor/id","path":"/37/actor/id"},
                {"op":"copy","from":"/35/actor/id","path":"/38/actor/id"},
                {"op":"copy","from":"/36/actor/id","path":"/39/actor/id"},
                {"op":"copy","from":"/37/actor/id","path":"/40/actor/id"},
                {"op":"copy","from":"/38/actor/id","path":"/41/actor/id"},
                {"op":"copy","from":"/39/actor/id","path":"/42/actor/id"},
                {"op":"copy","from":"/40/actor/id","path":"/43/actor/id"},
                {"op":"copy","from":"/41/actor/id","path":"/44/actor/id"},
                {"op":"copy","from":"/42/actor/id","path":"/45/actor/id"},
                {"op":"copy","from":"/43/actor/id","path":"/46/actor/id"}
            ]
            """;

        this.v4Patch = BuildV4Patch(patchJson);
        this.v5Patch = BuildV5Patch(patchJson);

        this.GlobalSetupFile("PatchBenchmarks/large-array-file.json");
    }

    /// <summary>
    /// V5 builder creation only — measures the overhead of creating a mutable copy.
    /// </summary>
    [Benchmark]
    public void CreateV5BuilderOnly()
    {
        using var builder = this.CreateV5Builder();
    }

    /// <summary>
    /// Patch using Corvus V5.
    /// </summary>
    [Benchmark]
    public bool PatchCorvusV5()
    {
        using var builder = this.CreateV5Builder();
        JsonElement.Mutable target = builder.RootElement;
        return target.TryApplyPatch(in this.v5Patch);
    }

    /// <summary>
    /// Patch using Corvus V4.
    /// </summary>
    [Benchmark]
    public bool PatchCorvusV4()
    {
        bool result = Corvus.Json.Patch.JsonPatchExtensions.TryApplyPatch(this.V4Any, this.v4Patch, out _);
        if (!result)
        {
            throw new InvalidOperationException("V4 patch failed");
        }

        return result;
    }

    /// <summary>
    /// Patch using JsonEverything.
    /// </summary>
    [Benchmark(Baseline = true)]
    public bool PatchJsonEverything()
    {
        global::Json.Patch.PatchResult? result = this.jePatch?.Apply(this.ElementAsNode());
        if (result is not global::Json.Patch.PatchResult pr || !pr.IsSuccess)
        {
            throw new InvalidOperationException(result?.Error);
        }

        return true;
    }
}