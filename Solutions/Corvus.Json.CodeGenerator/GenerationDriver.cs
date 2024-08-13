using System.Text.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Corvus.Json.Internal;
using Microsoft.CodeAnalysis;
using Spectre.Console;

namespace Corvus.Json.CodeGenerator;

/// <summary>
/// Drives code generation from our command line model.
/// </summary>
public static class GenerationDriver
{
    internal static async Task<int> GenerateTypes(GeneratorConfig generatorConfig)
    {
        try
        {
            if (!generatorConfig.IsValid())
            {
                return WriteValidationErrors(generatorConfig);
            }

            var documentResolver = new CompoundDocumentResolver(new FileSystemDocumentResolver(), new HttpClientDocumentResolver(new HttpClient()));

            Metaschema.AddMetaschema(documentResolver);

            await RegisterAdditionalFiles(generatorConfig, documentResolver);

            VocabularyRegistry vocabularyRegistry = ReigsterVocabularies(documentResolver);

            // This will be our fallback vocabulary
            IVocabulary defaultVocabulary = GetFallbackVocabulary(generatorConfig.UseSchemaValue ?? GeneratorConfig.UseSchema.DefaultInstance);

            JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

            Progress progress =
                AnsiConsole.Progress()
                    .Columns(
                    [
                        new TaskDescriptionColumn { Alignment = Justify.Left },    // Task description
                    ]);

            await progress.StartAsync(async context =>
            {
                await ExecuteTask(generatorConfig, context, defaultVocabulary, typeBuilder);
            });

        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return -1;
        }

        return 0;
    }

    private static async Task RegisterAdditionalFiles(GeneratorConfig generatorConfig, CompoundDocumentResolver documentResolver)
    {
        if (generatorConfig.AdditionalFiles is GeneratorConfig.FileList f)
        {
            foreach (GeneratorConfig.FileSpecification fileSpec in f.EnumerateArray())
            {
                documentResolver.AddDocument((string)fileSpec.CanonicalUri, await JsonDocument.ParseAsync(File.OpenRead((string)fileSpec.ContentPath)));
            }
        }
    }

    private static VocabularyRegistry ReigsterVocabularies(IDocumentResolver documentResolver)
    {
        VocabularyRegistry vocabularyRegistry = new();

        // Add support for the vocabularies we are interested in.
        CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        CodeGeneration.OpenApi30.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);

