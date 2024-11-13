// <copyright file="LargeFileBenchmark.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace Benchmarks;

/// <summary>
/// Additional properties benchmark.
/// </summary>
[MemoryDiagnoser]
public class LargeFileBenchmark : BenchmarkBase
{
    private Corvus.Json.Patch.Model.JsonPatchDocument corvusPatch;
    private Json.Patch.JsonPatch? jePatch;

    /// <summary>
    /// Global setup.
    /// </summary>
    /// <returns>A <see cref="Task"/> which completes once setup is complete.</returns>
    [GlobalSetup]
    public async Task GlobalSetup()
    {
        this.jePatch =
            new Json.Patch.JsonPatch(
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/11/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/14/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/12/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/15/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/13/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/16/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/14/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/17/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/15/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/18/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/16/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/19/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/17/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/20/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/18/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/21/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/19/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/22/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/20/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/23/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/21/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/24/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/22/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/25/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/23/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/26/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/24/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/27/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/25/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/28/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/26/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/29/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/27/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/30/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/28/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/31/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/29/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/32/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/30/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/33/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/31/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/34/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/32/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/35/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/33/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/36/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/34/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/37/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/35/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/38/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/36/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/39/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/37/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/40/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/38/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/41/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/39/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/42/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/40/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/43/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/41/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/44/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/42/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/45/actor/id")),
                Json.Patch.PatchOperation.Copy(
                    Json.Pointer.JsonPointer.Parse("/43/actor/id"),
                    Json.Pointer.JsonPointer.Parse("/46/actor/id")));

        this.corvusPatch =
            Corvus.Json.Patch.Model.JsonPatchDocument.FromItems(
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/11/actor/id", "/14/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/12/actor/id", "/15/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/13/actor/id", "/16/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/14/actor/id", "/17/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/15/actor/id", "/18/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/16/actor/id", "/19/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/17/actor/id", "/20/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/18/actor/id", "/21/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/19/actor/id", "/22/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/20/actor/id", "/23/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/21/actor/id", "/24/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/22/actor/id", "/25/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/23/actor/id", "/26/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/24/actor/id", "/27/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/25/actor/id", "/28/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/26/actor/id", "/29/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/27/actor/id", "/30/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/28/actor/id", "/31/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/29/actor/id", "/32/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/30/actor/id", "/33/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/31/actor/id", "/34/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/31/actor/id", "/35/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/33/actor/id", "/36/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/34/actor/id", "/37/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/35/actor/id", "/38/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/36/actor/id", "/39/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/37/actor/id", "/40/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/38/actor/id", "/41/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/39/actor/id", "/42/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/40/actor/id", "/43/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/41/actor/id", "/44/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/42/actor/id", "/45/actor/id"),
                Corvus.Json.Patch.Model.JsonPatchDocument.CopyOperation.Create("/43/actor/id", "/46/actor/id"));

        await this.GlobalSetup("large-array-file.json");
    }

    /// <summary>
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void PatchCorvus()
    {
        bool result = Corvus.Json.Patch.JsonPatchExtensions.TryApplyPatch(this.Any, this.corvusPatch, out _);
        if (!result)
        {
            throw new Exception("Fail!");
        }
    }

    /// <summary>
    /// Validates using the JsonEverything types.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void PatchJsonEverything()
    {
        Json.Patch.PatchResult? patchResult = this.jePatch?.Apply(this.ElementAsNode());
        if (patchResult is not Json.Patch.PatchResult pr || !pr.IsSuccess)
        {
            throw new Exception(patchResult?.Error);
        }
    }
}