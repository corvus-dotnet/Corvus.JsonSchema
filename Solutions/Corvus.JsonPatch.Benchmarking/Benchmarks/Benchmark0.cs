// <copyright file="Benchmark0.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
namespace Benchmarks
{
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Diagnosers;

    /// <summary>
    /// Additional properties benchmark.
    /// </summary>
    [MemoryDiagnoser]
    public class Benchmark0 : BenchmarkBase
    {
        private Corvus.Json.Patch.Model.PatchOperationArray corvusPatch;
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
                        Json.Pointer.JsonPointer.Create("/11/actor/id"),
                        Json.Pointer.JsonPointer.Create("/14/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/12/actor/id"),
                        Json.Pointer.JsonPointer.Create("/15/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/13/actor/id"),
                        Json.Pointer.JsonPointer.Create("/16/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/14/actor/id"),
                        Json.Pointer.JsonPointer.Create("/17/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/15/actor/id"),
                        Json.Pointer.JsonPointer.Create("/18/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/16/actor/id"),
                        Json.Pointer.JsonPointer.Create("/19/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/17/actor/id"),
                        Json.Pointer.JsonPointer.Create("/20/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/18/actor/id"),
                        Json.Pointer.JsonPointer.Create("/21/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/19/actor/id"),
                        Json.Pointer.JsonPointer.Create("/22/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/20/actor/id"),
                        Json.Pointer.JsonPointer.Create("/23/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/21/actor/id"),
                        Json.Pointer.JsonPointer.Create("/24/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/22/actor/id"),
                        Json.Pointer.JsonPointer.Create("/25/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/23/actor/id"),
                        Json.Pointer.JsonPointer.Create("/26/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/24/actor/id"),
                        Json.Pointer.JsonPointer.Create("/27/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/25/actor/id"),
                        Json.Pointer.JsonPointer.Create("/28/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/26/actor/id"),
                        Json.Pointer.JsonPointer.Create("/29/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/27/actor/id"),
                        Json.Pointer.JsonPointer.Create("/30/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/28/actor/id"),
                        Json.Pointer.JsonPointer.Create("/31/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/29/actor/id"),
                        Json.Pointer.JsonPointer.Create("/32/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/30/actor/id"),
                        Json.Pointer.JsonPointer.Create("/33/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/31/actor/id"),
                        Json.Pointer.JsonPointer.Create("/34/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/32/actor/id"),
                        Json.Pointer.JsonPointer.Create("/35/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/33/actor/id"),
                        Json.Pointer.JsonPointer.Create("/36/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/34/actor/id"),
                        Json.Pointer.JsonPointer.Create("/37/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/35/actor/id"),
                        Json.Pointer.JsonPointer.Create("/38/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/36/actor/id"),
                        Json.Pointer.JsonPointer.Create("/39/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/37/actor/id"),
                        Json.Pointer.JsonPointer.Create("/40/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/38/actor/id"),
                        Json.Pointer.JsonPointer.Create("/41/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/39/actor/id"),
                        Json.Pointer.JsonPointer.Create("/42/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/40/actor/id"),
                        Json.Pointer.JsonPointer.Create("/43/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/41/actor/id"),
                        Json.Pointer.JsonPointer.Create("/44/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/42/actor/id"),
                        Json.Pointer.JsonPointer.Create("/45/actor/id")),
                    Json.Patch.PatchOperation.Copy(
                        Json.Pointer.JsonPointer.Create("/43/actor/id"),
                        Json.Pointer.JsonPointer.Create("/46/actor/id")));

            this.corvusPatch =
                Corvus.Json.Patch.Model.PatchOperationArray.From(
                    Corvus.Json.Patch.Model.Copy.Create("/11/actor/id", "/14/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/12/actor/id", "/15/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/13/actor/id", "/16/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/14/actor/id", "/17/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/15/actor/id", "/18/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/16/actor/id", "/19/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/17/actor/id", "/20/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/18/actor/id", "/21/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/19/actor/id", "/22/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/20/actor/id", "/23/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/21/actor/id", "/24/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/22/actor/id", "/25/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/23/actor/id", "/26/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/24/actor/id", "/27/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/25/actor/id", "/28/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/26/actor/id", "/29/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/27/actor/id", "/30/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/28/actor/id", "/31/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/29/actor/id", "/32/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/30/actor/id", "/33/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/31/actor/id", "/34/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/31/actor/id", "/35/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/33/actor/id", "/36/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/34/actor/id", "/37/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/35/actor/id", "/38/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/36/actor/id", "/39/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/37/actor/id", "/40/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/38/actor/id", "/41/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/39/actor/id", "/42/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/40/actor/id", "/43/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/41/actor/id", "/44/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/42/actor/id", "/45/actor/id"),
                    Corvus.Json.Patch.Model.Copy.Create("/43/actor/id", "/46/actor/id"));

            await this.GlobalSetup("large-array-file.json").ConfigureAwait(false);
        }

        /// <summary>
        /// Validates using the Corvus types.
        /// </summary>
        [Benchmark]
        public void PatchCorvus()
        {
            bool result = Corvus.Json.Patch.JsonPatchExtensions.TryApplyPatch(this.Any, this.corvusPatch, out Corvus.Json.JsonAny output);
        }

        /// <summary>
        /// Validates using the Newtonsoft types.
        /// </summary>
        [Benchmark(Baseline = true)]
        public void PatchJsonEverything()
        {
            Json.Patch.PatchResult? patchResult = this.jePatch?.Apply(this.Node);
        }
    }
}
