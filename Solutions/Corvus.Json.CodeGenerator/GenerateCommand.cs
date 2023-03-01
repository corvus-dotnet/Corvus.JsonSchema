using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Corvus.Json.CodeGeneration;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;
using Spectre.Console.Cli;
using Microsoft.CodeAnalysis;

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

        [Description("The path to the schema file to process.")]
        [CommandArgument(0, "[schemaFile]")]
        public string? SchemaFile { get; init; }
    }

    /// <inheritdoc/>
    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(settings.SchemaFile); // We will never see this exception if the framework is doing its job; it should have blown up inside the CLI command handling
        ArgumentNullException.ThrowIfNullOrEmpty(settings.RootNamespace); // We will never see this exception if the framework is doing its job; it should have blown up inside the CLI command handling

        return GenerateTypes(settings.SchemaFile, settings.RootNamespace, settings.RootPath, settings.RebaseToRootPath, settings.OutputPath, settings.OutputMapFile, settings.OutputRootTypeName, settings.UseSchema);
    }

    private static async Task<int> GenerateTypes(string schemaFile, string rootNamespace, string? rootPath, bool rebaseToRootPath, string? outputPath, string? outputMapFile, string? rootTypeName, SchemaVariant schemaVariant)
    {
        try
        {
            var typeBuilder = new JsonSchemaTypeBuilder(new CompoundDocumentResolver(new FileSystemDocumentResolver(), new HttpClientDocumentResolver(new HttpClient())));
            JsonReference reference = new(schemaFile, rootPath ?? string.Empty);
            schemaVariant = ValidationSemanticsToSchemaVariant(await typeBuilder.GetValidationSemantics(reference, rebaseToRootPath).ConfigureAwait(false));
            IJsonSchemaBuilder builder =
                schemaVariant switch
                {
                    SchemaVariant.Draft6 => new CodeGeneration.Draft6.JsonSchemaBuilder(typeBuilder),
                    SchemaVariant.Draft7 => new CodeGeneration.Draft7.JsonSchemaBuilder(typeBuilder),
                    SchemaVariant.Draft202012 => new CodeGeneration.Draft202012.JsonSchemaBuilder(typeBuilder),
                    SchemaVariant.Draft201909 => new CodeGeneration.Draft201909.JsonSchemaBuilder(typeBuilder),
                    _ => new CodeGeneration.Draft202012.JsonSchemaBuilder(typeBuilder)
                };

            (string RootType, ImmutableDictionary<JsonReference, TypeAndCode> GeneratedTypes) = await builder.BuildTypesFor(reference, rootNamespace, rebaseToRootPath, rootTypeName: rootTypeName).ConfigureAwait(false);

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

            foreach (KeyValuePair<JsonReference, TypeAndCode> generatedType in GeneratedTypes)
            {
                Console.WriteLine($"Generating: {generatedType.Value.DotnetTypeName}");
                foreach (CodeAndFilename typeAndCode in generatedType.Value.Code)
                {
                    try
                    {
                        string source =
                            SyntaxFactory.ParseCompilationUnit(typeAndCode.Code)
                             .NormalizeWhitespace()
                             .GetText()
                             .ToString();

                        string outputFile = Path.Combine(outputPath, typeAndCode.Filename);
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
                            File.AppendAllText(mapFile, $"{{\"key\": \"{JsonEncodedText.Encode(generatedType.Key)}\", \"class\": \"{JsonEncodedText.Encode(generatedType.Value.DotnetTypeName)}\", \"path\": \"{JsonEncodedText.Encode(outputFile)}\"}}\r\n");
                        }
                    }
                    catch (Exception)
                    {
                        Console.Error.WriteLine($"Unable to parse generated type: {generatedType.Value.DotnetTypeName} from location {generatedType.Key}");
                        return -1;
                    }
                }
            }

            if (!string.IsNullOrEmpty(mapFile))
            {
                File.AppendAllText(mapFile, "]");
            }

        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return -1;
        }

        return 0;
    }

    private static SchemaVariant ValidationSemanticsToSchemaVariant(ValidationSemantics validationSemantics)
    {
        if (validationSemantics == ValidationSemantics.Unknown)
        {
            return SchemaVariant.NotSpecified;
        }

        if ((validationSemantics & ValidationSemantics.Draft6) != 0)
        {
            return SchemaVariant.Draft6;
        }

        if ((validationSemantics & ValidationSemantics.Draft7) != 0)
        {
            return SchemaVariant.Draft7;
        }

        if ((validationSemantics & ValidationSemantics.Draft201909) != 0)
        {
            return SchemaVariant.Draft201909;
        }

        if ((validationSemantics & ValidationSemantics.Draft202012) != 0)
        {
            return SchemaVariant.Draft202012;
        }

        return SchemaVariant.NotSpecified;
    }

}
