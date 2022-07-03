// <copyright file="BenchmarkBase.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Benchmarks
{
    using System.Text.Json.Nodes;
    using Corvus.Json;

    /// <summary>
    /// Base class for benchmarks.
    /// </summary>
    public class BenchmarkBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BenchmarkBase"/> class.
        /// </summary>
        protected BenchmarkBase()
        {
            this.Node = JsonValue.Create(false);
        }

        /// <summary>
        /// Gets the JsonNode for the benchmark.
        /// </summary>
        public JsonNode Node { get; private set; }

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
            var node = JsonNode.Parse(await File.ReadAllTextAsync(filePath).ConfigureAwait(false));

            if (node is JsonNode n)
            {
                this.Node = n;
            }
            else
            {
                throw new InvalidOperationException($"Bad input file {filePath}");
            }

            this.Any = JsonAny.Parse(await File.ReadAllTextAsync(filePath).ConfigureAwait(false));
        }
    }
}
