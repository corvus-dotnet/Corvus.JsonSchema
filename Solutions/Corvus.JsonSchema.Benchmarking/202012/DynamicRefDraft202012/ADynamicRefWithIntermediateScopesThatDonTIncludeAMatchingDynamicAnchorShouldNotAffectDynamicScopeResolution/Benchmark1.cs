// <copyright file="Benchmark1.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
#pragma warning disable
namespace DynamicRefDraft202012Feature.ADynamicRefWithIntermediateScopesThatDonTIncludeAMatchingDynamicAnchorShouldNotAffectDynamicScopeResolution
{
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Diagnosers;
    using Corvus.JsonSchema.Benchmarking.Benchmarks;
    /// <summary>
    /// Additional properties benchmark.
    /// </summary>
    [MemoryDiagnoser]
    public class Benchmark1 : BenchmarkBase
    {
        /// <summary>
        /// Global setup.
        /// </summary>
        /// <returns>A <see cref="Task"/> which completes once setup is complete.</returns>
        [GlobalSetup]
        public Task GlobalSetup()
        {
            return this.GlobalSetup("draft2020-12\\dynamicRef.json", "#/4/schema", "#/004/tests/001/data", false);
        }
        /// <summary>
        /// Validates using the Corvus types.
        /// </summary>
        [Benchmark]
        public void ValidateCorvus()
        {
            this.ValidateCorvusCore<DynamicRefDraft202012Feature.ADynamicRefWithIntermediateScopesThatDonTIncludeAMatchingDynamicAnchorShouldNotAffectDynamicScopeResolution.JsonStringArray>();
        }
        /// <summary>
        /// Validates using the Newtonsoft types.
        /// </summary>
        [Benchmark]
        public void ValidateNewtonsoft()
        {
            this.ValidateNewtonsoftCore();
        }
    }
}
