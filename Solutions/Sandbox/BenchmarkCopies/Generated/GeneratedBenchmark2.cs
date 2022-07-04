// <copyright file="GeneratedBenchmark2.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
#pragma warning disable
namespace Benchmarks
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// spec_tests - A.3.  Removing an Object Member.
    /// </summary>
    public class GeneratedBenchmark2 : BenchmarkBase
    {
        private Corvus.Json.Patch.Model.PatchOperationArray corvusPatch;
        private Json.Patch.JsonPatch? jePatch;

        /// <summary>
        /// Global setup.
        /// </summary>
        /// <returns>A <see cref="Task"/> which completes once setup is complete.</returns>
        public async Task GlobalSetup()
        {
            this.jePatch = BuildJEPatch("[{\"op\":\"remove\",\"path\":\"/baz\"}]");
                
            this.corvusPatch = Corvus.Json.JsonAny.Parse("[{\"op\":\"remove\",\"path\":\"/baz\"}]");

            await this.GlobalSetupJson("{\"baz\":\"qux\",\"foo\":\"bar\"}").ConfigureAwait(false);
        }

        /// <summary>
        /// Just remove the value explicitly
        /// </summary>
        public void RemoveExplicitly()
        {
            this.Any.AsObject.RemoveProperty("baz");
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
            catch(Exception)
            {
                // Push this out into a long wait for failure.
                Task.Delay(1000).Wait();
            }
        }
    }
}
