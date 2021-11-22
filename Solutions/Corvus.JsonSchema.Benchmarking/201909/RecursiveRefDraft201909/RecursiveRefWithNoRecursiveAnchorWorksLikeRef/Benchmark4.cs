// <copyright file="Benchmark4.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
#pragma warning disable
namespace RecursiveRefDraft201909Feature.RecursiveRefWithNoRecursiveAnchorWorksLikeRef
{
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Diagnosers;
    using Corvus.JsonSchema.Benchmarking.Benchmarks;
    /// <summary>
    /// Additional properties benchmark.
    /// </summary>
    [MemoryDiagnoser]
    public class Benchmark4 : BenchmarkBase
    {
        /// <summary>
        /// Global setup.
        /// </summary>
        /// <returns>A <see cref="Task"/> which completes once setup is complete.</returns>
        [GlobalSetup]
        public Task GlobalSetup()
        {
            return this.GlobalSetup("draft2019-09\\recursiveRef.json", "#/4/schema", "#/004/tests/004/data", false);
        }
        /// <summary>
        /// Validates using the Corvus types.
        /// </summary>
        [Benchmark]
        public void ValidateCorvus()
        {
            this.ValidateCorvusCore<RecursiveRefDraft201909Feature.RecursiveRefWithNoRecursiveAnchorWorksLikeRef.SchemaJson>();
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
