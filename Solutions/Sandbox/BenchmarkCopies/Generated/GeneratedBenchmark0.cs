// <copyright file="GeneratedBenchmark0.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
#pragma warning disable
namespace Benchmarks
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// spec_tests - A.1.  Adding an Object Member.
    /// </summary>
    public class GeneratedBenchmark0 : BenchmarkBase
    {
        private Corvus.Json.Patch.Model.PatchOperationArray corvusPatch;
        private Json.Patch.JsonPatch? jePatch;

        /// <summary>
        /// Global setup.
        /// </summary>
        /// <returns>A <see cref="Task"/> which completes once setup is complete.</returns>
        public async Task GlobalSetup()
        {
            this.jePatch = BuildJEPatch("[{\"op\":\"add\",\"path\":\"/baz\",\"value\":\"qux\"}]");
                
            this.corvusPatch = Corvus.Json.JsonAny.Parse("[{\"op\":\"add\",\"path\":\"/baz\",\"value\":\"qux\"}]");

            await this.GlobalSetupJson("{\"foo\":\"bar\"}").ConfigureAwait(false);
        }

        /// <summary>
        /// Validates using the Corvus types.
        /// </summary>
        public void PatchCorvus()
        {
            bool result = Corvus.Json.Patch.JsonPatchExtensions.TryApplyPatch(this.Any, this.corvusPatch, out Corvus.Json.JsonAny output);
        }

        /// <summary>
        /// Validates using the Newtonsoft types.
        /// </summary>
        public void PatchJsonEverything()
        {
            try
            {
               Json.Patch.PatchResult? patchResult = this.jePatch?.Apply(ElementAsNode());
            }
            catch(Exception ex)
            {
                // Push this out into a long wait for failure.
                Console.WriteLine(ex);
            }
        }
    }
}
