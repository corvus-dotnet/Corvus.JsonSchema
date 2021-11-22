// <copyright file="Benchmark3.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
#pragma warning disable
namespace FormatDraft202012Feature.ValidationOfIDHostnames
{
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Diagnosers;
    using Corvus.JsonSchema.Benchmarking.Benchmarks;
    /// <summary>
    /// Additional properties benchmark.
    /// </summary>
    [MemoryDiagnoser]
    public class Benchmark3 : BenchmarkBase
    {
        /// <summary>
        /// Global setup.
        /// </summary>
        /// <returns>A <see cref="Task"/> which completes once setup is complete.</returns>
        [GlobalSetup]
        public Task GlobalSetup()
        {
            return this.GlobalSetup("draft2020-12\\format.json", "#/5/schema", "#/005/tests/003/data", true);
        }
        /// <summary>
        /// Validates using the Corvus types.
        /// </summary>
        [Benchmark]
        public void ValidateCorvus()
        {
            this.ValidateCorvusCore<FormatDraft202012Feature.ValidationOfIDHostnames.Schema>();
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
