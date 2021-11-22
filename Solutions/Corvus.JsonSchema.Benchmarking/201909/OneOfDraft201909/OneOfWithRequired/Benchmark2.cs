// <copyright file="Benchmark2.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
#pragma warning disable
namespace OneOfDraft201909Feature.OneOfWithRequired
{
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Diagnosers;
    using Corvus.JsonSchema.Benchmarking.Benchmarks;
    /// <summary>
    /// Additional properties benchmark.
    /// </summary>
    [MemoryDiagnoser]
    public class Benchmark2 : BenchmarkBase
    {
        /// <summary>
        /// Global setup.
        /// </summary>
        /// <returns>A <see cref="Task"/> which completes once setup is complete.</returns>
        [GlobalSetup]
        public Task GlobalSetup()
        {
            return this.GlobalSetup("draft2019-09\\oneOf.json", "#/8/schema", "#/008/tests/002/data", true);
        }
        /// <summary>
        /// Validates using the Corvus types.
        /// </summary>
        [Benchmark]
        public void ValidateCorvus()
        {
            this.ValidateCorvusCore<OneOfDraft201909Feature.OneOfWithRequired.Schema>();
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
