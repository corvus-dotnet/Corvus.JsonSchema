// <copyright file="JsonSchemaBuilderDriver201909.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Drivers
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Text;
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Corvus.Extensions;
    using Corvus.Json;
    using Corvus.Json.JsonSchema.TypeBuilder.Draft201909;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Emit;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// A driver for specs for the <see cref="JsonSchemaBuilder"/>.
    /// </summary>
    public class JsonSchemaBuilderDriver201909 : IJsonSchemaBuilderDriver
    {
        private readonly IConfiguration configuration;
        private readonly JsonSchemaBuilder builder;
        private readonly IDocumentResolver documentResolver = new FileSystemDocumentResolver();
        private TestAssemblyLoadContext? assemblyLoadContext = new ();

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSchemaBuilderDriver201909"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="builder">The <see cref="JsonSchemaBuilder"/> instance to drive.</param>
        public JsonSchemaBuilderDriver201909(IConfiguration configuration, JsonSchemaBuilder builder)
        {
            this.configuration = configuration;
            this.builder = builder;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.assemblyLoadContext is not null)
            {
                this.documentResolver.Dispose();
                this.assemblyLoadContext!.Unload();
                this.assemblyLoadContext = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        /// <inheritdoc/>
        public Task<JsonElement?> GetElement(string filename, string referenceFragment)
        {
            string baseDirectory = this.configuration["jsonSchemaBuilder201909DriverSettings:testBaseDirectory"];
            string path = Path.Combine(baseDirectory, filename);
            return this.documentResolver.TryResolve(new JsonReference(path, referenceFragment));
        }

        /// <inheritdoc/>
        public async Task<Type> GenerateTypeFor(bool writeBenchmarks, int index, string filename, string schemaPath, string dataPath, string featureName, string scenarioName, bool valid)
        {
            string baseDirectory = this.configuration["jsonSchemaBuilder201909DriverSettings:testBaseDirectory"];
            string path = Path.Combine(baseDirectory, filename) + schemaPath;

            path = await this.builder.RebaseReferenceAsRootDocument(path).ConfigureAwait(false);

            (string rootTypeName, ImmutableDictionary<string, (string dotnetTypeName, string code)> generatedTypes) = await this.builder.BuildTypesFor(path, $"{featureName}Feature.{scenarioName}").ConfigureAwait(false);

            bool isCorvusType = rootTypeName.StartsWith("Corvus.");

            if (writeBenchmarks)
            {
                string outputBaseDirectory = this.configuration["jsonSchemaBuilder201909DriverSettings:benchmarkOutputPath"];
                WriteBenchmarks(index, filename, schemaPath, dataPath, featureName, scenarioName, generatedTypes, rootTypeName, valid, outputBaseDirectory);
            }

            IEnumerable<SyntaxTree> syntaxTrees = ParseSyntaxTrees(generatedTypes);

            // We are happy with the defaults (debug etc.)
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
            IEnumerable<MetadataReference> references = BuildMetadataReferences();
            var compilation = CSharpCompilation.Create($"Driver.GeneratedTypes_{Guid.NewGuid()}", syntaxTrees, references, options);

            using var outputStream = new MemoryStream();
            EmitResult result = compilation.Emit(outputStream);

            if (!result.Success)
            {
                throw new Exception("Unable to compile generated code\r\n" + BuildCompilationErrors(result));
            }

            outputStream.Flush();
            outputStream.Position = 0;

            Assembly generatedAssembly = this.assemblyLoadContext!.LoadFromStream(outputStream);

            if (isCorvusType)
            {
                return AssemblyLoadContext.Default.Assemblies.Where(a => a.GetName().Name == "Corvus.Json.ExtendedTypes").Single().ExportedTypes.Where(t => t.FullName == rootTypeName).Single();
            }

            return generatedAssembly.ExportedTypes.Where(t => t.FullName == rootTypeName).Single();
        }

        /// <inheritdoc/>
        public IJsonValue CreateInstance(Type type, JsonElement data)
        {
            ConstructorInfo? constructor = type.GetConstructor(new[] { typeof(JsonElement) });
            if (constructor is null)
            {
                throw new InvalidOperationException($"Unable to find the public JsonElement constructor on type '{type.FullName}'");
            }

            return CastTo<IJsonValue>.From(constructor.Invoke(new object[] { data }));
        }

        /// <inheritdoc/>
        public IJsonValue CreateInstance(Type type, string data)
        {
            using var document = JsonDocument.Parse(data);
            return this.CreateInstance(type, document.RootElement.Clone());
        }

        private static void WriteBenchmarks(int index, string filename, string schemaPath, string dataPath, string featureName, string scenarioName, ImmutableDictionary<string, (string, string)> generatedTypes, string rootTypeName, bool valid, string outputBaseDirectory)
        {
            foreach (KeyValuePair<string, (string dotnetTypeName, string code)> item in generatedTypes)
            {
                string path = Path.Combine(outputBaseDirectory, $@"{featureName}\{scenarioName}");
                Directory.CreateDirectory(path);
                File.WriteAllText(Path.ChangeExtension(Path.Combine(path, item.Value.dotnetTypeName), ".cs"), item.Value.code);
                File.WriteAllText(Path.Combine(path, $"Benchmark{index}.cs"), BuildBenchmark(index, filename, schemaPath, dataPath, featureName, scenarioName, rootTypeName, valid));
            }
        }

        private static string BuildBenchmark(int index, string filename, string schemaPath, string dataPath, string featureName, string scenarioName, string rootTypeName, bool valid)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"// <copyright file=\"Benchmark{index}.cs\" company=\"Endjin Limited\">");
            builder.AppendLine("// Copyright (c) Endjin Limited. All rights reserved.");
            builder.AppendLine("// </copyright>");
            builder.AppendLine("#pragma warning disable");
            builder.AppendLine($"namespace {featureName}Feature.{scenarioName}");
            builder.AppendLine("{");
            builder.AppendLine("    using System.Threading.Tasks;");
            builder.AppendLine("    using BenchmarkDotNet.Attributes;");
            builder.AppendLine("    using BenchmarkDotNet.Diagnosers;");
            builder.AppendLine("    using Corvus.JsonSchema.Benchmarking.Benchmarks;");
            builder.AppendLine("    /// <summary>");
            builder.AppendLine("    /// Additional properties benchmark.");
            builder.AppendLine("    /// </summary>");
            builder.AppendLine("    [MemoryDiagnoser]");
            builder.AppendLine($"    public class Benchmark{index} : BenchmarkBase");
            builder.AppendLine("    {");
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Global setup.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        /// <returns>A <see cref=\"Task\"/> which completes once setup is complete.</returns>");
            builder.AppendLine("        [GlobalSetup]");
            builder.AppendLine("        public Task GlobalSetup()");
            builder.AppendLine("        {");
            builder.AppendLine($"            return this.GlobalSetup(\"draft2019-09\\\\{filename}\", \"{schemaPath}\", \"{dataPath}\", {valid.ToString().ToLowerInvariant()});");
            builder.AppendLine("        }");
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Validates using the Corvus types.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        [Benchmark]");
            builder.AppendLine("        public void ValidateCorvus()");
            builder.AppendLine("        {");
            builder.AppendLine($"            this.ValidateCorvusCore<{rootTypeName}>();");
            builder.AppendLine("        }");
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Validates using the Newtonsoft types.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        [Benchmark]");
            builder.AppendLine("        public void ValidateNewtonsoft()");
            builder.AppendLine("        {");
            builder.AppendLine("            this.ValidateNewtonsoftCore();");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string BuildCompilationErrors(EmitResult result)
        {
            var builder = new StringBuilder();
            foreach (Diagnostic diagnostic in result.Diagnostics)
            {
                builder.AppendLine(diagnostic.ToString());
            }

            return builder.ToString();
        }

        private static IEnumerable<MetadataReference> BuildMetadataReferences()
        {
            return new MetadataReference[]
            {
                MetadataReference.CreateFromFile(AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "netstandard").Location),
                MetadataReference.CreateFromFile(AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "System.Runtime").Location),
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IEnumerable<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(JavaScriptEncoder).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Stack<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Uri).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(JsonElement).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CastTo<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ImmutableArray).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ReadOnlySequence<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Regex).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(UrlEncoder).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(JsonAny).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            };
        }

        private static IEnumerable<SyntaxTree> ParseSyntaxTrees(ImmutableDictionary<string, (string, string)> generatedTypes)
        {
            foreach (KeyValuePair<string, (string dotnetTypeName, string code)> type in generatedTypes)
            {
                yield return CSharpSyntaxTree.ParseText(type.Value.code, path: type.Key);
            }
        }

        private class TestAssemblyLoadContext : AssemblyLoadContext
        {
            public TestAssemblyLoadContext()
                : base($"TestAssemblyLoadContext_{Guid.NewGuid():N}", isCollectible: true)
            {
            }
        }
    }
}
