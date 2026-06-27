// <copyright file="GenerationDriverTypeScript.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Json.CodeGenerator;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.TypeScript.CodeGeneration;
using Spectre.Console;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Drives TypeScript code generation — the TypeScript engine, a peer of <see cref="GenerationDriverV4"/>
/// and <see cref="GenerationDriverV5"/>. It reuses the V5 engine's language-neutral schema-loading,
/// vocabulary, and file-writing machinery, emitting idiomatic TypeScript (types + AOT validators + the
/// shared runtime) via the <see cref="TypeScriptLanguageProvider"/> instead of C#.
/// </summary>
public static class GenerationDriverTypeScript
{
    internal static async Task<int> GenerateTypes(GeneratorConfig generatorConfig, CodeGenerationMode codeGenerationMode, CancellationToken cancellationToken)
    {
        try
        {
            if (!generatorConfig.IsValid())
            {
                return GenerationDriverV5.WriteValidationErrors(generatorConfig);
            }

            CompoundDocumentResolver documentResolver = (generatorConfig.SupportYaml ?? false)
                ? new CompoundDocumentResolver(new FileSystemDocumentResolver(new YamlPreProcessor()), new HttpClientDocumentResolver(new HttpClient(), new YamlPreProcessor()))
                : new CompoundDocumentResolver(new FileSystemDocumentResolver(), new HttpClientDocumentResolver(new HttpClient()));

            documentResolver.AddMetaschema();

            await GenerationDriverV5.RegisterAdditionalFiles(generatorConfig, documentResolver);

            VocabularyRegistry vocabularyRegistry = GenerationDriverV5.RegisterVocabularies(documentResolver);
            IVocabulary defaultVocabulary = GenerationDriverV5.GetFallbackVocabulary(generatorConfig.UseSchemaValue ?? GeneratorConfig.UseSchema.DefaultInstance);

            JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

            Progress progress = AnsiConsole.Progress().Columns(new TaskDescriptionColumn { Alignment = Justify.Left });
            await progress.StartAsync(context => ExecuteTask(generatorConfig, context, defaultVocabulary, typeBuilder));
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return -1;
        }

        return 0;
    }

