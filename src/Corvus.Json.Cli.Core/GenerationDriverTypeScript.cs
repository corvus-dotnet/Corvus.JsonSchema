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
            await progress.StartAsync(context => ExecuteTask(generatorConfig, context, defaultVocabulary, typeBuilder, codeGenerationMode));
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return -1;
        }

        return 0;
    }

    private static async Task ExecuteTask(GeneratorConfig generatorConfig, ProgressContext context, IVocabulary defaultVocabulary, JsonSchemaTypeBuilder typeBuilder, CodeGenerationMode codeGenerationMode)
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
        // "@endjin/corvus-json-runtime") imports the installed package and skips re-emitting the runtime.
        string runtimeModule =
            generatorConfig.TsRuntimeModule?.GetString() is { Length: > 0 } configured
                ? configured
                : Environment.GetEnvironmentVariable("CORVUS_TS_RUNTIME_MODULE") is { Length: > 0 } env
                    ? env
                    : "./corvus-runtime.js";

        // The TypeScript engine always emits the evaluate{Type} validators, so TypeGeneration and Both collapse
        // to the full type-surface-plus-validators output; only SchemaEvaluationOnly suppresses the type surface.
        // --tsModulePerType (gap A1) emits one module per type + a barrel index.ts instead of one generated.ts.
        bool modulePerType = generatorConfig.TsModulePerType ?? false;
        TypeScriptLanguageProvider provider = TypeScriptLanguageProvider.DefaultWithOptions(
            new TypeScriptLanguageProvider.Options(
                AlwaysAssertFormat: generatorConfig.AssertFormat ?? true,
                RuntimeModuleSpecifier: runtimeModule,
                EmitTypeSurface: codeGenerationMode != CodeGenerationMode.SchemaEvaluationOnly,
                ModulePerType: modulePerType));
        IReadOnlyCollection<GeneratedCodeFile> generatedCode = typeBuilder.GenerateCodeUsing(provider, typesToGenerate, CancellationToken.None);

        // Append a stable `evaluateRoot` entry point (aliases the root type's validator). In single-file mode
        // it goes on generated.ts; in module-per-type mode it goes on the barrel index.ts (with the imports the
        // barrel's `export *` re-exports cannot bring into local scope).
        if (rootType is { } root)
        {
            string targetFile = modulePerType ? "index.ts" : "generated.ts";
            string export = modulePerType ? provider.RootEvaluatorBarrelExport(root) : provider.RootEvaluatorExport(root);
            generatedCode = generatedCode
                .Select(f => f.FileName == targetFile
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