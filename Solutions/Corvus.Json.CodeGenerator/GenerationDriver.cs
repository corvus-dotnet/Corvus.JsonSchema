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
                ValidationContext result = generatorConfig.Validate(ValidationContext.ValidContext, ValidationLevel.Detailed);
                AnsiConsole.MarkupLine("[red]Error: Invalid configuration[/]");
                foreach (ValidationResult validationResult in result.Results)
                {
                    AnsiConsole.MarkupLineInterpolated($"[yellow]{validationResult.Message}[/] [white]{validationResult.Location?.ValidationLocation} {validationResult.Location?.SchemaLocation} {validationResult.Location?.DocumentLocation}[/]");
                }

                return -1;
            }

            IDocumentResolver documentResolver = new CompoundDocumentResolver(new FileSystemDocumentResolver(), new HttpClientDocumentResolver(new HttpClient()));

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
                ProgressTask outerTask = context.AddTask($"Generating JSON types", maxValue: generatorConfig.TypesToGenerate.GetArrayLength());
                List<TypeDeclaration> typesToGenerate = [];

                GeneratorConfig.NamedTypeList namedTypes = generatorConfig.NamedTypes ?? GeneratorConfig.NamedTypeList.EmptyArray;

                string? fallbackOutputPath = null;

                foreach (GeneratorConfig.GenerationSpecification generatorSpecification in generatorConfig.TypesToGenerate)
                {
                    string schemaFile = (string)generatorSpecification.SchemaFile;
                    JsonReference reference = new(schemaFile, generatorSpecification.RootPath is JsonUriReference rootPath ? (string)rootPath : string.Empty);
                    ProgressTask typeBuilderTask = context.AddTask($"Building type declarations for {reference}");
                    TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(reference, defaultVocabulary, generatorSpecification.RebaseToRootPath ?? false).ConfigureAwait(false);
                    typesToGenerate.Add(rootType);

                    if (fallbackOutputPath is null && Path.Exists(schemaFile))
                    {
                        // Set our fallback output path to be the path of the first schema file we find
                        fallbackOutputPath = Path.GetDirectoryName(schemaFile);
                    }

                    if (generatorSpecification.OutputRootTypeName is JsonString outputRootTypeName)
                    {
                        namedTypes = namedTypes.Add(GeneratorConfig.NamedTypeSpecification.Create(outputRootTypeName, new JsonIriReference(rootType.LocatedSchema.Location), generatorSpecification.OutputRootNamespace));
                    }

                    typeBuilderTask.Increment(100);
                    outerTask.Increment(1);
                    typeBuilderTask.StopTask();
                }

                var options = new CSharpLanguageProvider.Options(
                    (string)generatorConfig.RootNamespace,
                    namedTypes: namedTypes.Select(n => new CSharpLanguageProvider.NamedType(new((string)n.Reference), (string)n.DotnetTypeName, n.DotnetNamespace?.GetString())).ToArray(),
                    namespaces: generatorConfig.Namespaces?.Select(n => new CSharpLanguageProvider.Namespace(new JsonReference((string)n.Key), (string)n.Value)).ToArray(),
                    alwaysAssertFormat: generatorConfig.AssertFormat ?? true,
                    useOptionalNameHeuristics: !(generatorConfig.DisableOptionalNameHeuristics ?? false),
                    optionalAsNullable: (generatorConfig.OptionalAsNullableValue ?? GeneratorConfig.OptionalAsNullable.DefaultInstance).Equals(GeneratorConfig.OptionalAsNullable.EnumValues.NullOrUndefined),
                    disabledNamingHeuristics: generatorConfig.DisabledNamingHeuristics?.Select(n => (string)n).ToArray());

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

                string? mapFile = generatorConfig.OutputMapFile is JsonString omf ? Path.Combine(outputPath, (string)omf) : null;
                if (!string.IsNullOrEmpty(mapFile))
                {
                    File.Delete(mapFile);
                    File.AppendAllText(mapFile, "[");
                }

                bool first = true;

                currentTask = context.AddTask("Writing files", true, generatedCode.Count);

                HashSet<string> writtenFiles = [];

                foreach (GeneratedCodeFile generatedCodeFile in generatedCode)
                {
                    ProgressTask subtask = context.AddTask($"{generatedCodeFile.FileName} [green]({generatedCodeFile.TypeDeclaration.RelativeSchemaLocation.ToString().EscapeMarkup()})[/]");
                    currentTask.Increment(1);
                    string source = generatedCodeFile.FileContent;

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

                    File.WriteAllText(outputFile, source);

                    if (!string.IsNullOrEmpty(mapFile))
                    {
                        if (first)
                        {
                            first = false;
                        }
                        else
                        {
                            File.AppendAllText(mapFile, ", ");
                        }
                        File.AppendAllText(mapFile, $"{{\"key\": \"{JsonEncodedText.Encode(generatedCodeFile.TypeDeclaration.LocatedSchema.Location)}\", \"class\": \"{JsonEncodedText.Encode(CSharpLanguageProvider.GetFullyQualifiedDotnetTypeName(generatedCodeFile))}\", \"path\": \"{JsonEncodedText.Encode(outputFile)}\"}}\r\n");
                    }

                    subtask.Increment(100);
                    subtask.StopTask();
                }


                if (!string.IsNullOrEmpty(mapFile))
                {
                    await File.AppendAllTextAsync(mapFile, "]");
                }

                currentTask.StopTask();
                outerTask.Increment(100);
                AnsiConsole.MarkupLineInterpolated($"Completed in: [green]{outerTask.ElapsedTime?.TotalSeconds}s[/]");
                outerTask.StopTask();
            });

        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return -1;
        }

        return 0;
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
