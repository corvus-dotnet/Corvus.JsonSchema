using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Corvus.Json.CodeGeneration;
using System.Text.Json;
using Spectre.Console.Cli;
using Corvus.Json.CodeGeneration.CSharp;
using Spectre.Console;
using Corvus.Json.Internal;

namespace Corvus.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command for code generation.
/// </summary>
internal class GenerateCommand : AsyncCommand<GenerateCommand.Settings>
{
    /// <summary>
    /// Settings for the generate command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--rootNamespace")]
        [Description("The default root namespace for generated types.")]
        public string? RootNamespace { get; init; }

        [CommandOption("--rootPath")]
        [Description("The path in the document for the root type.")]
        public string? RootPath { get; init; }

        [CommandOption("--useSchema")]
        [Description("Override the schema variant to use. If NotSpecified, and it cannot be picked up from the schema itself, it will use Draft2020-12.")]
        [DefaultValue(SchemaVariant.NotSpecified)]
        public SchemaVariant UseSchema { get; init; }

        [CommandOption("--outputMapFile")]
        [Description("The name to use for a map file which includes details of the files that were written.")]
        public string? OutputMapFile { get; init; }

        [CommandOption("--outputPath")]
        [Description("The path to which to write the generated code.")]
        public string? OutputPath { get; init; }

        [CommandOption("--outputRootTypeName")]
        [Description("The dotnet type name for the root type.")]
        [DefaultValue(null)]
        public string? OutputRootTypeName { get; init; }

        [CommandOption("--rebaseToRootPath")]
        [Description("If a --rootPath is specified, rebase the document as if it was rooted on the specified element.")]
        [DefaultValue(false)]
        public bool RebaseToRootPath { get; init; }

        [CommandOption("--assertFormat")]
        [Description("If --assertFormat is specified, assert format specifications.")]
        [DefaultValue(true)]
        public bool AssertFormat { get; init; }

        [Description("The path to the schema file to process.")]
        [CommandArgument(0, "<schemaFile>")]
        [NotNull] // <> => NotNull
        public string? SchemaFile { get; init; }


        [CommandOption("--disableOptionalNamingHeuristics")]
        [Description("Disables optional naming heuristics.")]
        [DefaultValue(false)]
        public bool DisableOptionalNamingHeuristics { get; init; }

        [CommandOption("--optionalAsNullable")]
        [Description("If NullOrUndefined, optional properties are emitted as .NET nullable values.")]
        [DefaultValue(OptionalAsNullable.None)]
        public OptionalAsNullable OptionalAsNullable { get; init; }
    }

    /// <inheritdoc/>
    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(settings.SchemaFile); // We will never see this exception if the framework is doing its job; it should have blown up inside the CLI command handling
        ArgumentNullException.ThrowIfNullOrEmpty(settings.RootNamespace); // We will never see this exception if the framework is doing its job; it should have blown up inside the CLI command handling

        return GenerateTypes(settings.SchemaFile, settings.RootNamespace, settings.RootPath, settings.RebaseToRootPath, settings.OutputPath, settings.OutputMapFile, settings.OutputRootTypeName, settings.UseSchema, settings.AssertFormat, settings.DisableOptionalNamingHeuristics, settings.OptionalAsNullable);
    }

    private static async Task<int> GenerateTypes(string schemaFile, string rootNamespace, string? rootPath, bool rebaseToRootPath, string? outputPath, string? outputMapFile, string? rootTypeName, SchemaVariant schemaVariant, bool assertFormat, bool disableOptionalNameHeuristics, OptionalAsNullable optionalAsNullable)
    {
        try
        {
            IDocumentResolver documentResolver = new CompoundDocumentResolver(new FileSystemDocumentResolver(), new HttpClientDocumentResolver(new HttpClient()));

            VocabularyRegistry vocabularyRegistry = new();

            // Add support for the vocabularies we are interested in.
            CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
            CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
            CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
            CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
            CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
            CodeGeneration.OpenApi30.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);

            // This will be our fallback vocabulary
            IVocabulary defaultVocabulary = GetFallbackVocabulary(schemaVariant);

            JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

            JsonReference reference = new(schemaFile, rootPath ?? string.Empty);

            Progress progress =
                AnsiConsole.Progress()
                    .Columns(
                    [
                        new TaskDescriptionColumn { Alignment = Justify.Left },    // Task description
                    ]);

            await progress.StartAsync(async context =>
            {
                ProgressTask outerTask = context.AddTask($"Generating JSON type for {reference}");
                ProgressTask currentTask = context.AddTask($"Building type declarations for {reference}");
                TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(reference, defaultVocabulary, rebaseToRootPath).ConfigureAwait(false);
                currentTask.Increment(100);
                currentTask.StopTask();

                var options = new CSharpLanguageProvider.Options(
                    rootNamespace,
                    namedTypes: rootTypeName is string rtn ? [new CSharpLanguageProvider.NamedType(rootType.LocatedSchema.Location, rtn)] : null,
                    alwaysAssertFormat: assertFormat,
                    useOptionalNameHeuristics: !disableOptionalNameHeuristics,
                    optionalAsNullable: optionalAsNullable == OptionalAsNullable.NullOrUndefined);

                currentTask = context.AddTask($"Generating code for {reference}");
                var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
                IReadOnlyCollection<GeneratedCodeFile> generatedCode =
                    typeBuilder.GenerateCodeUsing(
                        languageProvider,
                        rootType);
                currentTask.Increment(100);
                currentTask.StopTask();

                if (!string.IsNullOrEmpty(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }
                else
                {
                    outputPath = Path.GetDirectoryName(schemaFile)!;
                }

                string? mapFile = string.IsNullOrEmpty(outputMapFile) ? outputMapFile : Path.Combine(outputPath, outputMapFile);
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
                            AnsiConsole.WriteLine($"[red]The file path [/][white]{originalFileName}[/] [red]was too long.[/]");
                            AnsiConsole.WriteLine($"[red]It was truncated to [/][white]{outputFile}[/][red], but that file name was already in use.[/]");
                            AnsiConsole.WriteLine($"[red]Consider using a shallower path for your output files, or explicitly map types into a root namespace, rather than nesting in their parent.[/]");
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

    private static IVocabulary GetFallbackVocabulary(SchemaVariant schemaVariant)
    {
        return schemaVariant switch
        {
            SchemaVariant.OpenApi30 => CodeGeneration.OpenApi30.VocabularyAnalyser.DefaultVocabulary,
            SchemaVariant.Draft4 => CodeGeneration.Draft4.VocabularyAnalyser.DefaultVocabulary,
            SchemaVariant.Draft6 => CodeGeneration.Draft6.VocabularyAnalyser.DefaultVocabulary,
            SchemaVariant.Draft7 => CodeGeneration.Draft7.VocabularyAnalyser.DefaultVocabulary,
            SchemaVariant.Draft201909 => CodeGeneration.Draft201909.VocabularyAnalyser.DefaultVocabulary,
            SchemaVariant.Draft202012 => CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,

            _ => CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
        };
    }
}
