// <copyright file="BenchmarkBase.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Benchmarks
{
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Corvus.Json;

    /// <summary>
    /// Base class for benchmarks.
    /// </summary>
    public class BenchmarkBase
    {
        private JsonElement element;

        /// <summary>
        /// Gets the JsonAny for the benchmark.
        /// </summary>
        public JsonAny Any { get; private set; }

        /// <summary>
        /// Builds a JE patch from a JSON string.
        /// </summary>
        /// <param name="patch">The JSON string containing the patch to use.</param>
        /// <returns>The <see cref="Json.Patch.JsonPatch"/> built from the string.</returns>
        protected static Json.Patch.JsonPatch BuildJEPatch(string patch)
        {
#pragma warning disable SA1009 // Closing parenthesis should be spaced correctly
            return JsonSerializer.Deserialize<Json.Patch.JsonPatch>(patch)!;
#pragma warning restore SA1009 // Closing parenthesis should be spaced correctly
        }

        /// <summary>
        /// Set up the benchmark using the given file.
        /// </summary>
        /// <param name="filePath">The input file for the document to transform.</param>
        /// <returns>A <see cref="Task"/> which completes when setup is complete.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the input file could not be parsed.</exception>
        protected async Task GlobalSetup(string filePath)
        {
            string jsonString = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
            await this.GlobalSetupJson(jsonString).ConfigureAwait(false);
        }

        /// <summary>
        /// Set up the benchmark using the given json string.
        /// </summary>
        /// <param name="jsonString">The input json for the document to transform.</param>
        /// <returns>A <see cref="Task"/> which completes when setup is complete.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the input file could not be parsed.</exception>
        protected Task GlobalSetupJson(string jsonString)
        {
            using var doc = JsonDocument.Parse(jsonString);
            this.element = doc.RootElement.Clone();

            this.Any = JsonAny.Parse(jsonString);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the element as a JsonNode.
        /// </summary>
        /// <returns>The json node for the element.</returns>
        protected System.Text.Json.Nodes.JsonNode? ElementAsNode()
        {
            return this.element.ValueKind switch
            {
                JsonValueKind.Object => System.Text.Json.Nodes.JsonObject.Create(this.element),
                JsonValueKind.Array => System.Text.Json.Nodes.JsonArray.Create(this.element),
                _ => System.Text.Json.Nodes.JsonValue.Create(this.element),
            };
        }
    }
}
