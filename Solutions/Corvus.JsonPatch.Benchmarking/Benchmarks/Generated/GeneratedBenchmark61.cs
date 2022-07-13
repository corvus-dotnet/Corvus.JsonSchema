// <copyright file="GeneratedBenchmark61.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
#pragma warning disable
namespace Benchmarks
{
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Diagnosers;

    /// <summary>
    /// tests - Undescribed scenario.
    /// </summary>
    [MemoryDiagnoser]
    public class GeneratedBenchmark61 : BenchmarkBase
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
            this.jePatch = BuildJEPatch("[{\"op\":\"test\",\"path\":\"/foo\",\"value\":[\"bar\",\"baz\"]},{\"op\":\"test\",\"path\":\"/foo/0\",\"value\":\"bar\"},{\"op\":\"test\",\"path\":\"/\",\"value\":0},{\"op\":\"test\",\"path\":\"/a~1b\",\"value\":1},{\"op\":\"test\",\"path\":\"/c%d\",\"value\":2},{\"op\":\"test\",\"path\":\"/e^f\",\"value\":3},{\"op\":\"test\",\"path\":\"/g|h\",\"value\":4},{\"op\":\"test\",\"path\":\"/i\\\\j\",\"value\":5},{\"op\":\"test\",\"path\":\"/k\\u0022l\",\"value\":6},{\"op\":\"test\",\"path\":\"/ \",\"value\":7},{\"op\":\"test\",\"path\":\"/m~0n\",\"value\":8}]");
                
            this.corvusPatch = Corvus.Json.JsonAny.Parse("[{\"op\":\"test\",\"path\":\"/foo\",\"value\":[\"bar\",\"baz\"]},{\"op\":\"test\",\"path\":\"/foo/0\",\"value\":\"bar\"},{\"op\":\"test\",\"path\":\"/\",\"value\":0},{\"op\":\"test\",\"path\":\"/a~1b\",\"value\":1},{\"op\":\"test\",\"path\":\"/c%d\",\"value\":2},{\"op\":\"test\",\"path\":\"/e^f\",\"value\":3},{\"op\":\"test\",\"path\":\"/g|h\",\"value\":4},{\"op\":\"test\",\"path\":\"/i\\\\j\",\"value\":5},{\"op\":\"test\",\"path\":\"/k\\u0022l\",\"value\":6},{\"op\":\"test\",\"path\":\"/ \",\"value\":7},{\"op\":\"test\",\"path\":\"/m~0n\",\"value\":8}]");

            await this.GlobalSetupJson("{\"foo\":[\"bar\",\"baz\"],\"\":0,\"a/b\":1,\"c%d\":2,\"e^f\":3,\"g|h\":4,\"i\\\\j\":5,\"k\\u0022l\":6,\" \":7,\"m~n\":8}").ConfigureAwait(false);
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
