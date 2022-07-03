// <copyright file="BenchmarkBase.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Benchmarks
{
    using System.Text.Json;
    using Corvus.Json;

    /// <summary>
    /// Base class for benchmarks.
    /// </summary>
    public class BenchmarkBase
    {
        /// <summary>
        /// Gets the JsonNode for the benchmark.
        /// </summary>
        public JsonElement Element { get; private set; }

        /// <summary>
        /// Gets the JsonAny for the benchmark.
        /// </summary>
        public JsonAny Any { get; private set; }

        /// <summary>
        /// Set up the benchmark using the given file.
        /// </summary>
        /// <param name="filePath">The input file for the document to transform.</param>
        /// <returns>A <see cref="Task"/> which completes when setup is complete.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the input file could not be parsed.</exception>
        protected async Task GlobalSetup(string filePath)
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(filePath).ConfigureAwait(false));
            this.Element = doc.RootElement.Clone();

            this.Any = JsonAny.Parse(await File.ReadAllTextAsync(filePath).ConfigureAwait(false));
        }
    }
}
