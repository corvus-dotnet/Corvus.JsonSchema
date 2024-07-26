using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Corvus.Json.CodeGeneration;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;
using Spectre.Console.Cli;
using Microsoft.CodeAnalysis;
using Corvus.Json.CodeGeneration.CSharp;
using Spectre.Console;

namespace Corvus.Json.SchemaGenerator;

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
        [Description("The dotnet type name for the root type.")]
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
    }

    /// <inheritdoc/>
    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(settings.SchemaFile); // We will never see this exception if the framework is doing its job; it should have blown up inside the CLI command handling
        ArgumentNullException.ThrowIfNullOrEmpty(settings.RootNamespace); // We will never see this exception if the framework is doing its job; it should have blown up inside the CLI command handling

        return GenerateTypes(settings.SchemaFile, settings.RootNamespace, settings.RootPath, settings.RebaseToRootPath, settings.OutputPath, settings.OutputMapFile, settings.OutputRootTypeName, settings.UseSchema, settings.AssertFormat);
    }

    private static async Task<int> GenerateTypes(string schemaFile, string rootNamespace, string? rootPath, bool rebaseToRootPath, string? outputPath, string? outputMapFile, string? rootTypeName, SchemaVariant schemaVariant, bool assertFormat)
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

            Progress progress = AnsiConsole.Progress();

            await progress.StartAsync(async context =>
            {
                ProgressTask currentTask = context.AddTask($"Building type declaration for {reference}", true);
                TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(reference, defaultVocabulary, rebaseToRootPath).ConfigureAwait(false);
                currentTask.StopTask();

                var options = new CSharpLanguageProvider.Options(
                    rootNamespace,
                    namedTypes: rootTypeName is string rtn ? [new CSharpLanguageProvider.NamedType(rootType.LocatedSchema.Location, rtn)] : null,
                    alwaysAssertFormat: assertFormat);

                currentTask = context.AddTask($"Generating code for {reference}", true);
                var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
                IReadOnlyCollection<GeneratedCodeFile> generatedCode =
                    typeBuilder.GenerateCodeUsing(
                        languageProvider,
                        rootType);
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

                currentTask = context.AddTask("Generating files", true, generatedCode.Count);

                foreach (GeneratedCodeFile generatedCodeFile in generatedCode)
                {
                    currentTask.Description = $"Generating {generatedCodeFile.TypeDeclaration.RelativeSchemaLocation}: {generatedCodeFile.FileName}";
                    currentTask.Increment(1);
                    try
                    {
                        string source = generatedCodeFile.FileContent;

                        string outputFile = Path.Combine(outputPath, generatedCodeFile.FileName);
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
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Unable to parse generated type: {CSharpLanguageProvider.GetFullyQualifiedDotnetTypeName(generatedCodeFile)} from location {generatedCodeFile.TypeDeclaration.LocatedSchema.Location}", ex);
                    }
                }

                if (!string.IsNullOrEmpty(mapFile))
                {
                    await File.AppendAllTextAsync(mapFile, "]");
                }
            });

        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
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
