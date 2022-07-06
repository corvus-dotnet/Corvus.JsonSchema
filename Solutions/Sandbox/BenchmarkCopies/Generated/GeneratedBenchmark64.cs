// <copyright file="GeneratedBenchmark64.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
#pragma warning disable
namespace Benchmarks
{
    using System.Threading.Tasks;

    /// <summary>
    /// tests - Undescribed scenario.
    /// </summary>
    public class GeneratedBenchmark64 : BenchmarkBase
    {
        private Corvus.Json.Patch.Model.PatchOperationArray corvusPatch;
        private Json.Patch.JsonPatch? jePatch;

        /// <summary>
        /// Global setup.
        /// </summary>
        /// <returns>A <see cref="Task"/> which completes once setup is complete.</returns>
        public async Task GlobalSetup()
        {
            this.jePatch = BuildJEPatch("[{\"op\":\"move\",\"from\":\"/baz/0/qux\",\"path\":\"/baz/1\"}]");
                
            this.corvusPatch = Corvus.Json.JsonAny.Parse("[{\"op\":\"move\",\"from\":\"/baz/0/qux\",\"path\":\"/baz/1\"}]");

            await this.GlobalSetupJson("{\"baz\":[{\"qux\":\"hello\"}],\"bar\":1}").ConfigureAwait(false);
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
                // Swallow failures until we can diagnose the issue with running inside BMDN
                // https://github.com/dotnet/BenchmarkDotNet/issues/2032
            }
        }
    }
}
