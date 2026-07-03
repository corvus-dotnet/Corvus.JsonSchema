// <copyright file="GenerationDriverPython.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Json.CodeGenerator;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.Python.CodeGeneration;
using Spectre.Console;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Drives Python code generation — the Python engine, a peer of <see cref="GenerationDriverTypeScript"/>.
/// It reuses the V5 engine's language-neutral schema-loading, vocabulary, and file-writing machinery,
/// emitting idiomatic, byte-native Python (module-per-type TypedDict surfaces + AOT validators) via the
/// <see cref="PythonLanguageProvider"/> instead of C#.
/// </summary>
public static class GenerationDriverPython
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

    private static async Task ExecuteTask(GeneratorConfig generatorConfig, ProgressContext context, IVocabulary defaultVocabulary, JsonSchemaTypeBuilder typeBuilder)
    {
        ProgressTask outerTask = context.AddTask("Generating Python types", maxValue: generatorConfig.TypesToGenerate.GetArrayLength());

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

        ProgressTask currentTask = context.AddTask("Generating Python for schema.");

        // Where generated modules import the shared model runtime from: the CORVUS_PY_RUNTIME_MODULE env var,
        // else the published package name. (A --pyRuntimeModule config option is a later increment.)
        string runtimeModule = Environment.GetEnvironmentVariable("CORVUS_PY_RUNTIME_MODULE") is { Length: > 0 } env
            ? env
            : "corvus_json_runtime";

        PythonLanguageProvider provider = PythonLanguageProvider.DefaultWithOptions(
            new PythonLanguageProvider.Options(runtimeModule, generatorConfig.AssertFormat ?? true));
        provider.RootHint = rootType;
        IReadOnlyCollection<GeneratedCodeFile> generatedCode = typeBuilder.GenerateCodeUsing(provider, typesToGenerate, CancellationToken.None);

        // Re-export the root type's validator as the package-level `evaluate` entry point (names are assigned
        // during GenerateCodeUsing, so this runs after it).
        if (rootType is { } root)
        {
            string export = provider.RootEvaluateExport(root);
            generatedCode = generatedCode
                .Select(f => f.FileName == "__init__.py"
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