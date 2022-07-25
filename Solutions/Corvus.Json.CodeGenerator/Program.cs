using System.Collections.Immutable;
using System.CommandLine;
using System.Text.Json;
using Corvus.Json.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.SchemaGenerator;

class Program
{
    static Task<int> Main(string[] args)
    {
        var rootNamespace = new Option<string>(
                            "--rootNamespace",
                            description: "The default root namespace for generated types");
        var rootPath = new Option<string>(
                            "--rootPath",
                            description: "The path in the document for the root type.");
        var useSchema = new Option<SchemaVariant>(
                            "--useSchema",
                            getDefaultValue: () => SchemaVariant.NotSpecified,
                            description: "Override the schema variant to use. This will default to draft2019-09 if it cannot be picked up from the schema itself.");
        var outputMapFile = new Option<string>(
                            "--outputMapFile",
                            description: "The name to use for a map file which includes details of the files that were written.");
        var outputPath = new Option<string>(
                            "--outputPath",
                            description: "The output directory. It defaults to the same folder as the schema file.");
        var outputRootTypeName = new Option<string?>(
                            "--outputRootTypeName",
                            getDefaultValue: () => null,
                            description: "The Dotnet TypeName for the root type.");
        var rebaseToRootPath = new Option<bool>(
                            "--rebaseToRootPath",
                            "If a --rootPath is specified, rebase the document as if it was rooted on the specified element.");

        var schemaFile = new Argument<string>(
                "schemaFile",
                "The path to the schema file to process")
        {
            Arity = ArgumentArity.ExactlyOne,
        };

        var rootCommand = new RootCommand
        {
            rootNamespace,
            rootPath,
            useSchema,
            outputMapFile,
            outputPath,
            outputRootTypeName,
            rebaseToRootPath,
        };

        rootCommand.Name = "generatejsonschematypes";
        rootCommand.Description = "Generate C# types from a JSON schema.";


        rootCommand.AddArgument(schemaFile);


        Handler.SetHandler(
            rootCommand,
            (schemaFile, rootNamespace, rootPath, rebaseToRootPath, outputPath, outputMapFile, outputRootTypeName, useSchema) =>
            {
                return GenerateTypes(schemaFile, rootNamespace, rootPath, rebaseToRootPath, outputPath, outputMapFile, outputRootTypeName, useSchema);
            },
            schemaFile,
            rootNamespace,
            rootPath,
            rebaseToRootPath,
            outputPath,
            outputMapFile,
            outputRootTypeName,
            useSchema
            );

        // Parse the incoming args and invoke the handler
        return rootCommand.InvokeAsync(args);
    }

    private static async Task<int> GenerateTypes(string schemaFile, string rootNamespace, string rootPath, bool rebaseToRootPath, string outputPath, string outputMapFile, string? rootTypeName, SchemaVariant schemaVariant)
    {
        try
        {
            var walker = new JsonWalker(new CompoundDocumentResolver(new FileSystemDocumentResolver(), new HttpClientDocumentResolver(new HttpClient())));
            var uri = new JsonUri(schemaFile);
            if (!uri.IsValid() || uri.GetUri().IsFile)
            {
                // If this is, in fact, a local file path, not a uri, then convert to a fullpath and URI-style separators.
                if (!Path.IsPathFullyQualified(schemaFile))
                {
                    schemaFile = Path.GetFullPath(schemaFile);
                }

                schemaFile = schemaFile.Replace('\\', '/');
            }

            JsonReference reference = new JsonReference(schemaFile).Apply(new JsonReference(rootPath));
            string resolvedReference = await walker.TryRebaseDocumentToPropertyValue(reference, "$id").ConfigureAwait(false);

            if (schemaVariant == SchemaVariant.NotSpecified)
            {
                if (await TryGetSchemaFrom(walker, resolvedReference).ConfigureAwait(false) is SchemaVariant variant)
                {
                    schemaVariant = variant;
                }
            }

            IJsonSchemaBuilder builder =
                schemaVariant switch
                {
                    SchemaVariant.Draft7 => new CodeGeneration.Draft7.JsonSchemaBuilder(walker),
                    SchemaVariant.Draft202012 => new CodeGeneration.Draft202012.JsonSchemaBuilder(walker),
                    SchemaVariant.Draft201909 => new CodeGeneration.Draft201909.JsonSchemaBuilder(walker),
                    _ => new CodeGeneration.Draft202012.JsonSchemaBuilder(walker)
                };

            (string RootType, ImmutableDictionary<string, TypeAndCode> GeneratedTypes) result = await builder.BuildTypesFor(resolvedReference, rootNamespace, rebaseToRootPath, rootTypeName: rootTypeName).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
            else
            {
                outputPath = Path.GetDirectoryName(schemaFile)!;
            }

            string mapFile = string.IsNullOrEmpty(outputMapFile) ? outputMapFile : Path.Combine(outputPath, outputMapFile);
            if (!string.IsNullOrEmpty(mapFile))
            {
                File.Delete(mapFile);
                File.AppendAllText(mapFile, "[");
            }

            bool first = true;

            foreach (KeyValuePair<string, TypeAndCode> generatedType in result.GeneratedTypes)
            {
                Console.WriteLine($"Generating: {generatedType.Value.DotnetTypeName}");
                foreach (CodeAndFilename typeAndCode in generatedType.Value.Code)
                {
                    try
                    {
                        string source = SyntaxFactory.ParseCompilationUnit(typeAndCode.Code)
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

    private static async Task<SchemaVariant?> TryGetSchemaFrom(JsonWalker walker, string resolvedReference)
    {
        JsonElement? rootElement = await walker.GetDocumentElement(resolvedReference).ConfigureAwait(false);
        if (rootElement is JsonElement re && re.TryGetProperty("$schema", out JsonElement schema))
        {
            if (schema.ValueKind == JsonValueKind.String)
            {
                string? schemaValue = schema.GetString();
                if (schemaValue is string sv)
                {
                    if (sv == "http://json-schema.org/draft-07/schema" || sv == "http://json-schema.org/draft-07/schema#")
                    {
                        return SchemaVariant.Draft7;
                    }

                    if (sv == "https://json-schema.org/draft/2019-09/schema")
                    {
                        return SchemaVariant.Draft201909;
                    }

                    if (sv == "https://json-schema.org/draft/2020-12/schema")
                    {
                        return SchemaVariant.Draft202012;
                    }
                }
            }
        }

        return null;
    }
}