        // And register the custom vocabulary for Corvus extensions.
        vocabularyRegistry.RegisterVocabularies(
            CodeGeneration.CorvusVocabulary.SchemaVocabulary.DefaultInstance);
        return vocabularyRegistry;
    }

    private static int WriteValidationErrors(GeneratorConfig generatorConfig)
    {
        ValidationContext result = generatorConfig.Validate(ValidationContext.ValidContext, ValidationLevel.Detailed);
        AnsiConsole.MarkupLine("[red]Error: Invalid configuration[/]");
        foreach (ValidationResult validationResult in result.Results)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{validationResult.Message}[/] [white]{validationResult.Location?.ValidationLocation} {validationResult.Location?.SchemaLocation} {validationResult.Location?.DocumentLocation}[/]");
        }

        return -1;
    }

    private static async Task ExecuteTask(GeneratorConfig generatorConfig, ProgressContext context, IVocabulary defaultVocabulary, JsonSchemaTypeBuilder typeBuilder)
    {
        ProgressTask outerTask = context.AddTask($"Generating JSON types", maxValue: generatorConfig.TypesToGenerate.GetArrayLength());

        List<TypeDeclaration> typesToGenerate = [];

        GeneratorConfig.NamedTypeList namedTypes = generatorConfig.NamedTypes ?? GeneratorConfig.NamedTypeList.EmptyArray;

        string? fallbackOutputPath = null;

        foreach (GeneratorConfig.GenerationSpecification generatorSpecification in generatorConfig.TypesToGenerate)
        {
            string schemaFile = (string)generatorSpecification.SchemaFile;
            JsonReference reference = new(schemaFile, generatorSpecification.RootPath is JsonUriReference rootPath ? (string)rootPath : string.Empty);
            ProgressTask typeBuilderTask = context.AddTask($"Building type declarations for [green]{reference}[/]");
            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(reference, defaultVocabulary, generatorSpecification.RebaseToRootPath ?? false);
            typesToGenerate.Add(rootType);

            if (fallbackOutputPath is null && Path.Exists(schemaFile))
            {
                // Set our fallback output path to be the path of the first schema file we find
                fallbackOutputPath = Path.GetDirectoryName(schemaFile);
            }

            if (generatorSpecification.OutputRootTypeName is JsonString outputRootTypeName)
            {
                namedTypes = namedTypes.Add(GeneratorConfig.NamedTypeSpecification.Create(outputRootTypeName, new JsonIriReference(rootType.ReducedTypeDeclaration().ReducedType.LocatedSchema.Location), generatorSpecification.OutputRootNamespace));
            }

            typeBuilderTask.Increment(100);
            outerTask.Increment(1);
            typeBuilderTask.StopTask();
        }

        CSharpLanguageProvider.Options options = MapGeneratorConfigToOptions(generatorConfig, namedTypes);

        ProgressTask currentTask = context.AddTask($"Generating code for schema.");
        var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
        IReadOnlyCollection<GeneratedCodeFile> generatedCode =
            typeBuilder.GenerateCodeUsing(
                languageProvider,
                typesToGenerate);
        currentTask.Increment(100);
        currentTask.StopTask();

        string? outputPath = generatorConfig.OutputPath?.GetString() ?? fallbackOutputPath ?? Environment.CurrentDirectory;

        if (!string.IsNullOrEmpty(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        currentTask = await WriteFiles(generatorConfig, context, currentTask, generatedCode, outputPath);

        currentTask.StopTask();
        outerTask.Increment(100);
        AnsiConsole.MarkupLineInterpolated($"Completed in: [green]{outerTask.ElapsedTime?.TotalSeconds}s[/]");
        outerTask.StopTask();
    }

    private static CSharpLanguageProvider.Options MapGeneratorConfigToOptions(in GeneratorConfig generatorConfig, in GeneratorConfig.NamedTypeList namedTypes)
    {
        return new CSharpLanguageProvider.Options(
            (string)generatorConfig.RootNamespace,
            namedTypes: namedTypes.Select(n => new CSharpLanguageProvider.NamedType(new((string)n.Reference), (string)n.DotnetTypeName, n.DotnetNamespace?.GetString())).ToArray(),
            namespaces: generatorConfig.Namespaces?.Select(n => new CSharpLanguageProvider.Namespace(new JsonReference((string)n.Key), (string)n.Value)).ToArray(),
            alwaysAssertFormat: generatorConfig.AssertFormat ?? true,
            useOptionalNameHeuristics: !(generatorConfig.DisableOptionalNameHeuristics ?? false),
            optionalAsNullable: (generatorConfig.OptionalAsNullableValue ?? GeneratorConfig.OptionalAsNullable.DefaultInstance).Equals(GeneratorConfig.OptionalAsNullable.EnumValues.NullOrUndefined),
            disabledNamingHeuristics: generatorConfig.DisabledNamingHeuristics?.Select(n => (string)n).ToArray());
    }

    private static async Task<string?> BeginMapFile(GeneratorConfig generatorConfig, string outputPath)
    {
        string? mapFile = generatorConfig.OutputMapFile is JsonString omf ? Path.Combine(outputPath, (string)omf) : null;
        if (!string.IsNullOrEmpty(mapFile))
        {
            File.Delete(mapFile);
            await File.AppendAllTextAsync(mapFile, "[");
        }

        return mapFile;
    }

    private static async Task EndMapFile(string? mapFile)
    {
        if (!string.IsNullOrEmpty(mapFile))
        {
            await File.AppendAllTextAsync(mapFile, "]");
        }
    }


    private static async Task<ProgressTask> WriteFiles(GeneratorConfig generatorConfig, ProgressContext context, ProgressTask currentTask, IReadOnlyCollection<GeneratedCodeFile> generatedCode, string outputPath)
    {
        currentTask = context.AddTask("Writing files", true, generatedCode.Count);

        HashSet<string> writtenFiles = [];

        int index = 0;

        string? mapFile = await BeginMapFile(generatorConfig, outputPath);

        foreach (GeneratedCodeFile generatedCodeFile in generatedCode)
        {
            WriteFile(context, currentTask, outputPath, mapFile, index++, writtenFiles, generatedCodeFile);
        }

        await EndMapFile(mapFile);

        return currentTask;
    }

    private static void WriteFile(ProgressContext context, ProgressTask currentTask, string outputPath, string? mapFile, int index, HashSet<string> writtenFiles, GeneratedCodeFile generatedCodeFile)
    {
        ProgressTask subtask = context.AddTask($"{generatedCodeFile.FileName} [green]({generatedCodeFile.TypeDeclaration.RelativeSchemaLocation.ToString().EscapeMarkup()})[/]");
        currentTask.Increment(1);
        string source = generatedCodeFile.FileContent;

        string outputFile = TruncateFileNameIfRequired(outputPath, writtenFiles, generatedCodeFile);

        File.WriteAllText(outputFile, source);

        WriteMapFile(mapFile, index, generatedCodeFile, outputFile);

        subtask.Increment(100);
        subtask.StopTask();
    }

    private static string TruncateFileNameIfRequired(string outputPath, HashSet<string> writtenFiles, GeneratedCodeFile generatedCodeFile)
    {
        string outputFile = Path.Combine(outputPath, generatedCodeFile.FileName);
        string originalFileName = outputFile;
        outputFile = PathTruncator.TruncatePath(outputFile);
        if (!writtenFiles.Add(outputFile))
        {
            if (originalFileName != outputFile)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]The file path [/][white]{originalFileName}[/] [red]was too long.[/]");
                AnsiConsole.MarkupLineInterpolated($"[red]It was truncated to [/][white]{outputFile}[/][red], but that file name was already in use.[/]");
                AnsiConsole.MarkupLineInterpolated($"[red]Consider using a shallower path for your output files, or explicitly map types into a root namespace, rather than nesting in their parent.[/]");
                outputFile = originalFileName;
            }
            else
            {
                throw new InvalidOperationException("Unexpected duplicate file generated.");
            }
        }

        return outputFile;
    }

    private static void WriteMapFile(string? mapFile, int index, GeneratedCodeFile generatedCodeFile, string outputFile)
    {
        if (!string.IsNullOrEmpty(mapFile))
        {
            if (index > 0)
            {
                File.AppendAllText(mapFile, ", ");
            }

            File.AppendAllText(mapFile, $"{{\"key\": \"{JsonEncodedText.Encode(generatedCodeFile.TypeDeclaration.LocatedSchema.Location)}\", \"class\": \"{JsonEncodedText.Encode(CSharpLanguageProvider.GetFullyQualifiedDotnetTypeName(generatedCodeFile))}\", \"path\": \"{JsonEncodedText.Encode(outputFile)}\"}}\r\n");
        }
    }

    private static IVocabulary GetFallbackVocabulary(GeneratorConfig.UseSchema schemaVariant)
    {
        return schemaVariant.Match(
            matchDraft4: static () => CodeGeneration.Draft4.VocabularyAnalyser.DefaultVocabulary,
            matchDraft6: static () => CodeGeneration.Draft6.VocabularyAnalyser.DefaultVocabulary,
            matchDraft7: static () => CodeGeneration.Draft7.VocabularyAnalyser.DefaultVocabulary,
            matchDraft201909: static () => CodeGeneration.Draft201909.VocabularyAnalyser.DefaultVocabulary,
            matchDraft202012: static () => CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            matchOpenApi30: static () => CodeGeneration.OpenApi30.VocabularyAnalyser.DefaultVocabulary,
            defaultMatch: static () => CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary);
    }
}