    /// <summary>
    /// In-process codegen worker for the TypeScript Bowtie harness. Reads NDJSON requests
    /// { "schema": &lt;schema&gt;, "registry"?: { uri: &lt;schema&gt; }, "out": &lt;dir&gt; } on stdin and, for each,
    /// generates the module — registering the Bowtie registry of remote schemas in the document resolver via
    /// <c>AddDocument</c> (the v5-harness approach: no HTTP/file remote resolution, no server) — then writes
    /// { "ok": true } or { "error": ... }. Long-running, so there is no per-schema process spawn.
    /// </summary>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunBowtieCodegenLoopAsync()
    {
        string? line;
        while ((line = await Console.In.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using System.Text.Json.JsonDocument req = System.Text.Json.JsonDocument.Parse(line);
                System.Text.Json.JsonElement r = req.RootElement;
                string schema = r.GetProperty("schema").GetRawText();
                string outDir = r.GetProperty("out").GetString()!;
                var registry = new Dictionary<string, System.Text.Json.JsonDocument>();
                if (r.TryGetProperty("registry", out System.Text.Json.JsonElement reg) && reg.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (System.Text.Json.JsonProperty p in reg.EnumerateObject())
                    {
                        registry[p.Name] = System.Text.Json.JsonDocument.Parse(p.Value.GetRawText());
                    }
                }

                await GenerateBowtieModuleAsync(schema, registry, outDir);
                await Console.Out.WriteLineAsync("{\"ok\":true}");
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message }));
            }

            await Console.Out.FlushAsync();
        }

        return 0;
    }

    private static async Task GenerateBowtieModuleAsync(string schemaJson, Dictionary<string, System.Text.Json.JsonDocument> registry, string outDir)
    {
        // No FileSystem/HTTP remote resolution (cf. the v5 harness' allowFileSystemAndHttpResolution:false):
        // the metaschemas come from AddMetaschema and every remote $ref comes from the prepopulated registry.
        CompoundDocumentResolver resolver = new(new FileSystemDocumentResolver());
        resolver.AddMetaschema();
        foreach ((string uri, System.Text.Json.JsonDocument doc) in registry)
        {
            resolver.AddDocument(uri, doc);
        }

        VocabularyRegistry vocabularyRegistry = GenerationDriverV5.RegisterVocabularies(resolver);
        IVocabulary fallback = GenerationDriverV5.GetFallbackVocabulary(GeneratorConfig.UseSchema.DefaultInstance);
        JsonSchemaTypeBuilder typeBuilder = new(resolver, vocabularyRegistry);

        // Normalize the synthetic main reference the same way the SuiteHarness does, so the AddDocument key and
        // the JsonReference lookup agree (an un-normalized ref collapses its slashes and the lookup misses).
        string mainRef = Path.Combine(outDir, "__corvus_main.json");
        if (SchemaReferenceNormalization.TryNormalizeSchemaReference(mainRef, out string? normalized))
        {
            mainRef = normalized;
        }

        typeBuilder.AddDocument(mainRef, System.Text.Json.JsonDocument.Parse(schemaJson));
        TypeDeclaration root = await typeBuilder.AddTypeDeclarationsAsync(new JsonReference(mainRef), fallback, true);

        // Annotation-only (the default provider): Bowtie's per-dialect suite tests format/content as
        // ANNOTATIONS ("invalid email string is only an annotation" -> valid), matching the SuiteHarness'
        // CreateDefault main-suite run. format ASSERTION is a separate, opt-in Bowtie run.
        TypeScriptLanguageProvider provider = TypeScriptLanguageProvider.CreateDefault();
        IReadOnlyCollection<GeneratedCodeFile> files = typeBuilder.GenerateCodeUsing(provider, new[] { root }, CancellationToken.None);
        string export = provider.RootEvaluatorExport(root);

        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "corvus-runtime.ts"), TypeScriptLanguageProvider.RuntimeModuleSource());
        GeneratedCodeFile generated = files.First(f => f.FileName == "generated.ts");
        File.WriteAllText(Path.Combine(outDir, "generated.ts"), generated.FileContent + export);
    }

    private static async Task ExecuteTask(GeneratorConfig generatorConfig, ProgressContext context, IVocabulary defaultVocabulary, JsonSchemaTypeBuilder typeBuilder)
    {
        ProgressTask outerTask = context.AddTask("Generating TypeScript types", maxValue: generatorConfig.TypesToGenerate.GetArrayLength());

        List<TypeDeclaration> typesToGenerate = [];
        TypeDeclaration? rootType = null;
        string? fallbackOutputPath = null;

        // Language-neutral schema/type building (no .NET named-types/namespace/accessibility machinery).
        foreach (GeneratorConfig.GenerationSpecification generatorSpecification in generatorConfig.TypesToGenerate)
        {
            string schemaFile = (string)generatorSpecification.SchemaFile;
            JsonReference reference = new(schemaFile, generatorSpecification.RootPath is { } rootPath ? (string)rootPath : string.Empty);
            ProgressTask typeBuilderTask = context.AddTask($"Building type declarations for [green]{reference}[/]");
            TypeDeclaration built = await typeBuilder.AddTypeDeclarationsAsync(reference, defaultVocabulary, generatorSpecification.RebaseToRootPath ?? false);
            typesToGenerate.Add(built);
            rootType ??= built;

            if (fallbackOutputPath is null && Path.Exists(schemaFile))
            {
                fallbackOutputPath = Path.GetDirectoryName(schemaFile);
            }

            typeBuilderTask.Increment(100);
            outerTask.Increment(1);
            typeBuilderTask.StopTask();
        }

        ProgressTask currentTask = context.AddTask("Generating TypeScript for schema.");

        // Where generated modules import the shared runtime from: the --tsRuntimeModule option, else the
        // CORVUS_TS_RUNTIME_MODULE env var, else the self-contained default. A bare specifier (e.g.
        // "@corvus/json-runtime") imports the installed package and skips re-emitting the runtime.
        string runtimeModule =
            generatorConfig.TsRuntimeModule?.GetString() is { Length: > 0 } configured
                ? configured
                : Environment.GetEnvironmentVariable("CORVUS_TS_RUNTIME_MODULE") is { Length: > 0 } env
                    ? env
                    : "./corvus-runtime.js";
        TypeScriptLanguageProvider provider = TypeScriptLanguageProvider.DefaultWithOptions(
            new TypeScriptLanguageProvider.Options(
                AlwaysAssertFormat: generatorConfig.AssertFormat ?? true,
                RuntimeModuleSpecifier: runtimeModule));
        IReadOnlyCollection<GeneratedCodeFile> generatedCode = typeBuilder.GenerateCodeUsing(provider, typesToGenerate, CancellationToken.None);

        // Append a stable `evaluateRoot` entry point (aliases the root type's validator) to the types module.
        if (rootType is { } root)
        {
            string export = provider.RootEvaluatorExport(root);
            generatedCode = generatedCode
                .Select(f => f.FileName == "generated.ts"
                    ? new GeneratedCodeFile(f.FileName, f.FileContent + export, f.TypeDeclaration)
                    : f)
                .ToArray();
        }

        currentTask.Increment(100);
        currentTask.StopTask();

        string outputPath = generatorConfig.OutputPath?.GetString() ?? fallbackOutputPath ?? Environment.CurrentDirectory;
        if (!string.IsNullOrEmpty(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        currentTask = await GenerationDriverV5.WriteFiles(generatorConfig, context, generatedCode, outputPath);
        currentTask.StopTask();

        outerTask.Increment(100);
        AnsiConsole.MarkupLineInterpolated($"Completed in: [green]{outerTask.ElapsedTime?.TotalSeconds}s[/]");
        outerTask.StopTask();
    }
}